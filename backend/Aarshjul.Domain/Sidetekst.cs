namespace Aarshjul.Domain;

/// <summary>
/// En redigerbar tekst på en flate (f.eks. introteksten på en visning), identifisert med en stabil
/// nøkkel. Administrator kan endre teksten i grensesnittet; finnes ingen lagret tekst, brukes flatens
/// innebygde standardtekst. Ren innholdsredigering — ingen synlighets-/tilgangsvirkning.
/// </summary>
public class Sidetekst
{
    /// <summary>Stabil nøkkel for tekstplassen, f.eks. «aarshjul.ingress».</summary>
    public required string Nokkel { get; set; }

    /// <summary>Administrators lagrede tekst.</summary>
    public string Tekst { get; set; } = "";
}
