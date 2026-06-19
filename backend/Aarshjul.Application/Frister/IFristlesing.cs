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
    /// Én enkelt godkjent frist, men kun hvis konteksten har rett til å se den (synlighet
    /// håndheves på server). Returnerer <c>null</c> når fristen ikke finnes, ikke er godkjent,
    /// eller ikke er synlig for konteksten. Brukes bl.a. når en bidragsyter skal foreslå
    /// endring på en publisert frist.
    /// </summary>
    Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid fristId, CancellationToken ct = default);

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
