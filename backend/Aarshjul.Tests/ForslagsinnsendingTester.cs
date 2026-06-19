using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukerforslag;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer bidragsyterens forslagsinnsending (Fase 2, Steg H): forslag havner i køen med
/// opphav bruker og innsenderens id, «mine forslag» viser status, og avviste forslag kan
/// redigeres og sendes inn på nytt (kravdok. 5.3, BRUKERHISTORIER 3.1–3.5).
/// </summary>
public class ForslagsinnsendingTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly GjeldendeBruker Innsender =
        new("bruker-1", "Kari Nordmann", Funksjonsrolle.Bidragsyter, ["FA"]);

    public ForslagsinnsendingTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();
    }

    private ForslagsinnsendingTjeneste Tjeneste() => new(_t.Db);

    private static BrukerforslagInndata Inndata(params string[] synlighet) => new()
    {
        Tittel = "Frist jeg kjenner til",
        Dato = new DateOnly(2027, 5, 2),
        Budsjettaar = 2028,
        Kategori = Kategori.Budsjett,
        ForeslaattSynlighet = synlighet.ToList()
    };

    [Fact]
    public async Task Innsendt_forslag_havner_i_koen_med_opphav_bruker_og_innsenderid()
    {
        var id = await Tjeneste().SendInnAsync(Inndata("FA"), Innsender);

        var forslag = await _t.Db.Forslag.SingleAsync(f => f.Id == id);
        Assert.Equal(Opphav.Bruker, forslag.Opphav);
        Assert.Equal("bruker-1", forslag.KildeEllerInnsender);
        Assert.Equal(FristStatus.Forslag, forslag.Status);
        Assert.Equal(ForslagType.NyFrist, forslag.ForslagType);
    }

    [Fact]
    public async Task Tom_tittel_avvises()
    {
        var inndata = Inndata("FA");
        inndata.Tittel = "  ";
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().SendInnAsync(inndata, Innsender));
    }

    [Fact]
    public async Task Bruker_kan_foreslaa_pol_som_forslag()
    {
        // POL er tillatt som forslag fra bruker; admin må uansett bekrefte aktivt ved godkjenning.
        var id = await Tjeneste().SendInnAsync(Inndata("FA", "POL"), Innsender);
        var mine = await Tjeneste().HentMineAsync("bruker-1");
        Assert.Contains("POL", mine.Single(m => m.Id == id).ForeslaattSynlighet);
    }

    [Fact]
    public async Task Ukjent_gruppe_avvises()
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().SendInnAsync(Inndata("FINNES-IKKE"), Innsender));
    }

    [Fact]
    public async Task Mine_forslag_viser_bare_egne_og_med_status()
    {
        await Tjeneste().SendInnAsync(Inndata("FA"), Innsender);
        await Tjeneste().SendInnAsync(Inndata("FA"), new GjeldendeBruker("annen", "Ola", Funksjonsrolle.Bidragsyter, ["FA"]));

        var mine = await Tjeneste().HentMineAsync("bruker-1");
        Assert.Single(mine);
        Assert.Equal(FristStatus.Forslag, mine[0].Status);
    }

    [Fact]
    public async Task Avvist_forslag_kan_redigeres_og_sendes_inn_paa_nytt()
    {
        var id = await Tjeneste().SendInnAsync(Inndata("FA"), Innsender);
        _t.Db.Forslag.Single(f => f.Id == id).Status = FristStatus.Avvist;
        _t.Db.SaveChanges();

        var redigering = await Tjeneste().HentForRedigeringAsync(id, "bruker-1");
        Assert.NotNull(redigering);
        redigering!.Tittel = "Justert tittel";

        await Tjeneste().SendInnPaaNyttAsync(id, redigering, Innsender);

        var forslag = await _t.Db.Forslag.SingleAsync(f => f.Id == id);
        Assert.Equal(FristStatus.Forslag, forslag.Status);
        Assert.Equal("Justert tittel", forslag.Tittel);
    }

    [Fact]
    public async Task Kan_ikke_sende_inn_andres_forslag_paa_nytt()
    {
        var id = await Tjeneste().SendInnAsync(Inndata("FA"), Innsender);
        _t.Db.Forslag.Single(f => f.Id == id).Status = FristStatus.Avvist;
        _t.Db.SaveChanges();

        var annen = new GjeldendeBruker("inntrenger", "X", Funksjonsrolle.Bidragsyter, ["FA"]);
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().SendInnPaaNyttAsync(id, Inndata("FA"), annen));
    }

    [Fact]
    public async Task Endringsforslag_krever_eksisterende_godkjent_frist()
    {
        var inndata = Inndata("FA");
        inndata.EndrerFristId = Guid.NewGuid(); // finnes ikke
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().SendInnAsync(inndata, Innsender));

        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = "Publisert", Dato = new DateOnly(2027, 5, 2),
            Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, Opphav = Opphav.Admin
        };
        _t.Db.Frister.Add(frist);
        _t.Db.SaveChanges();

        inndata.EndrerFristId = frist.Id;
        var id = await Tjeneste().SendInnAsync(inndata, Innsender);
        Assert.Equal(ForslagType.Endring, (await _t.Db.Forslag.SingleAsync(f => f.Id == id)).ForslagType);
    }

    public void Dispose() => _t.Dispose();
}
