using System.Net.Http.Headers;

namespace Aarshjul.Kilder;

/// <summary>
/// Første konkrete kilde (kravdok. 4.2): leser Finansdepartementets rundskrivarkiv på
/// regjeringen.no og laster ned enkeltdokumenter. Selve HTML-tolkningen ligger i den rene
/// <see cref="RegjeringenParser"/> slik at den kan verifiseres offline; denne klassen står kun
/// for nettverksleddet.
/// </summary>
/// <remarks>
/// <b>Merk (2026-07):</b> regjeringen.no ligger bak Cloudflares «challenge»-beskyttelse, som svarer
/// 403 på ikke-nettleserklienter. Live <see cref="OppdagAsync"/>/<see cref="HentAsync"/> kan derfor
/// ikke fullføres fra sandkassen ennå (se beslutningsloggen). Parsing- og URL-logikken er likevel
/// ferdig og testet mot en lagret kopi; nettverksleddet kobles til når tilgangen er avklart med IT.
/// </remarks>
public sealed class RegjeringenKilde : IKilde
{
    /// <summary>Arkivsiden med oversikt over FINs rundskriv (kravdok. 4.2).</summary>
    public const string Arkivurl =
        "https://www.regjeringen.no/no/dokument/dep/fin/rundskriv/arkiv/id446220/";

    private readonly HttpClient _http;

    public RegjeringenKilde(HttpClient http)
    {
        _http = http;
    }

    public string Kode => "regjeringen";

    public async Task<OppdagResultat> OppdagAsync(CancellationToken ct = default)
    {
        string html;
        try
        {
            using var forespoersel = LagForespoersel(HttpMethod.Get, Arkivurl);
            using var svar = await _http.SendAsync(forespoersel, ct);
            if (!svar.IsSuccessStatusCode)
                return OppdagResultat.ParseFeil(
                    $"Oversikten svarte {(int)svar.StatusCode} {svar.ReasonPhrase}.");
            html = await svar.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return OppdagResultat.ParseFeil($"Klarte ikke hente oversikten: {e.Message}");
        }

        return RegjeringenParser.Parse(html);
    }

    public async Task<HentResultat> HentAsync(Dokumentreferanse referanse, CancellationToken ct = default)
    {
        try
        {
            using var forespoersel = LagForespoersel(HttpMethod.Get, referanse.Url);
            using var svar = await _http.SendAsync(forespoersel, ct);
            if (!svar.IsSuccessStatusCode)
                return HentResultat.Feil($"{(int)svar.StatusCode} {svar.ReasonPhrase} for {referanse.Url}");

            var innhold = await svar.Content.ReadAsByteArrayAsync(ct);
            var mediaType = svar.Content.Headers.ContentType?.MediaType;
            return HentResultat.Ok(innhold, mediaType);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return HentResultat.Feil($"Klarte ikke laste ned {referanse.Url}: {e.Message}");
        }
    }

    /// <summary>
    /// Bygger en forespørsel med en ekte nettleser-User-Agent (kravdok. 4.2). Nødvendig fordi
    /// regjeringen.no avviser klienter uten (og bak Cloudflare kreves i praksis en full nettleser).
    /// </summary>
    private static HttpRequestMessage LagForespoersel(HttpMethod metode, string url)
    {
        var forespoersel = new HttpRequestMessage(metode, url);
        forespoersel.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        forespoersel.Headers.AcceptLanguage.ParseAdd("nb-NO,nb;q=0.9,no;q=0.8,en;q=0.5");
        forespoersel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        forespoersel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        forespoersel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        return forespoersel;
    }
}
