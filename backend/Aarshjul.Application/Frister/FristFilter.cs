using Aarshjul.Domain;

namespace Aarshjul.Application.Frister;

/// <summary>
/// Brukervalg på toppen av synlighetsfiltreringen (kategori + budsjettår + historikk).
/// Dette er presentasjonsvalg, ikke en sikkerhetsmekanisme — synligheten håndheves alltid
/// separat på server.
/// </summary>
public class FristFilter
{
    /// <summary>Kategorier som skal vises. Tom/Null = alle.</summary>
    public IReadOnlyCollection<Kategori>? Kategorier { get; init; }

    /// <summary>Budsjettår som skal vises. Tom/Null = alle.</summary>
    public IReadOnlyCollection<int>? Budsjettaar { get; init; }

    /// <summary>Når sann, vises også historikk (frister t.o.m. fristdagen flyttes ellers ut dato+1).</summary>
    public bool InkluderHistorikk { get; init; }

    /// <summary>Nedre grense (inklusiv) på sorteringsdag. Null = ingen nedre grense. Brukes av periodevalg (utskrift).</summary>
    public DateOnly? FraDato { get; init; }

    /// <summary>Øvre grense (inklusiv) på sorteringsdag. Null = ingen øvre grense. Brukes av periodevalg (utskrift).</summary>
    public DateOnly? TilDato { get; init; }

    /// <summary>Standardfilter: budsjett + gulbok på, regnskap av (kravdok. 3.2).</summary>
    public static IReadOnlyCollection<Kategori> StandardKategorier { get; } =
        [Kategori.Budsjett, Kategori.Gulbok];
}
