using Aarshjul.Application.Datouttrekk;
using Aarshjul.Infrastructure.Datouttrekk;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Det heuristiske (offline) datouttrekket: parser norske datoformater fra tekst.</summary>
public class HeuristiskDatouttrekkTester
{
    private readonly HeuristiskDatouttrekk _u = new();

    private async Task<string?> Dato(string tekst, int budsjettaar = 2027)
    {
        var r = await _u.TrekkUtAsync(tekst, budsjettaar);
        return r.Felt(Uttrekksfelter.Dato)?.TolketVerdi;
    }

    [Theory]
    [InlineData("Frist er 23. januar 2027.", "2027-01-23")]
    [InlineData("Sendes ut 15.03.2027.", "2027-03-15")]
    [InlineData("Innen 2027-02-15 skal alt være klart.", "2027-02-15")]
    [InlineData("Møte 23/1-27.", "2027-01-23")]
    public async Task Parser_konkrete_datoer(string tekst, string forventet)
    {
        Assert.Equal(forventet, await Dato(tekst));
    }

    [Fact]
    public async Task Uten_aarstall_brukes_budsjettaaret()
    {
        Assert.Equal("2027-03-05", await Dato("Frist 5. mars", budsjettaar: 2027));
    }

    [Fact]
    public async Task Uten_dato_gir_null_datoverdi()
    {
        Assert.Null(await Dato("Dette avsnittet har ingen dato."));
    }

    [Fact]
    public async Task Tittel_trekkes_ut_uten_datoen()
    {
        var r = await _u.TrekkUtAsync("Frist for satsingsforslag er 23. januar 2027.", 2027);
        var tittel = r.Felt(Uttrekksfelter.Tittel)?.TolketVerdi;
        Assert.NotNull(tittel);
        Assert.Contains("satsingsforslag", tittel);
        Assert.DoesNotContain("23. januar 2027", tittel);
    }
}
