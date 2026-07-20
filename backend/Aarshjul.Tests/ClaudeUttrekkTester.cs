using Aarshjul.Application.Datouttrekk;
using Aarshjul.Infrastructure.Datouttrekk;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer tolkningen av Claude-svaret (<see cref="ClaudeUttrekk.TolkSvar"/>) uten nettverk:
/// strukturert JSON → én <see cref="Uttrekksresultat"/> per frist, robust mot manglende/ugyldig JSON.
/// Selve HTTP-kallet (<see cref="ClaudeDatouttrekk"/>) testes ikke mot ekte API her.
/// </summary>
public class ClaudeUttrekkTester
{
    [Fact]
    public void Tolker_flere_frister_til_ett_resultat_hver()
    {
        const string json = """
        {
          "frister": [
            { "tittel": "Frist for satsingsforslag", "dato": "2026-01-23", "budsjettaar": 2027, "kildeutdrag": "... 23. januar 2026", "konfidens": 0.9 },
            { "tittel": "Rammefordeling", "dato": "2026-03-15", "budsjettaar": 2027, "kildeutdrag": "... 15. mars 2026", "konfidens": 0.8 }
          ]
        }
        """;

        var res = ClaudeUttrekk.TolkSvar(json, 2027);

        Assert.Equal(2, res.Count);
        Assert.Equal("Frist for satsingsforslag", res[0].Felt(Uttrekksfelter.Tittel)?.TolketVerdi);
        Assert.Equal("2026-01-23", res[0].Felt(Uttrekksfelter.Dato)?.TolketVerdi);
        Assert.Equal("2027", res[0].Felt(Uttrekksfelter.Budsjettaar)?.TolketVerdi);
        Assert.Equal(0.9, res[0].Felt(Uttrekksfelter.Dato)?.Konfidens);
        // Kildeutdraget bæres med på hvert felt.
        Assert.Contains("23. januar", res[0].Felt(Uttrekksfelter.Dato)?.Kildeutdrag);
    }

    [Fact]
    public void Null_dato_bevares_som_tentativ()
    {
        const string json = """
        { "frister": [ { "tittel": "Høstkonferanse", "dato": null, "budsjettaar": 2027, "kildeutdrag": "ultimo august", "konfidens": 0.4 } ] }
        """;

        var res = ClaudeUttrekk.TolkSvar(json, 2027);

        Assert.Single(res);
        Assert.Null(res[0].Felt(Uttrekksfelter.Dato)?.TolketVerdi);
    }

    [Fact]
    public void Manglende_budsjettaar_faller_tilbake_paa_kontekst()
    {
        const string json = """
        { "frister": [ { "tittel": "X", "dato": "2026-05-01", "kildeutdrag": "1. mai 2026", "konfidens": 0.7 } ] }
        """;

        var res = ClaudeUttrekk.TolkSvar(json, 2028);

        Assert.Equal("2028", res[0].Felt(Uttrekksfelter.Budsjettaar)?.TolketVerdi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ikke gyldig json")]
    [InlineData("{}")]
    [InlineData("{ \"frister\": \"ikke en liste\" }")]
    public void Ugyldig_eller_tomt_svar_gir_tom_liste(string? innhold)
    {
        Assert.Empty(ClaudeUttrekk.TolkSvar(innhold, 2027));
    }
}
