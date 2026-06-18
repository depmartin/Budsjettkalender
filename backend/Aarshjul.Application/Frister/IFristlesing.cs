using Aarshjul.Application.Synlighet;

namespace Aarshjul.Application.Frister;

/// <summary>
/// Leseport for frister. Alle metoder filtrerer på server mot brukerens synlighet før noe
/// returneres. Implementeres i infrastrukturlaget (mot databasen).
/// </summary>
public interface IFristlesing
{
    /// <summary>Synlige, godkjente frister som matcher filteret, sortert kronologisk.</summary>
    Task<IReadOnlyList<FristDto>> HentSynligeAsync(ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Landingsflatens utvalg (BRUKERHISTORIER 2.1): union av «innen 30 dager» og «minst de
    /// fem førstkommende», rullerende fra <paramref name="fraOgMed"/>, filtrert på synlighet.
    /// </summary>
    Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(ISynlighetskontekst ctx, DateOnly fraOgMed, FristFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Budsjettårene brukeren har synlige frister for. Uavhengig av budsjettårsvalget i
    /// filteret, slik at årsfilteret ikke kollapser når ett år er valgt.
    /// </summary>
    Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(ISynlighetskontekst ctx, bool inkluderHistorikk, CancellationToken ct = default);
}
