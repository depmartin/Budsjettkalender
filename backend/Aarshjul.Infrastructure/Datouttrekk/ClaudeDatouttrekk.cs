using System.Net.Http.Json;
using System.Text.Json;
using Aarshjul.Application.Datouttrekk;
using Microsoft.Extensions.Options;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Ekte datouttrekk mot Claude (Messages API). Tar PDF-tekst inn, ber modellen returnere alle
/// fristene som strukturert JSON (tvunget via <c>output_config.format</c>), og tolker svaret via
/// <see cref="ClaudeUttrekk.TolkSvar"/>. Kalles bak <see cref="IDatouttrekk"/>, så pipelinen er
/// uendret enten fake eller ekte modell brukes.
/// </summary>
/// <remarks>
/// Rått HTTP-kall (ingen SDK-avhengighet) holder avtrykket lite og unngår versjonsrisiko på .NET 10;
/// endelig valg av provider/lokasjon (ekstern Claude API vs. Azure-vertet) og SDK-vs-rå er et
/// IT-styringsspørsmål (kravdok. kap. 12) som byttes bak <c>DatouttrekkOpsjoner.BasisUrl</c>. Feil
/// (HTTP-feil, avslag, ugyldig JSON) gir tom liste — pipelinen lager da et tentativt forslag til
/// manuell vurdering framfor å miste dokumentet.
/// </remarks>
public sealed class ClaudeDatouttrekk : IDatouttrekk
{
    private const string ApiVersjon = "2023-06-01";
    private const int MaksTokens = 8000;

    private readonly HttpClient _http;
    private readonly DatouttrekkOpsjoner _opsjoner;

    public ClaudeDatouttrekk(HttpClient http, IOptions<DatouttrekkOpsjoner> opsjoner)
    {
        _http = http;
        _opsjoner = opsjoner.Value;
    }

    public async Task<IReadOnlyList<Uttrekksresultat>> TrekkUtAsync(
        string pdfTekst, int budsjettaar, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfTekst))
            return [];

        var forespoersel = new
        {
            model = _opsjoner.Modell,
            max_tokens = MaksTokens,
            system = ClaudeUttrekk.Systeminstruks,
            output_config = new { format = new { type = "json_schema", schema = ClaudeUttrekk.Skjema() } },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Budsjettår i kontekst: {budsjettaar}.\n\nRundskrivets tekst:\n{pdfTekst}"
                }
            }
        };

        try
        {
            using var melding = new HttpRequestMessage(HttpMethod.Post, $"{_opsjoner.BasisUrl}/v1/messages")
            {
                Content = JsonContent.Create(forespoersel)
            };
            melding.Headers.Add("x-api-key", _opsjoner.ApiNokkel);
            melding.Headers.Add("anthropic-version", ApiVersjon);

            using var svar = await _http.SendAsync(melding, ct);
            if (!svar.IsSuccessStatusCode)
                return [];

            using var dok = JsonDocument.Parse(await svar.Content.ReadAsStringAsync(ct));
            var rot = dok.RootElement;

            // Avslag (stop_reason = refusal) → ingen frister; pipelinen lager tentativt forslag.
            if (rot.TryGetProperty("stop_reason", out var stopp) && stopp.GetString() == "refusal")
                return [];

            // Første tekstblokk inneholder JSON-en (garantert av output_config.format).
            if (!rot.TryGetProperty("content", out var innhold) || innhold.ValueKind != JsonValueKind.Array)
                return [];

            foreach (var blokk in innhold.EnumerateArray())
            {
                if (blokk.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                    blokk.TryGetProperty("text", out var tekst))
                {
                    return ClaudeUttrekk.TolkSvar(tekst.GetString(), budsjettaar);
                }
            }
            return [];
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return [];
        }
    }
}
