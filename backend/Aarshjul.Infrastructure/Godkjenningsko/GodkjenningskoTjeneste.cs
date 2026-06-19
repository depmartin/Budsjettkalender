using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Godkjenningsko;

/// <summary>
/// Implementerer godkjenningskøen. Godkjenning publiserer et <see cref="Forslag"/> til en
/// <see cref="Frist"/> med samme synlighetsvalidering som manuell innlegging (POL kun ved aktivt
/// valg — her håndhevet på serveren via <see cref="GodkjennInndata.PolBekreftet"/>). Avvisning
/// bevarer forslaget (status avvist) og oppretter et varsel ved brukerforslag.
/// </summary>
public class GodkjenningskoTjeneste(AppDbContext db) : IGodkjenningsko
{
    private const string Pol = "POL";

    public async Task<IReadOnlyList<Koelement>> HentKoAsync(Kofilter? filter = null, CancellationToken ct = default)
    {
        // Genererte forslag behandles på sin egen generér-flate, adskilt fra den løpende køen
        // (beslutning 2026-06-18). De utelates derfor her.
        var forslag = await db.Forslag
            .AsNoTracking()
            .Include(f => f.UttrekksBevis)
            .Where(f => f.Status == FristStatus.Forslag && f.ForslagType != ForslagType.Generert)
            .ToListAsync(ct);

        // Endringsforslag viser før/etter — hent gjeldende verdier for de berørte fristene.
        var endrerIder = forslag.Where(f => f.EndrerFristId is not null)
            .Select(f => f.EndrerFristId!.Value).Distinct().ToList();
        var gjeldende = endrerIder.Count == 0
            ? new Dictionary<Guid, FristGjeldende>()
            : await db.Frister.AsNoTracking().Include(f => f.Synlighet)
                .Where(f => endrerIder.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => new FristGjeldende(
                    f.Tittel, f.Dato, f.Budsjettaar, f.Kategori,
                    f.Synlighet.Select(s => s.GruppeKode).ToList()), ct);

        var elementer = forslag.Select(f => new Koelement
        {
            Id = f.Id,
            ForslagType = f.ForslagType,
            Opphav = f.Opphav,
            KildeEllerInnsender = f.KildeEllerInnsender,
            Tittel = f.Tittel,
            Dato = f.Dato,
            Datopresisjon = f.Datopresisjon,
            Datokvalifikator = f.Datokvalifikator,
            Budsjettaar = f.Budsjettaar,
            Kategori = f.Kategori,
            Loep = f.Loep,
            Notat = f.Notat,
            ForeslaattSynlighet = LesSynlighet(f.ForeslaattSynlighet),
            EndrerFristId = f.EndrerFristId,
            DokumentId = f.DokumentId,
            Bevis = f.UttrekksBevis
                .Select(b => new KoBevis(b.Felt, b.TolketVerdi, b.Kildeutdrag, b.Konfidens)).ToList(),
            Gjeldende = f.EndrerFristId is Guid id && gjeldende.TryGetValue(id, out var g) ? g : null
        });

        if (filter is not null)
        {
            if (filter.Opphav is { } opphav) elementer = elementer.Where(e => e.Opphav == opphav);
            if (filter.Kategori is { } kat) elementer = elementer.Where(e => e.Kategori == kat);
            if (filter.ForslagType is { } ft) elementer = elementer.Where(e => e.ForslagType == ft);
            if (!string.IsNullOrWhiteSpace(filter.Kilde))
                elementer = elementer.Where(e => e.KildeEllerInnsender == filter.Kilde);
            if (filter.UkjentType is { } ukjent) elementer = elementer.Where(e => e.ErUkjentType == ukjent);
        }

        // Gruppert per kilde i visningen — sorter stabilt på kilde, så tittel.
        return elementer
            .OrderBy(e => e.KildeEllerInnsender ?? "")
            .ThenBy(e => e.Tittel)
            .ToList();
    }

    public async Task<Guid> GodkjennAsync(GodkjennInndata inndata, CancellationToken ct = default)
    {
        var forslag = await db.Forslag
            .FirstOrDefaultAsync(f => f.Id == inndata.ForslagId && f.Status == FristStatus.Forslag, ct)
            ?? throw new Valideringsfeil("Forslaget finnes ikke eller er allerede behandlet.");

        return await PubliserAsync(forslag, inndata.Synlighetskoder, inndata.PolBekreftet, ct);
    }

