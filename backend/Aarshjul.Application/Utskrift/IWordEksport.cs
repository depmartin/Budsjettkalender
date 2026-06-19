using Aarshjul.Application.Frister;

namespace Aarshjul.Application.Utskrift;

/// <summary>
/// Utvalgskriteriet for en Word-utskrift: hvilken gruppe (eller «alt» = administrators fulle
/// innsyn) og hvilken periode. Brukes både til å bygge toppteksten og som synlig dokumentasjon av
/// hva utskriften faktisk inneholder, slik at den ikke kan forveksles med en annen gruppes utvalg.
/// </summary>
public sealed record Utskriftsforesporsel(
    string? GruppeKode,
    string GruppeEtikett,
    DateOnly? FraDato,
    DateOnly? TilDato,
    bool ErAlt);

/// <summary>
/// Genererer et Word-dokument (.docx) i FINs notatmal fra et ferdig filtrert sett frister
/// (kravdok. kap. 8, SYSTEMARKITEKTUR kap. 9). Selve synlighetsfiltreringen skjer i leseporten før
/// dette kalles — her bygges kun dokumentet. Implementeres i infrastrukturlaget (Open XML SDK).
/// </summary>
public interface IWordEksport
{
    /// <summary>Bygger .docx-en i minnet og returnerer innholdet som bytes.</summary>
    byte[] GenererFristdokument(Utskriftsforesporsel foresporsel, IReadOnlyList<FristDto> frister);
}
