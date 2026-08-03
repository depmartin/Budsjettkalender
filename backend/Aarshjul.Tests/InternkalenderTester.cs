using Aarshjul.Application;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// Internkalenderen (trinn 1) mot SQLite: runder, gjøremål, tidfesting → sorteringsdag,
/// venter-på-anker, avhuking (hvem/når), personfilter og tverr-rundevisning «Mine».
/// </summary>
public class InternkalenderTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private readonly InternkalenderTjeneste _tj;

    public InternkalenderTester()
    {
        _tj = new InternkalenderTjeneste(_t.Db, new FastKlokke(new DateOnly(2026, 8, 3)));
    }

    // --- Runder ---

    [Fact]
    public async Task OpprettRunde_gir_unik_runde_og_avviser_duplikat()
    {
        await _tj.OpprettRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(
            () => _tj.OpprettRundeAsync(Rundetype.Augustrunden, 2027, "Dag"));
        Assert.Contains("finnes allerede", feil.Message);
    }

    [Fact]
    public async Task OvrigRunde_er_singleton_uten_aar()
    {
        var a = await _tj.HentEllerOpprettOvrigAsync("Dag");
        var b = await _tj.HentEllerOpprettOvrigAsync("Dag");
        Assert.Equal(a, b);
        var runde = await _tj.HentRundeAsync(a);
        Assert.Null(runde!.Aar);
        Assert.Equal(Rundetype.Ovrig, runde.Rundetype);
    }

    // --- Tidfesting → sorteringsdag ---

    [Fact]
    public async Task Hurtiglegg_lager_mangelfullt_gjoeremaal_uten_dato()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Rnb, 2027, "Dag");
        await _tj.HurtiglaggAsync(runde, "Bare en tittel");
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal("Bare en tittel", g.Tittel);
        Assert.Equal(Tidfestingstype.Ingen, g.Tidfestingstype);
        Assert.Null(g.Sorteringsdag);
    }

    [Fact]
    public async Task KonkretDato_gir_sorteringsdag_lik_datoen()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Rnb, 2027, "Dag");
        await _tj.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "X",
            Tidfestingstype = Tidfestingstype.KonkretDato,
            Dato = new DateOnly(2027, 5, 12)
        });
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(new DateOnly(2027, 5, 12), g.Sorteringsdag);
    }

    [Fact]
    public async Task TentativMaaned_medio_gir_sorteringsdag_den_15()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        await _tj.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "Gul bok",
            Tidfestingstype = Tidfestingstype.TentativMaaned,
            Dato = new DateOnly(2026, 9, 1),
            Datokvalifikator = Datokvalifikator.Medio
        });
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(new DateOnly(2026, 9, 15), g.Sorteringsdag);
        Assert.Contains("medio september 2026", g.Tidsangivelse);
    }

    [Fact]
    public async Task Rundeposisjon_gir_dato_innenfor_rundens_spenn()
    {
        // Augustrunden 2027 = juli–oktober 2026.
        var runde = await _tj.OpprettRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        await _tj.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "Tidlig",
            Tidfestingstype = Tidfestingstype.Rundeposisjon,
            Rundeposisjon = Rundeposisjon.Start
        });
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(new DateOnly(2026, 7, 1), g.Sorteringsdag);
    }

    // --- Anker ---

    [Fact]
    public async Task AnkerRelativ_oppløses_mot_publisert_frist()
    {
        _t.Db.Frister.Add(new Frist
        {
            Tittel = "Rammefordeling",
            Dato = new DateOnly(2027, 3, 20),
            Budsjettaar = 2027,
            Loep = "rammefordeling",
            Status = FristStatus.Godkjent
        });
        await _t.Db.SaveChangesAsync();

        var runde = await _tj.OpprettRundeAsync(Rundetype.Marsrunden, 2027, "Dag");
        await _tj.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "En uke etter rammefordeling",
            Tidfestingstype = Tidfestingstype.AnkerRelativ,
            AnkerLoep = "rammefordeling",
            AnkerOffsetDager = 7
        });
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.False(g.VenterPaaAnker);
        Assert.Equal(new DateOnly(2027, 3, 27), g.Sorteringsdag);
    }

    [Fact]
    public async Task AnkerRelativ_uten_frist_gir_venter_paa_anker()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Marsrunden, 2027, "Dag");
        await _tj.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "Etter FAGs frist",
            Tidfestingstype = Tidfestingstype.AnkerRelativ,
            AnkerLoep = "fagfrist",
            AnkerOffsetDager = 7
        });
        var g = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.True(g.VenterPaaAnker);
        Assert.Null(g.Sorteringsdag);
        Assert.Contains("Venter på ankerdato", g.Tidsangivelse);
    }

    // --- Avhuking ---

    [Fact]
    public async Task Fullfoer_lagrer_hvem_og_naar_og_flytter_til_ferdig_gjenaapne_reverserer()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Rnb, 2027, "Dag");
        var id = await _tj.HurtiglaggAsync(runde, "Oppgave");

        await _tj.FullfoerAsync(id, "demo-sbr", "Sivert SBR");
        var ferdig = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(GjoeremaalStatus.Fullfoert, ferdig.Status);
        Assert.Equal("Sivert SBR", ferdig.FullfoertAvNavn);
        Assert.Equal(new DateOnly(2026, 8, 3), DateOnly.FromDateTime(ferdig.FullfoertTid!.Value.UtcDateTime));

        await _tj.GjenaapneAsync(id);
        var aktiv = (await _tj.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(GjoeremaalStatus.Aktiv, aktiv.Status);
        Assert.Null(aktiv.FullfoertAvNavn);
        Assert.Null(aktiv.FullfoertTid);
    }

    // --- Personfilter + Mine på tvers av runder ---

    [Fact]
    public async Task Personfilter_viser_kun_egne_og_Mine_gaar_paa_tvers_av_runder()
    {
        var august = await _tj.OpprettRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var nys = await _tj.OpprettRundeAsync(Rundetype.Nysaldering, 2026, "Dag");

        await _tj.OpprettGjoeremaalAsync(august, Med("Min i august", "demo-sbr", "Sivert"));
        await _tj.OpprettGjoeremaalAsync(august, Med("Annen i august", "demo-admin", "Dag"));
        await _tj.OpprettGjoeremaalAsync(nys, Med("Min i nysaldering", "demo-sbr", "Sivert"));

        var filtrert = await _tj.HentGjoeremaalAsync(august, "demo-sbr");
        Assert.Single(filtrert);
        Assert.Equal("Min i august", filtrert[0].Tittel);

        var mine = await _tj.HentMineAsync("demo-sbr");
        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, m => m.RundeEtikett == "Augustrunden 2027");
        Assert.Contains(mine, m => m.RundeEtikett == "Nysalderingen 2026");
    }

    [Fact]
    public async Task Manuell_endring_av_generert_gjoeremaal_setter_ManueltEndret()
    {
        var runde = await _tj.OpprettRundeAsync(Rundetype.Rnb, 2027, "Dag");
        // Simuler et generert gjøremål ved å legge det direkte.
        var g = new InterntGjoeremaal { Id = Guid.NewGuid(), RundeId = runde, Tittel = "Generert", Opphav = GjoeremaalOpphav.Generert };
        _t.Db.InterneGjoeremaal.Add(g);
        await _t.Db.SaveChangesAsync();

        await _tj.OppdaterGjoeremaalAsync(g.Id, new GjoeremaalInndata { Tittel = "Endret" });
        var lagret = await _t.Db.InterneGjoeremaal.AsNoTracking().FirstAsync(x => x.Id == g.Id);
        Assert.True(lagret.ManueltEndret);
    }

    private static GjoeremaalInndata Med(string tittel, string brukerId, string navn) => new()
    {
        Tittel = tittel,
        Ansvarlige = [new AnsvarligDto(brukerId, navn)]
    };

    public void Dispose() => _t.Dispose();
}
