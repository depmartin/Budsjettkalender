using Aarshjul.Application;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Kalender;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer forvaltningen av kalender-abonnementslenker (Endring 1): opprettelse gir et token
/// som kan slås opp, en avskrudd lenke slås ikke opp, ukjent token gir null, og ugyldig gruppe avvises.
/// </summary>
public class KalenderabonnementTester
{
    private static KalenderabonnementTjeneste Tjeneste(Testdatabase tdb) => new(tdb.Db, TimeProvider.System);

    private static async Task SeedGruppe(Testdatabase tdb, string kode, string navn)
    {
        tdb.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, Aktiv = true, ErStandard = true });
        await tdb.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Opprett_gir_token_som_kan_slaas_opp()
    {
        using var tdb = new Testdatabase();
        await SeedGruppe(tdb, "POL", "Politisk ledelse");
        var t = Tjeneste(tdb);

        var a = await t.OpprettAsync("POL", "admin");

        Assert.False(string.IsNullOrWhiteSpace(a.Token));
        Assert.Equal("POL", a.GruppeKode);
        Assert.True(a.Aktiv);

        var utvalg = await t.HentAktivtUtvalgAsync(a.Token);
        Assert.NotNull(utvalg);
        Assert.Equal("POL", utvalg!.GruppeKode);
    }

    [Fact]
    public async Task Alt_lenke_har_ingen_gruppe()
    {
        using var tdb = new Testdatabase();
        var a = await Tjeneste(tdb).OpprettAsync(null, "admin");
        Assert.Null(a.GruppeKode);
        Assert.True(a.ErAlt);
    }

    [Fact]
    public async Task Avskrudd_lenke_slaas_ikke_opp()
    {
        using var tdb = new Testdatabase();
        var t = Tjeneste(tdb);
        var a = await t.OpprettAsync(null, "admin");

        await t.SettAktivAsync(a.Id, false);

        Assert.Null(await t.HentAktivtUtvalgAsync(a.Token));
    }

    [Fact]
    public async Task Ukjent_token_gir_null()
    {
        using var tdb = new Testdatabase();
        Assert.Null(await Tjeneste(tdb).HentAktivtUtvalgAsync("finnes-ikke"));
    }

    [Fact]
    public async Task Ugyldig_gruppe_avvises()
    {
        using var tdb = new Testdatabase();
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste(tdb).OpprettAsync("FINNES-IKKE", "admin"));
    }
}
