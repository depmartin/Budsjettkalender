using Aarshjul.Domain;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Domenelogikk for internkalenderens runder: månedsspenn, årsforskyvning og rundeposisjon.</summary>
public class RunderTester
{
    [Theory]
    [InlineData(Rundetype.Marsrunden, 2027, 2026, 1, 2026, 4)]   // jan–apr år t-1
    [InlineData(Rundetype.Augustrunden, 2027, 2026, 7, 2026, 10)] // jul–okt år t-1
    [InlineData(Rundetype.Rnb, 2027, 2027, 4, 2027, 5)]           // apr–mai år t
    [InlineData(Rundetype.Nysaldering, 2026, 2026, 10, 2026, 12)] // okt–des år t
    [InlineData(Rundetype.Regnskap, 2026, 2026, 1, 2026, 12)]     // hele året
    public void Spenn_gir_riktig_kalendervindu(Rundetype type, int aar, int fraAar, int fraMnd, int tilAar, int tilMnd)
    {
        var spenn = Runder.Spenn(type, aar);
        Assert.NotNull(spenn);
        Assert.Equal(fraAar, spenn!.Value.Fra.Year);
        Assert.Equal(fraMnd, spenn.Value.Fra.Month);
        Assert.Equal(tilAar, spenn.Value.Til.Year);
        Assert.Equal(tilMnd, spenn.Value.Til.Month);
    }

    [Fact]
    public void Ovrig_har_intet_spenn_og_ingen_aar()
    {
        Assert.Null(Runder.Spenn(Rundetype.Ovrig, 2027));
        Assert.False(Runder.HarAar(Rundetype.Ovrig));
        Assert.Equal("Øvrig", Runder.Etikett(Rundetype.Ovrig, null));
    }

    [Fact]
    public void Posisjonsdag_start_og_slutt_treffer_spennets_ender()
    {
        // Augustrunden 2027 = 1. juli–31. oktober 2026.
        Assert.Equal(new DateOnly(2026, 7, 1), Runder.Posisjonsdag(Rundetype.Augustrunden, 2027, Rundeposisjon.Start));
        Assert.Equal(new DateOnly(2026, 10, 31), Runder.Posisjonsdag(Rundetype.Augustrunden, 2027, Rundeposisjon.Slutt));
    }

    [Fact]
    public void Etikett_bruker_aar_for_daterte_runder()
    {
        Assert.Equal("Augustrunden 2027", Runder.Etikett(Rundetype.Augustrunden, 2027));
    }
}
