using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer den sikkerhetskritiske server-side synlighetsfiltreringen direkte på
/// tjenestens resultat (ikke via grensesnittet) — jf. Fase 1-akseptkriteriene.
/// </summary>
public class SynlighetTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2026, 1, 1);

    public SynlighetTester() => SeedAsync().GetAwaiter().GetResult();

    private async Task SeedAsync()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe
            {
                Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true
            });
        }
        await _t.Db.SaveChangesAsync();

        LeggTilFrist("Kun FA", new[] { "FA" });
        LeggTilFrist("FA og FAG", new[] { "FA", "FAG" });
        LeggTilFrist("FA og POL", new[] { "FA", "POL" });
        LeggTilFrist("Kun FAG", new[] { "FAG" });
        await _t.Db.SaveChangesAsync();
    }

    private void LeggTilFrist(string tittel, string[] grupper)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = tittel,
            Dato = new DateOnly(2026, 6, 1),
            Budsjettaar = 2027,
            Kategori = Kategori.Budsjett,
            Status = FristStatus.Godkjent
        };
        foreach (var g in grupper)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        }
        _t.Db.Frister.Add(frist);
    }

    private FristTjeneste Tjeneste() => new(_t.Db, new FastKlokke(Idag));

    private static FristFilter AlleKategorier => new();

    [Fact]
    public async Task Fag_bruker_ser_kun_frister_merket_FAG()
    {
        var ctx = new Synlighetskontekst(serAlt: false, grupper: ["FAG"]);

        var resultat = await Tjeneste().HentSynligeAsync(ctx, AlleKategorier);

        Assert.Equal(2, resultat.Count);
        Assert.All(resultat, f => Assert.Contains("FAG", f.Synlighet));
        Assert.DoesNotContain(resultat, f => f.Tittel == "Kun FA");
        Assert.DoesNotContain(resultat, f => f.Tittel == "FA og POL");
    }

    [Fact]
    public async Task Frist_merket_FA_og_POL_er_synlig_for_FA_og_POL_men_ikke_FAG()
    {
        var fa = await Tjeneste().HentSynligeAsync(new Synlighetskontekst(false, ["FA"]), AlleKategorier);
        var pol = await Tjeneste().HentSynligeAsync(new Synlighetskontekst(false, ["POL"]), AlleKategorier);
        var fag = await Tjeneste().HentSynligeAsync(new Synlighetskontekst(false, ["FAG"]), AlleKategorier);

        Assert.Contains(fa, f => f.Tittel == "FA og POL");
        Assert.Contains(pol, f => f.Tittel == "FA og POL");
        Assert.DoesNotContain(fag, f => f.Tittel == "FA og POL");
    }

    [Fact]
    public async Task Administrator_ser_alle_frister_uavhengig_av_merking()
    {
        var admin = new Synlighetskontekst(serAlt: true, grupper: []);

        var resultat = await Tjeneste().HentSynligeAsync(admin, AlleKategorier);

        Assert.Equal(4, resultat.Count);
    }

    [Fact]
    public async Task Bruker_uten_grupper_ser_ingen_frister()
    {
        var ingen = new Synlighetskontekst(serAlt: false, grupper: []);

        var resultat = await Tjeneste().HentSynligeAsync(ingen, AlleKategorier);

        Assert.Empty(resultat);
    }

    [Fact]
    public async Task Pol_bruker_ser_ikke_FIN_interne_frister()
    {
        var pol = new Synlighetskontekst(false, ["POL"]);

        var resultat = await Tjeneste().HentSynligeAsync(pol, AlleKategorier);

        Assert.Single(resultat);
        Assert.Equal("FA og POL", resultat[0].Tittel);
    }

    public void Dispose() => _t.Dispose();
}
