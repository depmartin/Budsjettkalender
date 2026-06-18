namespace Aarshjul.Application.Brukere;

/// <summary>
/// Claim-typer som beriker den innloggede principalen med funksjonsrolle og synlighetsgrupper
/// fra databasen (satt av en claims-transformasjon). Lar autorisasjonspolicyer og
/// synlighetskontekst bygges direkte fra claims.
/// </summary>
public static class Brukerclaims
{
    public const string Rolle = "aarshjul:rolle";
    public const string Gruppe = "aarshjul:gruppe";
}
