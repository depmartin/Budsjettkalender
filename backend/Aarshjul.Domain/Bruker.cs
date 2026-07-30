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

    /// <summary>
    /// Sant når administratorrollen ble gitt automatisk fordi brukeren tilhører en konfigurert
    /// admin-gruppe i Entra (f.eks. seksjon SBR). Da er tilgangen medlemskapsstyrt: forlater
    /// brukeren gruppen, mister vedkommende admin ved neste innlogging. En seedet/manuelt satt
    /// administrator har dette feltet falskt og nedgraderes aldri automatisk.
    /// </summary>
    public bool AdminViaEntra { get; set; }

    /// <summary>Synlighetsgrupper, med kilde (Entra-utledet vs. manuelt satt).</summary>
    public ICollection<BrukerGruppe> Grupper { get; set; } = new List<BrukerGruppe>();
}
