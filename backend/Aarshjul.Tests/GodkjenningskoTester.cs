using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Godkjenningsko;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer godkjenningskøen (Fase 2, Steg F): godkjenning publiserer via det
/// synlighetsvaliderte mønsteret (POL kun ved aktiv bekreftelse), avvisning bevarer forslaget og
/// varsler innsender, og endringsforslag oppdaterer den berørte fristen.
/// </summary>
public class GodkjenningskoTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public GodkjenningskoTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();
    }

    private GodkjenningskoTjeneste Tjeneste() => new(_t.Db);

    private Guid LeggForslag(Opphav opphav = Opphav.Robot, string? kilde = "R-4/2027", string? loep = "rammefordeling",
        DateOnly? dato = null, string synlighetJson = "[\"FA\"]", ForslagType type = ForslagType.NyFrist, Guid? endrerFristId = null)
    {
        var f = new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = type,
            Opphav = opphav,
            KildeEllerInnsender = kilde,
            Tittel = "Hovedbudsjettskriv",
            Dato = dato ?? new DateOnly(2027, 3, 15),
            Budsjettaar = 2028,
            Kategori = Kategori.Budsjett,
            Loep = loep,
            ForeslaattSynlighet = synlighetJson,
            DokumentId = Guid.NewGuid(),
            EndrerFristId = endrerFristId,
            Status = FristStatus.Forslag
        };
        _t.Db.Forslag.Add(f);
        _t.Db.SaveChanges();
        return f.Id;
    }

    [Fact]
    public async Task Godkjenn_publiserer_frist_med_synlighet_og_bevarer_opphav()
    {
        var id = LeggForslag(opphav: Opphav.Robot, kilde: "R-4/2027");

        var fristId = await Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = id, Synlighetskoder = ["FA", "FIN-FAG"] });

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == fristId);
        Assert.Equal(FristStatus.Godkjent, frist.Status);
        Assert.Equal(Opphav.Robot, frist.Opphav);
        Assert.Equal("R-4/2027", frist.Kilde);
        Assert.NotNull(frist.DokumentId);
        Assert.Equal(["FA", "FIN-FAG"], frist.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));

        var forslag = await _t.Db.Forslag.SingleAsync(f => f.Id == id);
        Assert.Equal(FristStatus.Godkjent, forslag.Status);
    }

    [Fact]
    public async Task Pol_krever_aktiv_bekreftelse()
    {
        var id = LeggForslag();

        await Assert.ThrowsAsync<Valideringsfeil>(() =>
            Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = id, Synlighetskoder = ["FA", "POL"], PolBekreftet = false }));

        var fristId = await Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = id, Synlighetskoder = ["FA", "POL"], PolBekreftet = true });
        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == fristId);
        Assert.Contains("POL", frist.Synlighet.Select(s => s.GruppeKode));
    }

    [Fact]
    public async Task Godkjenn_uten_synlighet_avvises()
    {
        var id = LeggForslag();
        await Assert.ThrowsAsync<Valideringsfeil>(() =>
            Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = id, Synlighetskoder = [] }));
    }

    [Fact]
    public async Task Godkjenn_uten_dato_avvises()
    {
        var f = new Forslag
        {
            Id = Guid.NewGuid(), Opphav = Opphav.Robot, Tittel = "Uten dato", Dato = null,
            Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Forslag
        };
        _t.Db.Forslag.Add(f);
        _t.Db.SaveChanges();

        await Assert.ThrowsAsync<Valideringsfeil>(() =>
            Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = f.Id, Synlighetskoder = ["FA"] }));
    }

    [Fact]
    public async Task Avvis_bevarer_forslag_og_varsler_brukerforslag()
    {
        var id = LeggForslag(opphav: Opphav.Bruker, kilde: "bruker-123");

        await Tjeneste().AvvisAsync(id, "Dublett av eksisterende frist.");

        var forslag = await _t.Db.Forslag.SingleAsync(f => f.Id == id);
        Assert.Equal(FristStatus.Avvist, forslag.Status);

        var varsel = await _t.Db.Varsler.SingleAsync(v => v.BrukerId == "bruker-123");
        Assert.False(varsel.Lest);
        Assert.Equal("Dublett av eksisterende frist.", varsel.Begrunnelse);
    }

    [Fact]
    public async Task Avvis_robotforslag_gir_ikke_varsel()
    {
        var id = LeggForslag(opphav: Opphav.Robot);
        await Tjeneste().AvvisAsync(id);
        Assert.False(await _t.Db.Varsler.AnyAsync());
    }

    [Fact]
    public async Task Godkjent_brukerforslag_varsler_innsender()
    {
        var id = LeggForslag(opphav: Opphav.Bruker, kilde: "bruker-9");
        await Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = id, Synlighetskoder = ["FA"] });

        var varsel = await _t.Db.Varsler.SingleAsync(v => v.BrukerId == "bruker-9");
        Assert.Contains("godkjent", varsel.Tekst.ToLowerInvariant());
    }

    [Fact]
    public async Task Endringsforslag_oppdaterer_eksisterende_frist_forst_ved_godkjenning()
    {
        // Publisert frist som skal endres.
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = "Gammel tittel", Dato = new DateOnly(2027, 3, 15),
            Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, Opphav = Opphav.Admin
        };
        frist.Synlighet.Add(new FristSynlighet { GruppeKode = "FA" });
        _t.Db.Frister.Add(frist);
        _t.Db.SaveChanges();

        var forslagId = LeggForslag(type: ForslagType.Endring, endrerFristId: frist.Id, dato: new DateOnly(2027, 4, 1));

        // Original uendret før godkjenning.
        Assert.Equal("Gammel tittel", (await _t.Db.Frister.AsNoTracking().SingleAsync(f => f.Id == frist.Id)).Tittel);

        await Tjeneste().GodkjennAsync(new GodkjennInndata { ForslagId = forslagId, Synlighetskoder = ["FA", "FIN-FAG"] });

        var oppdatert = await _t.Db.Frister.AsNoTracking().Include(f => f.Synlighet).SingleAsync(f => f.Id == frist.Id);
        Assert.Equal("Hovedbudsjettskriv", oppdatert.Tittel);
        Assert.Equal(new DateOnly(2027, 4, 1), oppdatert.Dato);
        Assert.Equal(["FA", "FIN-FAG"], oppdatert.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));
        // Ingen ny frist opprettet.
        Assert.Equal(1, await _t.Db.Frister.CountAsync());
    }

    [Fact]
    public async Task Henter_kun_apne_forslag_med_bevis_og_ukjent_type_flagg()
    {
        // Ukjent type: robot uten løp.
        var ukjentId = LeggForslag(loep: null, kilde: "R-99/2027");
        _t.Db.UttrekksBevis.Add(new UttrekksBevis { Id = Guid.NewGuid(), ForslagId = ukjentId, Felt = "dato", TolketVerdi = "uklart", Konfidens = 0.3 });
        // Et avvist forslag skal ikke dukke opp.
        var avvistId = LeggForslag();
        _t.Db.Forslag.Single(f => f.Id == avvistId).Status = FristStatus.Avvist;
        _t.Db.SaveChanges();

        var ko = await Tjeneste().HentKoAsync();
        Assert.DoesNotContain(ko, e => e.Id == avvistId);

        var ukjent = ko.Single(e => e.Id == ukjentId);
        Assert.True(ukjent.ErUkjentType);
        Assert.Single(ukjent.Bevis);

        var bareUkjent = await Tjeneste().HentKoAsync(new Kofilter { UkjentType = true });
        Assert.All(bareUkjent, e => Assert.True(e.ErUkjentType));
    }

    [Fact]
    public async Task Foreslaatt_synlighet_leses_fra_json()
    {
        var id = LeggForslag(synlighetJson: JsonSerializer.Serialize(new[] { "FA", "FIN-FAG" }));
        var element = (await Tjeneste().HentKoAsync()).Single(e => e.Id == id);
        Assert.Equal(["FA", "FIN-FAG"], element.ForeslaattSynlighet.OrderBy(k => k));
    }

    public void Dispose() => _t.Dispose();
}