    /// <summary>Felles publisering: gjelder både ren godkjenning og «juster og godkjenn». For
    /// endringsforslag oppdateres kun mål-fristens innhold; <c>synlig_for</c> står urørt (punkt C).</summary>
    private async Task<Guid> PubliserAsync(Forslag forslag, IReadOnlyList<string> synlighetskoder, bool polBekreftet, CancellationToken ct)
    {
        if (forslag.Dato is not { } dato)
            throw new Valideringsfeil("Forslaget mangler dato. Juster forslaget og sett en dato før godkjenning.");

        Guid fristId;
        if (forslag.ForslagType == ForslagType.Endring && forslag.EndrerFristId is { } endrerId)
        {
            // Endringsforslag rører kun innhold — fristens synlig_for står urørt. Derfor ingen
            // synlighetsvalidering og ingen oppdatering av synlighet her (synlighet er admins
            // domene og POL-sensitivt; det styres aldri via et endringsforslag).
            var frist = await db.Frister
                .FirstOrDefaultAsync(f => f.Id == endrerId, ct)
                ?? throw new Valideringsfeil("Fristen som skulle endres finnes ikke.");

            SettFristfelter(frist, forslag, dato);
            fristId = frist.Id;
        }
        else
        {
            var koder = await ValiderSynlighetAsync(synlighetskoder, polBekreftet, ct);

            var frist = new Frist
            {
                Id = Guid.NewGuid(),
                Tittel = forslag.Tittel.Trim(),
                Opphav = forslag.Opphav,
                Status = FristStatus.Godkjent,
                Kilde = forslag.Opphav == Opphav.Robot ? forslag.KildeEllerInnsender : "manuell",
                ForeslaattAv = forslag.Opphav == Opphav.Bruker ? forslag.KildeEllerInnsender : null,
                DokumentId = forslag.DokumentId
            };
            SettFristfelter(frist, forslag, dato);
            foreach (var kode in koder)
                frist.Synlighet.Add(new FristSynlighet { GruppeKode = kode });
            db.Frister.Add(frist);
            fristId = frist.Id;
        }

        forslag.Status = FristStatus.Godkjent;
        VarsleVedBrukerforslag(forslag, "Forslaget ditt er godkjent.", null);

        await db.SaveChangesAsync(ct);
        return fristId;
    }

    public async Task AvvisAsync(Guid forslagId, string? begrunnelse = null, CancellationToken ct = default)
    {
        var forslag = await db.Forslag
            .FirstOrDefaultAsync(f => f.Id == forslagId && f.Status == FristStatus.Forslag, ct)
            ?? throw new Valideringsfeil("Forslaget finnes ikke eller er allerede behandlet.");

        forslag.Status = FristStatus.Avvist;
        VarsleVedBrukerforslag(forslag, "Forslaget ditt er avvist.", begrunnelse);

        await db.SaveChangesAsync(ct);
    }

    public async Task<ForslagForJustering?> HentForslagForJusteringAsync(Guid forslagId, CancellationToken ct = default)
    {
        var forslag = await db.Forslag.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == forslagId && f.Status == FristStatus.Forslag, ct);
        if (forslag is null)
            return null;

        var erEndring = forslag.ForslagType == ForslagType.Endring;

