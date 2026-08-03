using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Autorisasjonspolicyer for funksjonsrollene (kravdok. 2.2).</summary>
public static class Autorisasjon
{
    public const string ErAdministrator = "ErAdministrator";
    public const string KanForeslaa = "KanForeslå";

    /// <summary>Kun bidragsyter (ikke administrator, ikke leser). Forslag-/varsel-løpet er
    /// bidragsyterens; administrator redigerer frister direkte og trenger det ikke (endring #1).</summary>
    public const string ErBidragsyter = "ErBidragsyter";

    /// <summary>Tilgang til den SBR-interne internkalenderen. I v1 er SBR = administrator (admin gis
    /// automatisk via Entra-gruppen SBR). Egen policy slik at SBR senere kan skilles fra admin uten
    /// å endre hver flate. All internkalender-data er SBR-intern og sendes aldri til en ikke-SBR-klient.</summary>
    public const string ErSbr = "ErSbr";

    public static void LeggTilPolicyer(AuthorizationOptions o)
    {
        o.AddPolicy(ErAdministrator, p => p.RequireClaim(
            Brukerclaims.Rolle, nameof(Funksjonsrolle.Administrator)));

        // Administrator og bidragsyter kan sende forslag; leser kan ikke.
        o.AddPolicy(KanForeslaa, p => p.RequireClaim(
            Brukerclaims.Rolle,
            nameof(Funksjonsrolle.Administrator),
            nameof(Funksjonsrolle.Bidragsyter)));

        // Forslag-/varsel-flatene: kun bidragsyter. Administrator gjør endringer direkte
        // (publiseres med én gang) og har ikke forslag/varsler i grensesnittet.
        o.AddPolicy(ErBidragsyter, p => p.RequireClaim(
            Brukerclaims.Rolle, nameof(Funksjonsrolle.Bidragsyter)));

        // Internkalenderen: SBR-intern. I v1 sammenfaller SBR med administrator.
        o.AddPolicy(ErSbr, p => p.RequireClaim(
            Brukerclaims.Rolle, nameof(Funksjonsrolle.Administrator)));
    }
}
