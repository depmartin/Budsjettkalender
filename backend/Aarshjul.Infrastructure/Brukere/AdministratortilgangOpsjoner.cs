namespace Aarshjul.Infrastructure.Brukere;

/// <summary>
/// Konfigurerbar regel for hvem som får administratortilgang automatisk fra Entra (beslutning
/// 2026-07-30). Tanken er at alle i seksjon SBR i Finansavdelingen skal bli administrator uten
/// å måtte utpekes manuelt — det forenkler overgangen når en administrator slutter.
///
/// Verdiene er IT-avklarte og settes i konfig, ikke i kode (jf. kravdok. kap. 12): typisk
/// objekt-id-ene (GUID) til SBR-sikkerhetsgruppen i Entra. Tom liste = ingen får admin
/// automatisk (da gjelder bare seedet/manuelt satt administrator).
/// </summary>
public class AdministratortilgangOpsjoner
{
    public const string Seksjon = "Administratortilgang";

    /// <summary>Claim-typen admin-gruppetilhørigheten leses fra (typisk "groups" for Entra-sikkerhetsgrupper).</summary>
    public string KildeClaimType { get; set; } = "groups";

    /// <summary>Verdiene (f.eks. gruppe-objekt-id-er) som gir administrator automatisk. Tom = ingen auto-admin.</summary>
    public List<string> Gruppeider { get; set; } = new();
}
