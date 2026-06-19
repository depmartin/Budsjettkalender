using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Godkjenningsko;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Genereringstjenesten (Fase 3 Steg C/D/E) mot SQLite: persistering, synlighet-videreføring, adskilt fra køen.</summary>
public class GenereringsTjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public GenereringsTjenesteTester()
    {
        foreach (var (kode, navn) in Aarshjul.Infrastructure.Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        _t.Db.SaveChanges();
    }

    private GenereringsTjeneste Tjeneste(params string[] standardKoder)
    {
        var opsjoner = Options.Create(new SynlighetsregelOpsjoner
        {
            StandardKoder = standardKoder.Length > 0 ? standardKoder.ToList() : ["FA", "FIN-FAG"]
        });
        return new GenereringsTjeneste(_t.Db, new Synlighetsregel(opsjoner));
    }

    private async Task LeggRegel(string loep, string parametre, bool valgaarssensitiv = false)
    {
        _t.Db.Gjentaksregler.Add(new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = loep,
            Tittel = $"Tittel {loep}",
            Kategori = Kategori.Budsjett,
            Regeltype = Regeltype.FastDato,
            Regelparametre = parametre,
            Valgaarssensitiv = valgaarssensitiv
        });
        await _t.Db.SaveChangesAsync();
    }

    private async Task LeggGodkjentFjoraarsfrist(string loep, int budsjettaar, DateOnly dato, params string[] grupper)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = $"Fjor {loep}",
            Dato = dato,
            Budsjettaar = budsjettaar,
            Kategori = Kategori.Budsjett,
            Loep = loep,
            Status = FristStatus.Godkjent
        };
        foreach (var g in grupper)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        }
        _t.Db.Frister.Add(frist);
        await _t.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Generering_lager_forslag_av_type_generert()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        var resultat = await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(1, resultat.AntallForslag);
        var forslag = await _t.Db.Forslag.SingleAsync();
        Assert.Equal(ForslagType.Generert, forslag.ForslagType);
        Assert.Equal(FristStatus.Forslag, forslag.Status);
        Assert.Equal(2030, forslag.Budsjettaar);
    }

    [Fact]
    public async Task Synlighet_viderefoeres_fra_godkjent_fjoraarsfrist_inkludert_pol_som_forslag()
    {
        await LeggRegel("gulbok", "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}");
        await LeggGodkjentFjoraarsfrist("gulbok", 2029, new DateOnly(2029, 9, 15), "FA", "POL");

        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));
        Assert.Contains("FA", dto.ForeslaattSynlighet);
        // POL bæres med som forslag (krever aktiv bekreftelse ved godkjenning, settes aldri stilltiende).
        Assert.Contains("POL", dto.ForeslaattSynlighet);
        Assert.Equal(new DateOnly(2029, 9, 15), dto.FjoraarDato);
    }

    [Fact]
    public async Task Uten_fjoraarsfrist_brukes_synlighetsregelens_default_uten_pol()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        await Tjeneste("FA", "FIN-FAG", "POL").GenererAsync(new GenereringsForespoersel(2030));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));
        Assert.Equal(["FA", "FIN-FAG"], dto.ForeslaattSynlighet.OrderBy(k => k).ToArray());
        Assert.DoesNotContain("POL", dto.ForeslaattSynlighet);
    }

    [Fact]
    public async Task Regenerering_gir_ikke_dubletter()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        var t = Tjeneste();

        await t.GenererAsync(new GenereringsForespoersel(2030));
        await t.GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(1, await _t.Db.Forslag.CountAsync(f => f.Budsjettaar == 2030));
    }

    [Fact]
    public async Task Genererte_forslag_vises_ikke_i_den_loepende_koeen()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        var ko = await new GodkjenningskoTjeneste(_t.Db).HentKoAsync();

        Assert.Empty(ko);
    }

    [Fact]
    public async Task Valgaarssensitiv_frist_i_stortingsvalgaar_blir_tentativ_paa_flaten()
    {
        // Budsjettår 2025: gulbok faller samme år (forskyvning 0) = stortingsvalgår.
        await LeggRegel("gulbok", "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        await Tjeneste().GenererAsync(new GenereringsForespoersel(2025));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2025));
        Assert.True(dto.Tentativ);
        Assert.True(dto.Valgaarsflagg);
        Assert.Equal(Datopresisjon.Maaned, dto.Datopresisjon);
    }

    [Fact]
    public async Task Uopploeselig_anker_rapporteres_som_feil_ikke_som_forslag()
    {
        _t.Db.Gjentaksregler.Add(new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = "rammenotat",
            Tittel = "Rammenotat",
            Kategori = Kategori.Budsjett,
            Regeltype = Regeltype.RelativTilMilepael,
            Regelparametre = "{\"anker_loep\":\"finnesikke\",\"offset_dager\":-7}"
        });
        await _t.Db.SaveChangesAsync();

        var resultat = await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(0, resultat.AntallForslag);
        Assert.NotEmpty(resultat.Feil);
        Assert.Empty(await _t.Db.Forslag.ToListAsync());
    }

    [Fact]
    public async Task Godkjenning_av_generert_forslag_publiserer_frist_med_synlighet()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));
        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));

        var ko = new GodkjenningskoTjeneste(_t.Db);
        var fristId = await ko.GodkjennAsync(new GodkjennInndata
        {
            ForslagId = dto.ForslagId,
            Synlighetskoder = ["FA"],
            PolBekreftet = false
        });

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == fristId);
        Assert.Equal(FristStatus.Godkjent, frist.Status);
        Assert.Equal("marskonferanse", frist.Loep);
        Assert.Contains(frist.Synlighet, s => s.GruppeKode == "FA");
    }

    [Fact]
    public async Task Godkjenning_av_generert_forslag_med_pol_krever_aktiv_bekreftelse()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));
        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));

        var ko = new GodkjenningskoTjeneste(_t.Db);
        await Assert.ThrowsAsync<Aarshjul.Application.Valideringsfeil>(() => ko.GodkjennAsync(new GodkjennInndata
        {
            ForslagId = dto.ForslagId,
            Synlighetskoder = ["FA", "POL"],
            PolBekreftet = false
        }));
    }

    public void Dispose() => _t.Dispose();
}
