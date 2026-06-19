using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Brukerforslag;

/// <summary>
/// Tar imot bidragsyterforslag (kravdok. 5.3) og legger dem i samme godkjenningskø som
/// robotforslag, med <see cref="Opphav.Bruker"/> og innsenderens stabile id i
/// <c>KildeEllerInnsender</c> (slik varsler kan rutes tilbake). Foreslått synlighet bæres med,
/// men er kun et forslag administrator beslutter endelig. Avviste forslag bevares og kan
/// redigeres og sendes inn på nytt.
/// </summary>
public class ForslagsinnsendingTjeneste(AppDbContext db) : IForslagsinnsending
{
    public async Task<Guid> SendInnAsync(BrukerforslagInndata inndata, GjeldendeBruker innsender, CancellationToken ct = default)
    {
        var (synlighet, type) = await ValiderAsync(inndata, ct);

        var forslag = new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = type,
            Opphav = Opphav.Bruker,
            KildeEllerInnsender = innsender.Id,
            Status = FristStatus.Forslag,
            EndrerFristId = inndata.EndrerFristId,
            ForeslaattSynlighet = JsonSerializer.Serialize(synlighet),
            Tittel = inndata.Tittel.Trim()
        };
        SettFelter(forslag, inndata);

        db.Forslag.Add(forslag);
        await db.SaveChangesAsync(ct);
        return forslag.Id;
    }

    public async Task<IReadOnlyList<MineForslagElement>> HentMineAsync(string brukerId, CancellationToken ct = default)
    {
        var forslag = await db.Forslag.AsNoTracking()
            .Where(f => f.Opphav == Opphav.Bruker && f.KildeEllerInnsender == brukerId)
            .ToListAsync(ct);

        return forslag
            .OrderByDescending(f => f.Status == FristStatus.Forslag) // ventende øverst
            .ThenBy(f => f.Tittel)
            .Select(f => new MineForslagElement(
                f.Id, f.ForslagType, f.Tittel, f.Dato, f.Budsjettaar, f.Kategori, f.Status,
                Les(f.ForeslaattSynlighet), f.EndrerFristId))
            .ToList();
    }

    public async Task<BrukerforslagInndata?> HentForRedigeringAsync(Guid forslagId, string brukerId, CancellationToken ct = default)
    {
        var f = await db.Forslag.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == forslagId && x.Opphav == Opphav.Bruker && x.KildeEllerInnsender == brukerId, ct);
        if (f is null)
            return null;

        return new BrukerforslagInndata
        {
            Tittel = f.Tittel,
            Dato = f.Dato ?? DateOnly.FromDateTime(DateTime.Today),
            Datopresisjon = f.Datopresisjon,
            Datokvalifikator = f.Datokvalifikator,
            Budsjettaar = f.Budsjettaar,
            Kategori = f.Kategori,
            Notat = f.Notat,
            ForeslaattSynlighet = Les(f.ForeslaattSynlighet).ToList(),
            EndrerFristId = f.EndrerFristId
        };
    }

    public async Task SendInnPaaNyttAsync(Guid forslagId, BrukerforslagInndata inndata, GjeldendeBruker innsender, CancellationToken ct = default)
    {
        var forslag = await db.Forslag
            .FirstOrDefaultAsync(x => x.Id == forslagId && x.Opphav == Opphav.Bruker && x.KildeEllerInnsender == innsender.Id, ct)
            ?? throw new Valideringsfeil("Forslaget finnes ikke eller tilhører ikke deg.");

        if (forslag.Status != FristStatus.Avvist)
            throw new Valideringsfeil("Bare avviste forslag kan sendes inn på nytt.");

        var (synlighet, type) = await ValiderAsync(inndata, ct);
        SettFelter(forslag, inndata);
        forslag.ForeslaattSynlighet = JsonSerializer.Serialize(synlighet);
        forslag.ForslagType = type;
        forslag.EndrerFristId = inndata.EndrerFristId;
        forslag.Status = FristStatus.Forslag;
        await db.SaveChangesAsync(ct);
    }

    private static void SettFelter(Forslag forslag, BrukerforslagInndata inndata)
    {
        forslag.Tittel = inndata.Tittel.Trim();
        forslag.Dato = inndata.Dato;
        forslag.Datopresisjon = inndata.Datopresisjon;
        forslag.Datokvalifikator = inndata.Datopresisjon == Datopresisjon.Maaned ? inndata.Datokvalifikator : null;
        forslag.Budsjettaar = inndata.Budsjettaar;
        forslag.Kategori = inndata.Kategori;
        forslag.Notat = string.IsNullOrWhiteSpace(inndata.Notat) ? null : inndata.Notat.Trim();
    }

    /// <summary>Validerer tittel + (valgfri) foreslått synlighet, og utleder forslagstype.</summary>
    private async Task<(List<string> Synlighet, ForslagType Type)> ValiderAsync(BrukerforslagInndata inndata, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inndata.Tittel))
            throw new Valideringsfeil("Tittel må fylles ut.");

        var koder = inndata.ForeslaattSynlighet
            .Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList();

        List<string> gyldige = [];
        if (koder.Count > 0)
        {
            gyldige = await db.Synlighetsgrupper
                .Where(g => g.Aktiv && koder.Contains(g.Kode)).Select(g => g.Kode).ToListAsync(ct);
            var ukjente = koder.Except(gyldige).ToList();
            if (ukjente.Count > 0)
                throw new Valideringsfeil($"Ukjente eller inaktive grupper: {string.Join(", ", ukjente)}.");
        }

        var type = ForslagType.NyFrist;
        if (inndata.EndrerFristId is { } fristId)
        {
            var finnes = await db.Frister.AnyAsync(f => f.Id == fristId && f.Status == FristStatus.Godkjent, ct);
            if (!finnes)
                throw new Valideringsfeil("Fristen du foreslår å endre finnes ikke.");
            type = ForslagType.Endring;
        }

        return (gyldige, type);
    }

    private static IReadOnlyList<string> Les(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
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
