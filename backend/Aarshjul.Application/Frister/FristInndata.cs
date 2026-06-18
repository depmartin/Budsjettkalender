using Aarshjul.Domain;

namespace Aarshjul.Application.Frister;

/// <summary>
/// Inndata fra redigeringsskjemaet (kravdok. 5.2). Samme skjema brukes for manuell
/// opprettelse og justering. Synlighet må velges aktivt — ingen stilltiende standard.
/// </summary>
public class FristInndata
{
    public string Tittel { get; set; } = "";
    public DateOnly Dato { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;
    public Datokvalifikator? Datokvalifikator { get; set; }
    public int Budsjettaar { get; set; } = DateTime.Today.Year + 1;
    public Kategori Kategori { get; set; } = Kategori.Budsjett;
    public string? Loep { get; set; }
    public string? Notat { get; set; }

    /// <summary>Valgte synlighetsgrupper (gruppekoder). Må inneholde minst én (POL kun ved aktivt valg).</summary>
    public List<string> Synlighetskoder { get; set; } = [];
}
