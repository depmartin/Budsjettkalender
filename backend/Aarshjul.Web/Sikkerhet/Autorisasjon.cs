using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Autorisasjonspolicyer for funksjonsrollene (kravdok. 2.2).</summary>
public static class Autorisasjon
{
    public const string ErAdministrator = "ErAdministrator";
    public const string KanForeslaa = "KanForeslå";

    public static void LeggTilPolicyer(AuthorizationOptions o)
    {
        o.AddPolicy(ErAdministrator, p => p.RequireClaim(
            Brukerclaims.Rolle, nameof(Funksjonsrolle.Administrator)));

        // Administrator og bidragsyter kan sende forslag; leser kan ikke.
        o.AddPolicy(KanForeslaa, p => p.RequireClaim(
            Brukerclaims.Rolle,
            nameof(Funksjonsrolle.Administrator),
            nameof(Funksjonsrolle.Bidragsyter)));
    }
}
