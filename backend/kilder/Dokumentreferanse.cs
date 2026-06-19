namespace Aarshjul.Kilder;

/// <summary>
/// En referanse til ett dokument oppdaget hos en kilde, før innholdet er hentet.
/// Feltene holdes kildeagnostiske (kravdok. 4.1) slik at flere kilder kan levere likt.
/// </summary>
public sealed record Dokumentreferanse
{
    /// <summary>
    /// Stabil, kildeintern nøkkel for dokumentet, uavhengig av rundskrivnummer
    /// (kravdok. 3.4). Brukes som <c>BehandletDokument.DokumentNokkel</c> ved dedup.
    /// </summary>
    public required string Nokkel { get; init; }

    /// <summary>Dokumentets tittel slik den står på oversikten.</summary>
    public required string Tittel { get; init; }

    /// <summary>Dato fra oversikten der den finnes.</summary>
    public DateOnly? Dato { get; init; }

    /// <summary>Lenke til selve dokumentet (PDF).</summary>
    public required string Url { get; init; }

    /// <summary>
    /// Rundskrivnummer der det lar seg lese. Kun et svakt hint til filtrering/validering,
    /// aldri nøkkel — nummeret kan skifte mellom år (kravdok. 4.3).
    /// </summary>
    public int? Nummer { get; init; }
}
