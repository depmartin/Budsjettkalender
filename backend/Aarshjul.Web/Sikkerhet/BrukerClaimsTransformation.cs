using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Microsoft.AspNetCore.Authentication;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>
/// Beriker den innloggede principalen med funksjonsrolle og synlighetsgrupper fra databasen,
/// slik at autorisasjonspolicyer og synlighetskontekst kan bygges fra claims. Kjøres per
/// forespørsel; idempotent innenfor en forespørsel.
/// </summary>
public class BrukerClaimsTransformation(IBrukeroppslag oppslag) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        // Sikkerhet: fjern ALLE innkommende rolle-/gruppeclaims før berikelse, slik at et
        // token som (i et fremmed/gjeste-tenant) inneholder en forfalsket «aarshjul:rolle»
        // eller «aarshjul:gruppe» aldri kan gi admin- eller gruppetilgang. Disse claimene
        // settes utelukkende fra databasen her.
        FjernEksisterende(principal);

        var bruker = await oppslag.HentEllerOpprettAsync(principal);

        var identitet = new ClaimsIdentity();
        identitet.AddClaim(new Claim(Brukerclaims.Rolle, bruker.Funksjonsrolle.ToString()));
        foreach (var gruppe in bruker.Grupper)
        {
            identitet.AddClaim(new Claim(Brukerclaims.Gruppe, gruppe));
        }

        principal.AddIdentity(identitet);
        return principal;
    }

    private static void FjernEksisterende(ClaimsPrincipal principal)
    {
        foreach (var identitet in principal.Identities)
        {
            var berikede = identitet.FindAll(Brukerclaims.Rolle)
                .Concat(identitet.FindAll(Brukerclaims.Gruppe))
                .ToList();
            foreach (var claim in berikede)
            {
                identitet.RemoveClaim(claim);
            }
        }
    }
}
