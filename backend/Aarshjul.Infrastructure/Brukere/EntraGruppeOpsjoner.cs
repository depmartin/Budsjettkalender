namespace Aarshjul.Infrastructure.Brukere;

/// <summary>
/// Konfigurerbar mapping fra Entra-attributter til synlighetsgrupper (kravdok. 2.3, 12).
/// Den konkrete mappingen avhenger av katalogen og avklares med IT; her er den ren
/// konfigurasjon slik at den kan endres uten kodeendring. <c>POL</c> mappes aldri automatisk.
/// </summary>
public class EntraGruppeOpsjoner
{
    public const string Seksjon = "EntraGrupper";

    /// <summary>Claim-typen Entra-attributtene leses fra (f.eks. "groups", "department", "roles").</summary>
    public string KildeClaimType { get; set; } = "groups";

    /// <summary>Mapping fra attributtverdi → gruppekode. POL ignoreres her uansett.</summary>
    public Dictionary<string, string> AttributtTilGruppe { get; set; } = new();
}
