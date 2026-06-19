using System.Text.Json;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Generering;

/// <summary>
/// Genererer et budsjettår fra årsmalen og legger forslagene på generér-flaten (adskilt fra køen).
/// Beregningen er ren (<see cref="Genereringsberegning"/>); denne tjenesten henter regler,
/// viderefører synlighet fra fjorårets godkjente frist (ellers synlighetsregelen), og lagrer
/// <see cref="ForslagType.Generert"/>-forslag. Ingenting publiseres her — godkjenning skjer via
/// godkjenningskøens publiseringsmønster.
/// </summary>
public class GenereringsTjeneste(AppDbContext db, ISynlighetsregel synlighetsregel) : IGenereringstjeneste
{
    public async Task<GenereringsResultat> GenererAsync(GenereringsForespoersel forespoersel, CancellationToken ct = default)
    {
        var maalaar = forespoersel.Maalbudsjettaar;
        var regler = await db.Gjentaksregler.AsNoTracking().ToListAsync(ct);

        var beregnet = Genereringsberegning.Beregn(maalaar, regler, forespoersel.ManuelleAnkre);

        // Fjorårets godkjente frister per løp, for videreføring av synlighet.
        var fjoraar = await db.Frister
            .AsNoTracking()
            .Include(f => f.Synlighet)
            .Where(f => f.Budsjettaar == maalaar - 1 && f.Status == FristStatus.Godkjent && f.Loep != null)
            .ToListAsync(ct);
        var fjoraarPerLoep = fjoraar
            .GroupBy(f => f.Loep!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Erstatt tidligere åpne genererte forslag for året (idempotent re-generering).
        var gamle = await db.Forslag
            .Where(f => f.ForslagType == ForslagType.Generert
                        && f.Status == FristStatus.Forslag
                        && f.Budsjettaar == maalaar)
            .ToListAsync(ct);
        db.Forslag.RemoveRange(gamle);

        var feil = new List<string>();
        var ankreSomMaaSettes = new List<string>();
        var antall = 0;

        foreach (var r in beregnet)
        {
            if (r.ErFeil)
            {
                feil.Add($"{Visningsnavn(r.Regel)}: {r.Feil}");
                // En uoppløselig kjede skyldes ofte et valgårssensitivt anker som må settes manuelt.
                if (r.Regel.Valgaarssensitiv)
                {
                    ankreSomMaaSettes.Add(r.Regel.Loep);
                }
                continue;
            }

            db.Forslag.Add(new Forslag
            {
                Id = Guid.NewGuid(),
                ForslagType = ForslagType.Generert,
                Opphav = Opphav.Admin,
                KildeEllerInnsender = $"Generert {maalaar}",
                Tittel = Visningsnavn(r.Regel),
                Dato = r.Dato,
                Datopresisjon = r.Presisjon,
                Datokvalifikator = null,
                Budsjettaar = maalaar,
                Kategori = r.Regel.Kategori,
                Loep = r.Regel.Loep,
                Status = FristStatus.Forslag,
                ForeslaattSynlighet = JsonSerializer.Serialize(
                    VidérefoertSynlighet(r.Regel.Loep, fjoraarPerLoep))
            });
            antall++;
        }

        await db.SaveChangesAsync(ct);

        return new GenereringsResultat(maalaar, antall, feil, ankreSomMaaSettes.Distinct().ToList());
    }

    public async Task<IReadOnlyList<GenerertForslagDto>> HentGenererteAsync(int maalbudsjettaar, CancellationToken ct = default)
    {
        var forslag = await db.Forslag
            .AsNoTracking()
            .Where(f => f.ForslagType == ForslagType.Generert
                        && f.Status == FristStatus.Forslag
                        && f.Budsjettaar == maalbudsjettaar)
            .ToListAsync(ct);

        // Fjorårets datoer for sammenligning, og reglene for å avlede valgårsmarkering.
        var fjoraar = await db.Frister
            .AsNoTracking()
            .Where(f => f.Budsjettaar == maalbudsjettaar - 1 && f.Status == FristStatus.Godkjent && f.Loep != null)
            .ToListAsync(ct);
        var fjoraarDato = fjoraar
            .GroupBy(f => f.Loep!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Dato, StringComparer.OrdinalIgnoreCase);

        var valgaarssensitive = await db.Gjentaksregler
            .AsNoTracking()
            .Where(r => r.Valgaarssensitiv)
            .Select(r => r.Loep)
            .ToListAsync(ct);
        var valgaarssensitivSet = valgaarssensitive.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return forslag
            .Select(f => new GenerertForslagDto
            {
                ForslagId = f.Id,
                Tittel = f.Tittel,
                Loep = f.Loep,
                Kategori = f.Kategori,
                Dato = f.Dato,
                Datopresisjon = f.Datopresisjon,
                Tentativ = f.Datopresisjon != Datopresisjon.Dag,
                Valgaarsflagg = f.Loep is not null
                                && valgaarssensitivSet.Contains(f.Loep)
                                && f.Dato is { } d
                                && Valgaar.Type(d.Year) != Valgtype.Ingen,
                ForeslaattSynlighet = LesSynlighet(f.ForeslaattSynlighet),
                FjoraarDato = f.Loep is not null && fjoraarDato.TryGetValue(f.Loep, out var fd) ? fd : null
            })
            .OrderBy(d => d.Dato ?? DateOnly.MaxValue)
            .ThenBy(d => d.Tittel)
            .ToList();
    }

    public async Task<IReadOnlyList<int>> HentAarMedForslagAsync(CancellationToken ct = default)
        => await db.Forslag
            .AsNoTracking()
            .Where(f => f.ForslagType == ForslagType.Generert && f.Status == FristStatus.Forslag)
            .Select(f => f.Budsjettaar)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

    /// <summary>
    /// Videreført synlighet: fjorårets godkjente frist for samme løp gir standard. POL bæres med
    /// som forslag (vises for administrator), men krever aktiv bekreftelse ved godkjenning — det
    /// settes aldri stilltiende. Uten fjorårsfrist brukes synlighetsregelens default (uten POL).
    /// </summary>
    private List<string> VidérefoertSynlighet(string loep, IReadOnlyDictionary<string, Frist> fjoraarPerLoep)
    {
        if (fjoraarPerLoep.TryGetValue(loep, out var fjor))
        {
            return fjor.Synlighet.Select(s => s.GruppeKode).ToList();
        }

        return synlighetsregel.StandardForslagssynlighet().ToList();
    }

    private static string Visningsnavn(Gjentaksregel regel)
        => string.IsNullOrWhiteSpace(regel.Tittel) ? regel.Loep : regel.Tittel;

    private static IReadOnlyList<string> LesSynlighet(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
