using System.Security.Claims;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Brukere;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer medlemskapsstyrt administratortilgang (Endring 2, 2026-07-30): en bruker i den
/// konfigurerte admin-gruppen (SBR) blir automatisk administrator, forlater vedkommende gruppen
/// mistes rollen igjen — men en seedet/manuelt satt administrator nedgraderes aldri automatisk.
/// </summary>
public class AdminTilgangTester
{
    private const string SbrGruppe = "sbr-objekt-id";

    private static BrukeroppslagTjeneste Tjeneste(Testdatabase tdb) => new(
        tdb.Db,
        Options.Create(new EntraGruppeOpsjoner()),
        Options.Create(new AdministratortilgangOpsjoner { KildeClaimType = "groups", Gruppeider = [SbrGruppe] }));

    private static ClaimsPrincipal Principal(string id, string navn, bool iSbr)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, navn)
        };
        if (iSbr)
        {
            claims.Add(new Claim("groups", SbrGruppe));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Bruker_i_SBR_blir_administrator_automatisk()
    {
        using var tdb = new Testdatabase();

        var resultat = await Tjeneste(tdb).HentEllerOpprettAsync(Principal("u-sbr", "Sivert SBR", iSbr: true));

        Assert.Equal(Funksjonsrolle.Administrator, resultat.Funksjonsrolle);
        var lagret = await tdb.Db.Brukere.FindAsync("u-sbr");
        Assert.True(lagret!.AdminViaEntra);
        Assert.True(lagret.ErFin);
    }

    [Fact]
    public async Task Bruker_uten_SBR_blir_ikke_administrator()
    {
        using var tdb = new Testdatabase();

        var resultat = await Tjeneste(tdb).HentEllerOpprettAsync(Principal("u-vanlig", "Vanlig Bruker", iSbr: false));

        Assert.NotEqual(Funksjonsrolle.Administrator, resultat.Funksjonsrolle);
    }

    [Fact]
    public async Task Auto_admin_nedgraderes_naar_bruker_forlater_SBR()
    {
        using var tdb = new Testdatabase();
        tdb.Db.Brukere.Add(new Bruker
        {
            Id = "u-forlot", Navn = "Forlot SBR", ErFin = true,
            Funksjonsrolle = Funksjonsrolle.Administrator, AdminViaEntra = true
        });
        await tdb.Db.SaveChangesAsync();

        var resultat = await Tjeneste(tdb).HentEllerOpprettAsync(Principal("u-forlot", "Forlot SBR", iSbr: false));

        Assert.Equal(Funksjonsrolle.Bidragsyter, resultat.Funksjonsrolle); // FIN-ansatt faller til bidragsyter
        var lagret = await tdb.Db.Brukere.FindAsync("u-forlot");
        Assert.False(lagret!.AdminViaEntra);
    }

    [Fact]
    public async Task Seedet_admin_beholder_rollen_uten_SBR()
    {
        using var tdb = new Testdatabase();
        // Seedet/manuelt satt administrator: AdminViaEntra = false skal aldri nedgraderes automatisk.
        tdb.Db.Brukere.Add(new Bruker
        {
            Id = "u-seed", Navn = "Seedet Admin", ErFin = true,
            Funksjonsrolle = Funksjonsrolle.Administrator, AdminViaEntra = false
        });
        await tdb.Db.SaveChangesAsync();

        var resultat = await Tjeneste(tdb).HentEllerOpprettAsync(Principal("u-seed", "Seedet Admin", iSbr: false));

        Assert.Equal(Funksjonsrolle.Administrator, resultat.Funksjonsrolle);
        var lagret = await tdb.Db.Brukere.FindAsync("u-seed");
        Assert.False(lagret!.AdminViaEntra);
    }
}
