using System.Net;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer .ics-eksport-endepunktet: kun administrator har tilgang, og utvalget følger
/// den valgte gruppens server-side synlighet — nøyaktig som Word-eksporten. Verifiseres på selve
/// den nedlastede kalenderfilen (SUMMARY-linjene), ikke bare i UI.
/// </summary>
public class KalenderEksportApiTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    private async Task<string> EksporterTekstAsync(string spørring, string rolle = "Administrator")
    {
        var klient = app.CreateClient();
        var melding = new HttpRequestMessage(HttpMethod.Get, $"/api/eksport/ics?{spørring}");
        melding.Headers.Add("X-Test-Rolle", rolle);

        var svar = await klient.SendAsync(melding);
        svar.EnsureSuccessStatusCode();
        Assert.Equal("text/calendar", svar.Content.Headers.ContentType?.MediaType);

        return await svar.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Er_en_gyldig_ics_med_kalenderhendelser()
    {
        var ics = await EksporterTekstAsync("alt=true");
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
    }

    [Fact]
    public async Task Eksport_for_FAG_utelater_FIN_interne_frister()
    {
        var ics = await EksporterTekstAsync("gruppe=FAG");
        Assert.Contains("Kun FAG", ics);
        Assert.DoesNotContain("FA og POL", ics);
    }

    [Fact]
    public async Task Eksport_for_FA_gir_FA_settet_men_ikke_FAG()
    {
        var ics = await EksporterTekstAsync("gruppe=FA");
        Assert.Contains("Kun FA", ics);
        Assert.Contains("FA og POL", ics);
        Assert.DoesNotContain("Kun FAG", ics);
    }

    [Fact]
    public async Task Bidragsyter_far_403()
    {
        var klient = app.CreateClient();
        var melding = new HttpRequestMessage(HttpMethod.Get, "/api/eksport/ics?gruppe=FA");
        melding.Headers.Add("X-Test-Rolle", "Bidragsyter");
        var svar = await klient.SendAsync(melding);
        Assert.Equal(HttpStatusCode.Forbidden, svar.StatusCode);
    }

    [Fact]
    public async Task Uautentisert_far_401()
    {
        var klient = app.CreateClient();
        var svar = await klient.GetAsync("/api/eksport/ics?alt=true");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
    }
}
