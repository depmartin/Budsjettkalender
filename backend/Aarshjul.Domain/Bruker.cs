namespace Aarshjul.Domain;

/// <summary>
/// Bruker autentisert via Entra ID (kravdok. 3.5). Funksjonsrolle og synlighetsgrupper er
/// adskilte akser. Administratorrollen settes av en annen administrator inne i appen.
/// </summary>
public class Bruker
{
    /// <summary>Stabil bruker-id fra Entra (oid/sub).</summary>
    public required string Id { get; set; }

    /// <summary>Visningsnavn fra Entra (brukes i <see cref="Frist.ForeslaattAv"/>).</summary>
    public required string Navn { get; set; }

    public Funksjonsrolle Funksjonsrolle { get; set; } = Funksjonsrolle.Leser;

    /// <summary>Om brukeren er FIN-ansatt (forutsetning for å kunne bli administrator).</summary>
    public bool ErFin { get; set; }

    /// <summary>Synlighetsgrupper, med kilde (Entra-utledet vs. manuelt satt).</summary>
    public ICollection<BrukerGruppe> Grupper { get; set; } = new List<BrukerGruppe>();
}
