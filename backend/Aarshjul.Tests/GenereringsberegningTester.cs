using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Ren genereringsberegning (Fase 3 Steg C): anker-kjeder, sirkularitet, tentativ-arv, valgår.</summary>
public class GenereringsberegningTester
{
    private static Gjentaksregel Regel(string loep, Regeltype type, string parametre, bool valgaarssensitiv = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Loep = loep,
            Tittel = loep,
            Kategori = Kategori.Budsjett,
            Regeltype = type,
            Regelparametre = parametre,
            Valgaarssensitiv = valgaarssensitiv
        };

    [Fact]
    public void FastDato_beregnes_i_riktig_kalenderaar_med_forskyvning()
    {
        var regel = Regel("marskonferanse", Regeltype.FastDato, "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        var r = Assert.Single(Genereringsberegning.Beregn(2030, [regel]));

        Assert.False(r.ErFeil);
        Assert.Equal(2029, r.Dato!.Value.Year); // 2030 + (-1)
        Assert.Equal(3, r.Dato.Value.Month);
        Assert.False(r.Tentativ);
    }

    [Fact]
    public void RelativTilMilepael_henger_paa_ankerets_dato()
    {
        var anker = Regel("fremleggelse", Regeltype.FastDato, "{\"maaned\":10,\"dag\":6,\"aar_forskyvning\":-1}");
        var avhengig = Regel("rammenotat", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"fremleggelse\",\"offset_dager\":-7}");

        var resultat = Genereringsberegning.Beregn(2030, [anker, avhengig]);
        var ankerR = resultat.Single(r => r.Regel.Loep == "fremleggelse");
        var avhR = resultat.Single(r => r.Regel.Loep == "rammenotat");

        Assert.Equal(ankerR.Dato!.Value.AddDays(-7), avhR.Dato!.Value);
        Assert.False(avhR.Tentativ);
    }

    [Fact]
    public void Sirkulaer_ankerkjede_feiler_tydelig()
    {
        var a = Regel("a", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"b\",\"offset_dager\":1}");
        var b = Regel("b", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"a\",\"offset_dager\":1}");

        var resultat = Genereringsberegning.Beregn(2030, [a, b]);

        Assert.All(resultat, r => Assert.True(r.ErFeil));
        Assert.Contains(resultat, r => r.Feil!.Contains("Sirkulær", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Manglende_anker_feiler_uten_aa_gjette()
    {
        var avhengig = Regel("rammenotat", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"finnesikke\",\"offset_dager\":-7}");

        var r = Assert.Single(Genereringsberegning.Beregn(2030, [avhengig]));

        Assert.True(r.ErFeil);
        Assert.Null(r.Dato);
        Assert.Contains("Mangler anker", r.Feil!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Valgaarssensitiv_i_stortingsvalgaar_blir_tentativ_uten_gjettet_dag()
    {
        // 2025 er stortingsvalgår; frist faller samme år (forskyvning 0).
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        var r = Assert.Single(Genereringsberegning.Beregn(2025, [regel]));

        Assert.True(r.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, r.Presisjon);
        Assert.True(r.Valgaarsflagg);
    }

    [Fact]
    public void Valgaarssensitiv_i_kommunevalgaar_beholder_konkret_dato_med_mildt_flagg()
    {
        // 2027 er kommunevalgår.
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        var r = Assert.Single(Genereringsberegning.Beregn(2027, [regel]));

        Assert.False(r.Tentativ);
        Assert.Equal(Datopresisjon.Dag, r.Presisjon);
        Assert.True(r.Valgaarsflagg);
    }

    [Fact]
    public void Tentativitet_arves_nedover_ankerkjeden()
    {
        // Tentativt valgårssensitivt anker i stortingsvalgår; avhengig frist er ikke selv valgårssensitiv.
        var anker = Regel("haustkonferanse", Regeltype.FastDato, "{\"maaned\":8,\"dag\":20,\"aar_forskyvning\":0}", valgaarssensitiv: true);
        var avhengig = Regel("gulbok", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"haustkonferanse\",\"offset_dager\":21}");

        var resultat = Genereringsberegning.Beregn(2025, [anker, avhengig]);
        var avhR = resultat.Single(r => r.Regel.Loep == "gulbok");

        Assert.True(avhR.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, avhR.Presisjon);
    }

    [Fact]
    public void Manuelt_satt_anker_overstyrer_tentativ_beregning()
    {
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);
        var manuell = new Dictionary<string, DateOnly> { ["gulbok"] = new(2025, 9, 22) };

        var r = Assert.Single(Genereringsberegning.Beregn(2025, [regel], manuell));

        Assert.Equal(new DateOnly(2025, 9, 22), r.Dato);
        Assert.False(r.Tentativ);
    }
}
