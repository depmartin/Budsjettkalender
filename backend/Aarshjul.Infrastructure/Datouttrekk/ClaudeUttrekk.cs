using System.Globalization;
using System.Text.Json;
using Aarshjul.Application.Datouttrekk;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Ren tolkning av Claude-svaret (adskilt fra HTTP-kallet i <see cref="ClaudeDatouttrekk"/> slik at
/// parsingen kan enhetstestes uten nettverk). Modellen bes returnere strukturert JSON på formen
/// <c>{ "frister": [ { "tittel", "dato", "budsjettaar", "kildeutdrag", "konfidens" }, … ] }</c>;
/// dette gjør hvert element om til en <see cref="Uttrekksresultat"/> (én per frist).
/// </summary>
public static class ClaudeUttrekk
{
    /// <summary>Systeminstruksjonen som ber modellen trekke ut alle fristene som strukturert JSON.</summary>
    public const string Systeminstruks =
        "Du er en assistent som leser norske rundskriv fra Finansdepartementet og trekker ut ALLE " +
        "fristene i dokumentet. Ett rundskriv inneholder ofte flere frister med ulike datoer (typisk i " +
        "en tidsplan- eller kalenderseksjon). For hver frist: gi en kort tittel som beskriver oppgaven, " +
        "datoen på ISO-format (yyyy-MM-dd) hvis den er konkret ellers null, budsjettåret fristen gjelder, " +
        "det ordrette tekstutdraget datoen er hentet fra, og en konfidens mellom 0 og 1. Ikke finn opp " +
        "frister som ikke står i teksten. Svar kun med JSON i det avtalte skjemaet.";

    /// <summary>JSON Schema modellen tvinges til å følge (output_config.format).</summary>
    public static JsonElement Skjema()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "frister": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "tittel": { "type": "string" },
                  "dato": { "type": ["string", "null"] },
                  "budsjettaar": { "type": "integer" },
                  "kildeutdrag": { "type": "string" },
                  "konfidens": { "type": "number" }
                },
                "required": ["tittel", "dato", "budsjettaar", "kildeutdrag", "konfidens"],
                "additionalProperties": false
              }
            }
          },
          "required": ["frister"],
          "additionalProperties": false
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Tolker JSON-innholdet modellen returnerte (teksten i første tekstblokk) til per-frist-resultater.
    /// Feltverdier speiler <see cref="Uttrekksfelter"/>; kildeutdraget bæres med på alle felt slik at det
    /// vises ved siden av tolket verdi i køen. Tåler manglende/ugyldig JSON ved å returnere tom liste.
    /// </summary>
    public static IReadOnlyList<Uttrekksresultat> TolkSvar(string? jsonInnhold, int budsjettaar)
    {
        if (string.IsNullOrWhiteSpace(jsonInnhold))
            return [];

        try
        {
            using var dok = JsonDocument.Parse(jsonInnhold);
            if (!dok.RootElement.TryGetProperty("frister", out var frister) ||
                frister.ValueKind != JsonValueKind.Array)
                return [];

            var resultater = new List<Uttrekksresultat>();
            foreach (var f in frister.EnumerateArray())
            {
                var tittel = Tekst(f, "tittel");
                var dato = Tekst(f, "dato");
                var utdrag = Tekst(f, "kildeutdrag");
                var aar = Heltall(f, "budsjettaar") ?? budsjettaar;
                var konfidens = Desimal(f, "konfidens") ?? 0.5;

                resultater.Add(new Uttrekksresultat
                {
                    Felter =
                    [
                        new Uttrekksfelt { Felt = Uttrekksfelter.Tittel, TolketVerdi = tittel, Kildeutdrag = utdrag, Konfidens = konfidens },
                        new Uttrekksfelt { Felt = Uttrekksfelter.Dato, TolketVerdi = dato, Kildeutdrag = utdrag, Konfidens = konfidens },
                        new Uttrekksfelt { Felt = Uttrekksfelter.Budsjettaar, TolketVerdi = aar.ToString(CultureInfo.InvariantCulture), Kildeutdrag = utdrag, Konfidens = konfidens }
                    ]
                });
            }
            return resultater;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Tekst(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Heltall(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static double? Desimal(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;
}