        // Prefyll innholdsfeltene. For endringsforslag skjules synlighet (punkt C); ellers prefylles
        // foreslått synlighet UTEN POL, slik at POL aldri blir forhåndshuket i skjemaet.
        var synlighet = erEndring
            ? []
            : LesSynlighet(forslag.ForeslaattSynlighet)
                .Where(k => !string.Equals(k, Pol, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var felter = new FristInndata
        {
            Tittel = forslag.Tittel,
            Dato = forslag.Dato ?? DateOnly.FromDateTime(DateTime.Today),
            Datopresisjon = forslag.Datopresisjon,
            Datokvalifikator = forslag.Datokvalifikator,
            Budsjettaar = forslag.Budsjettaar,
            Kategori = forslag.Kategori,
            Loep = forslag.Loep,
            Notat = forslag.Notat,
            Synlighetskoder = synlighet
        };

        return new ForslagForJustering(felter, erEndring);
    }

    public async Task<Guid> JusterOgGodkjennAsync(Guid forslagId, FristInndata felter, CancellationToken ct = default)
    {
        var forslag = await db.Forslag
            .FirstOrDefaultAsync(f => f.Id == forslagId && f.Status == FristStatus.Forslag, ct)
            ?? throw new Valideringsfeil("Forslaget finnes ikke eller er allerede behandlet.");

        // Overskriv innholdsfeltene fra skjemaet før publisering. Synlighet håndteres i PubliserAsync
        // (ignoreres for endringsforslag). Aktiv avkryssing av POL i skjemaet ER den aktive bekreftelsen.
        forslag.Tittel = felter.Tittel;
        forslag.Dato = felter.Dato;
        forslag.Datopresisjon = felter.Datopresisjon;
        forslag.Datokvalifikator = felter.Datopresisjon == Datopresisjon.Maaned ? felter.Datokvalifikator : null;
        forslag.Budsjettaar = felter.Budsjettaar;
        forslag.Kategori = felter.Kategori;
        forslag.Loep = string.IsNullOrWhiteSpace(felter.Loep) ? null : felter.Loep.Trim();
        forslag.Notat = string.IsNullOrWhiteSpace(felter.Notat) ? null : felter.Notat.Trim();

        var polBekreftet = felter.Synlighetskoder.Any(k => string.Equals(k, Pol, StringComparison.OrdinalIgnoreCase));
        return await PubliserAsync(forslag, felter.Synlighetskoder, polBekreftet, ct);
    }

    private static void SettFristfelter(Frist frist, Forslag forslag, DateOnly dato)
    {
        frist.Tittel = forslag.Tittel.Trim();
        frist.Dato = dato;
        frist.Datopresisjon = forslag.Datopresisjon;
        frist.Datokvalifikator = forslag.Datopresisjon == Datopresisjon.Maaned ? forslag.Datokvalifikator : null;
        frist.Budsjettaar = forslag.Budsjettaar;
        frist.Kategori = forslag.Kategori;
        frist.Loep = string.IsNullOrWhiteSpace(forslag.Loep) ? null : forslag.Loep.Trim();
        frist.Notat = string.IsNullOrWhiteSpace(forslag.Notat) ? null : forslag.Notat.Trim();
    }

    private void VarsleVedBrukerforslag(Forslag forslag, string tekst, string? begrunnelse)
    {
        if (forslag.Opphav == Opphav.Bruker && !string.IsNullOrWhiteSpace(forslag.KildeEllerInnsender))
        {
            db.Varsler.Add(new Varsel
            {
                Id = Guid.NewGuid(),
                BrukerId = forslag.KildeEllerInnsender,
                Tekst = tekst,
                Begrunnelse = begrunnelse
            });
        }
    }

    /// <summary>Validerer at synlighet er valgt, peker på aktive grupper, og at POL kun settes ved aktiv bekreftelse.</summary>
    private async Task<List<string>> ValiderSynlighetAsync(IReadOnlyList<string> innkoder, bool polBekreftet, CancellationToken ct)
    {
        var koder = innkoder
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct()
            .ToList();

        if (koder.Count == 0)
            throw new Valideringsfeil("Velg minst én synlighetsgruppe.");

        if (koder.Any(k => string.Equals(k, Pol, StringComparison.OrdinalIgnoreCase)) && !polBekreftet)
            throw new Valideringsfeil("Synlighet for politisk ledelse (POL) krever aktiv bekreftelse.");

        var gyldige = await db.Synlighetsgrupper
            .Where(g => g.Aktiv && koder.Contains(g.Kode))
            .Select(g => g.Kode)
            .ToListAsync(ct);

        var ukjente = koder.Except(gyldige).ToList();
        if (ukjente.Count > 0)
            throw new Valideringsfeil($"Ukjente eller inaktive grupper: {string.Join(", ", ukjente)}.");

        return gyldige;
    }

    private static IReadOnlyList<string> LesSynlighet(string json)
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
