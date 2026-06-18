using Aarshjul.Domain;

namespace Aarshjul.Application.Frister;

/// <summary>
/// Leseprojeksjon av en frist for visning/API. Inneholder bare det en bruker som ser fristen
/// har rett til. <see cref="Synlighet"/> (gruppemerkingen) vises i grensesnittet kun for
/// administrator (BRUKERHISTORIER 4.8), men er fristens egne koder — ikke andre fristers.
/// </summary>
public record FristDto(
    Guid Id,
    string Tittel,
    DateOnly Dato,
    Datopresisjon Datopresisjon,
    Datokvalifikator? Datokvalifikator,
    DateOnly Sorteringsdag,
    int Budsjettaar,
    Kategori Kategori,
    string? Loep,
    string? Kilde,
    Guid? DokumentId,
    string? Notat,
    FristStatus Status,
    IReadOnlyList<string> Synlighet)
{
    /// <summary>Sant når fristen ikke har en fastsatt dag (tentativ) og må merkes som det.</summary>
    public bool ErTentativ => Datopresisjon != Datopresisjon.Dag;
}
