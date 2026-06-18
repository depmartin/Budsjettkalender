using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Frister;

/// <summary>
/// Databasenær implementasjon av <see cref="IFristlesing"/>. Synlighetsfiltreringen påføres
/// spørringen før projeksjon, slik at frister brukeren ikke har rett til aldri hentes fra
/// databasen — og dermed aldri kan sendes til klienten.
/// </summary>
public class FristTjeneste(AppDbContext db, TimeProvider klokke) : IFristlesing
{
    public async Task<IReadOnlyList<FristDto>> HentSynligeAsync(
        ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default)
    {
        var idag = Idag();
        var spørring = GrunnSpørring(ctx, filter, idag);
        return await ProjiserAsync(spørring.OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Tittel), ct);
    }

    public async Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(
        ISynlighetskontekst ctx, DateOnly fraOgMed, FristFilter filter, CancellationToken ct = default)
    {
        // Landingsflaten er alltid framoverskuende fra angitt dag.
        var spørring = GrunnSpørring(ctx, filter, fraOgMed, kunFramover: true)
            .OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Tittel);

        var kommende = await ProjiserAsync(spørring, ct);

        // Union av «innen 30 dager» og «minst de fem førstkommende».
        var grense = fraOgMed.AddDays(30);
        return kommende
            .Where((f, indeks) => indeks < 5 || f.Sorteringsdag <= grense)
            .ToList();
    }

    public async Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(
        ISynlighetskontekst ctx, bool inkluderHistorikk, CancellationToken ct = default)
    {
        var idag = Idag();
        var spørring = db.Frister
            .AsNoTracking()
            .Where(f => f.Status == FristStatus.Godkjent)
            .FiltrerSynlige(ctx);

        if (!inkluderHistorikk)
        {
            spørring = spørring.Where(f => f.Sorteringsdag >= idag);
        }

        return await spørring
            .Select(f => f.Budsjettaar)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    /// <summary>Synlige, godkjente frister som matcher kategori-/budsjettårsfilter og historikkregel.</summary>
    private IQueryable<Frist> GrunnSpørring(ISynlighetskontekst ctx, FristFilter filter, DateOnly idag, bool kunFramover = false)
    {
        var spørring = db.Frister
            .AsNoTracking()
            .Where(f => f.Status == FristStatus.Godkjent)
            .FiltrerSynlige(ctx);

        if (filter.Kategorier is { Count: > 0 } kategorier)
        {
            spørring = spørring.Where(f => kategorier.Contains(f.Kategori));
        }

        if (filter.Budsjettaar is { Count: > 0 } aar)
        {
            spørring = spørring.Where(f => aar.Contains(f.Budsjettaar));
        }

        // Frist vises t.o.m. fristdagen; flyttes til historikk dato + 1 (ren kalender, ingen forsinket-tilstand).
        if (kunFramover || !filter.InkluderHistorikk)
        {
            spørring = spørring.Where(f => f.Sorteringsdag >= idag);
        }

        return spørring;
    }

    private static Task<List<FristDto>> ProjiserAsync(IQueryable<Frist> spørring, CancellationToken ct)
        => spørring
            .Select(f => new FristDto(
                f.Id,
                f.Tittel,
                f.Dato,
                f.Datopresisjon,
                f.Datokvalifikator,
                f.Sorteringsdag,
                f.Budsjettaar,
                f.Kategori,
                f.Loep,
                f.Kilde,
                f.DokumentId,
                f.Notat,
                f.Status,
                f.Synlighet.Select(s => s.GruppeKode).ToList()))
            .ToListAsync(ct);

    private DateOnly Idag() => Datohjelp.Idag(klokke);
}
