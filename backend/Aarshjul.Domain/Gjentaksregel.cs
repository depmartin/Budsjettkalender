namespace Aarshjul.Domain;

/// <summary>
/// Årsmal-regel (kravdok. 3.3). Malen lagrer regler, ikke datoer; ved generering beregnes
/// konkrete datoer fra reglene. Tas i full bruk i Fase 3.
/// </summary>
public class Gjentaksregel
{
    public Guid Id { get; set; }

    /// <summary>Funksjonsnavn, f.eks. "rammefordeling", "marskonferanse".</summary>
    public required string Loep { get; set; }

    /// <summary>Maltittel som blir fristens tittel ved generering (kan justeres per generert frist).</summary>
    public string Tittel { get; set; } = "";

    public Kategori Kategori { get; set; }

    public Regeltype Regeltype { get; set; }

    /// <summary>Regelparametre som JSON (avhenger av <see cref="Regeltype"/>).</summary>
    public string Regelparametre { get; set; } = "{}";

    /// <summary>Om høstløpet påvirkes av valg (valgårslogikk, kravdok. 6).</summary>
    public bool Valgaarssensitiv { get; set; }
}
