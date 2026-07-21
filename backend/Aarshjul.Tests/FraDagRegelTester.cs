using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// «Relativ ukedag» med <c>fra_dag</c>: første ukedag på/etter en gitt dag — f.eks. mandagen i
/// uken mellom 20. og 26. juli (første mandag på/etter 20. juli).
/// </summary>
public class FraDagRegelTester
{
    [Theory]
    [InlineData(2025)]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2028)]
    [InlineData(2029)]
    [InlineData(2030)]
    [InlineData(2031)]
    [InlineData(2032)]
    [InlineData(2033)]
    public void Foerste_mandag_paa_eller_etter_20_juli_lander_alltid_i_vinduet_20_til_26(int aar)
    {
        var dato = Datoberegning.FoersteUkedagFraOgMed(aar, 7, 20, DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, dato.DayOfWeek);
        Assert.Equal(7, dato.Month);
        Assert.InRange(dato.Day, 20, 26);
    }

    [Fact]
    public void Generering_med_fra_dag_gir_mandag_i_vinduet()
    {
        var regel = new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = "sommermilepael",
            Tittel = "Sommermilepæl",
            Kategori = Kategori.Budsjett,
            Regeltype = Regeltype.RelativUkedag,
            Regelparametre = "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}"
        };

        var r = Assert.Single(Genereringsberegning.Beregn(2030, [regel]));

        Assert.False(r.ErFeil);
        Assert.Equal(DayOfWeek.Monday, r.Dato!.Value.DayOfWeek);
        Assert.Equal(2030, r.Dato.Value.Year);
        Assert.Equal(7, r.Dato.Value.Month);
        Assert.InRange(r.Dato.Value.Day, 20, 26);
    }

    [Fact]
    public void Uten_fra_dag_gjelder_fortsatt_n_te_forekomst()
    {
        // Andre mandag i mars 2026 (1. mars 2026 er søndag → mandager 2, 9, 16 …) = 9.
        var regel = new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = "marsmandag",
            Tittel = "Marsmandag",
            Kategori = Kategori.Budsjett,
            Regeltype = Regeltype.RelativUkedag,
            Regelparametre = "{\"maaned\":3,\"uke\":2,\"ukedag\":\"man\"}"
        };

        var r = Assert.Single(Genereringsberegning.Beregn(2026, [regel]));

        Assert.Equal(new DateOnly(2026, 3, 9), r.Dato);
    }
}
