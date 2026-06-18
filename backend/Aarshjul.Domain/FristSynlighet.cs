namespace Aarshjul.Domain;

/// <summary>
/// Koblingstabell frist ↔ gruppekode (SYSTEMARKITEKTUR 3.1, arkitektur.md). Modellerer
/// <c>frist.synlig_for</c> relasjonelt slik at server-side synlighetsfiltrering blir en
/// indeksert join. En frist er synlig for en gruppe hvis det finnes en rad her.
/// </summary>
public class FristSynlighet
{
    public Guid FristId { get; set; }
    public Frist? Frist { get; set; }

    /// <summary>Refererer <see cref="Synlighetsgruppe.Kode"/> (stabil nøkkel).</summary>
    public required string GruppeKode { get; set; }
    public Synlighetsgruppe? Gruppe { get; set; }
}
