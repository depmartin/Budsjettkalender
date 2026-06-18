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

        // Allerede beriket i denne forespørselen?
        if (principal.HasClaim(c => c.Type == Brukerclaims.Rolle))
        {
            return principal;
        }

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
}
