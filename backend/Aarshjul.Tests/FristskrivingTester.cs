using Aarshjul.Application;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>Verifiserer manuell innlegging: synlighet må velges aktivt, og POL settes aldri automatisk.</summary>
public class FristskrivingTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public FristskrivingTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        _t.Db.SaveChanges();
    }

    private FristskrivingTjeneste Tjeneste() => new(_t.Db);

    private static FristInndata GyldigInndata(params string[] koder) => new()
    {
        Tittel = "Rammefordeling",
        Dato = new DateOnly(2027, 3, 15),
        Budsjettaar = 2028,
        Kategori = Kategori.Budsjett,
        Synlighetskoder = koder.ToList()
    };

    [Fact]
    public async Task Lagring_uten_synlighet_avvises()
    {
        var inndata = GyldigInndata(); // ingen grupper valgt

        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().OpprettAsync(inndata));
    }

    [Fact]
    public async Task Oppretter_frist_med_valgt_synlighet()
    {
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "FAG"));

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(FristStatus.Godkjent, frist.Status);
        Assert.Equal(Opphav.Admin, frist.Opphav);
        Assert.Equal(["FA", "FAG"], frist.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));
    }

    [Fact]
    public async Task Pol_lagres_kun_nar_den_er_eksplisitt_valgt()
    {
        var utenPol = await Tjeneste().OpprettAsync(GyldigInndata("FA"));
        var medPol = await Tjeneste().OpprettAsync(GyldigInndata("FA", "POL"));

        var f1 = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == utenPol);
        var f2 = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == medPol);

        Assert.DoesNotContain("POL", f1.Synlighet.Select(s => s.GruppeKode));
        Assert.Contains("POL", f2.Synlighet.Select(s => s.GruppeKode));
    }

    [Fact]
    public async Task Ukjent_gruppekode_avvises()
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().OpprettAsync(GyldigInndata("FINNES-IKKE")));
    }

    [Fact]
    public async Task Oppdatering_som_beholder_en_eksisterende_gruppe_kaster_ikke()
    {
        // Vanligste redigering: behold FA, bytt FAG -> POL. Tidligere «clear + re-add» kastet her.
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "FAG"));

        await Tjeneste().OppdaterAsync(id, GyldigInndata("FA", "POL"));

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(["FA", "POL"], frist.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));
    }

    [Fact]
    public async Task Tilgjengelige_budsjettaar_paavirkes_ikke_av_aarsfilter()
    {
        await Tjeneste().OpprettAsync(MedAar(2027, "FA"));
        await Tjeneste().OpprettAsync(MedAar(2028, "FA"));

        var lesing = new FristTjeneste(_t.Db, new FastKlokke(new DateOnly(2026, 1, 1)));
        var aar = await lesing.HentTilgjengeligeBudsjettaarAsync(new Synlighetskontekst(false, ["FA"]), inkluderHistorikk: true);

        Assert.Equal([2027, 2028], aar);
    }

    private static FristInndata MedAar(int aar, params string[] koder)
    {
        var inndata = GyldigInndata(koder);
        inndata.Budsjettaar = aar;
        return inndata;
    }

    [Fact]
    public async Task Oppdatering_erstatter_synlighet_og_beregner_sorteringsdag()
    {
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "POL"));

        var endret = GyldigInndata("FAG");
        endret.Datopresisjon = Datopresisjon.Maaned;
        endret.Datokvalifikator = Datokvalifikator.Ultimo;
        endret.Dato = new DateOnly(2027, 2, 10);
        await Tjeneste().OppdaterAsync(id, endret);

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(["FAG"], frist.Synlighet.Select(s => s.GruppeKode));
        Assert.Equal(new DateOnly(2027, 2, 28), frist.Sorteringsdag);
    }

    public void Dispose() => _t.Dispose();
}
