using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Web.Sikkerhet;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer at claims-transformasjonen aldri stoler på innkommende rolle-/gruppeclaims:
/// et token med forfalsket «aarshjul:rolle»/«aarshjul:gruppe» skal ikke gi tilgang.
/// </summary>
public class ClaimsTransformasjonTester
{
    private sealed class FastOppslag(GjeldendeBruker bruker) : IBrukeroppslag
    {
        public Task<GjeldendeBruker> HentEllerOpprettAsync(ClaimsPrincipal principal, CancellationToken ct = default)
            => Task.FromResult(bruker);
    }

    [Fact]
    public async Task Forfalskede_rolle_og_gruppeclaims_overstyres_av_databasen()
    {
        var forfalsket = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "u1"),
                new Claim(Brukerclaims.Rolle, nameof(Funksjonsrolle.Administrator)),
                new Claim(Brukerclaims.Gruppe, "POL")
            ],
            authenticationType: "test");
        var principal = new ClaimsPrincipal(forfalsket);

        // Databasen sier: vanlig leser i FAG.
        var oppslag = new FastOppslag(new GjeldendeBruker("u1", "Test", Funksjonsrolle.Leser, ["FAG"]));

        var resultat = await new BrukerClaimsTransformation(oppslag).TransformAsync(principal);
        var ctx = Synlighetskontekst.FraPrincipal(resultat);

        Assert.False(ctx.SerAlt); // forfalsket Administrator er fjernet
        Assert.Equal(["FAG"], ctx.Grupper.OrderBy(g => g)); // forfalsket POL er borte, kun DB-gruppen står
    }
}
