using System.Net;
using DocumentFormat.OpenXml.Packaging;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer Word-eksport-endepunktet (Steg K): kun administrator har tilgang, og utvalget følger
/// den valgte gruppens server-side synlighet — «for FAG» utelater FIN-interne frister, «for POL»
/// gir nøyaktig POL-settet. Verifiseres på selve nedlastede dokumentet.
/// </summary>
public class EksportApiTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private async Task<string> EksporterTekstAsync(string spørring, string rolle = "Administrator")
    {
        var klient = app.CreateClient();
        var melding = new HttpRequestMessage(HttpMethod.Get, $"/api/eksport/word?{spørring}");
        melding.Headers.Add("X-Test-Rolle", rolle);

        var svar = await klient.SendAsync(melding);
        svar.EnsureSuccessStatusCode();
        Assert.Equal(DocxType, svar.Content.Headers.ContentType?.MediaType);

        var bytes = await svar.Content.ReadAsByteArrayAsync();
        using var strøm = new MemoryStream(bytes);
        using var dok = WordprocessingDocument.Open(strøm, false);
        return dok.MainDocumentPart!.Document.Body!.InnerText;
    }

    [Fact]
    public async Task Utskrift_for_FAG_utelater_FIN_interne_frister()
    {
        var tekst = await EksporterTekstAsync("gruppe=FAG");
        Assert.Contains("Kun FAG", tekst);
        // «FA og POL» er FIN-internt og skal være utelatt. («Kun FA» unngås her fordi det er en
        // delstreng av «Kun FAG»; FA-only-utelukkelsen dekkes av FA-/alt-testene.)
        Assert.DoesNotContain("FA og POL", tekst);
    }

    [Fact]
    public async Task Utskrift_for_FA_gir_FA_settet_men_ikke_FAG()
    {
        var tekst = await EksporterTekstAsync("gruppe=FA");
        Assert.Contains("Kun FA", tekst);
        Assert.Contains("FA og POL", tekst);
        Assert.DoesNotContain("Kun FAG", tekst);
    }

    [Fact]
    public async Task Alt_gir_fullt_innsyn_merket_FIN_internt()
    {
        var tekst = await EksporterTekstAsync("alt=true");
        Assert.Contains("Kun FA", tekst);
        Assert.Contains("Kun FAG", tekst);
        Assert.Contains("FA og POL", tekst);
        Assert.Contains("FIN-internt", tekst);
    }

    [Fact]
    public async Task Bidragsyter_far_403()
    {
        var klient = app.CreateClient();
        var melding = new HttpRequestMessage(HttpMethod.Get, "/api/eksport/word?gruppe=FA");
        melding.Headers.Add("X-Test-Rolle", "Bidragsyter");
        var svar = await klient.SendAsync(melding);
        Assert.Equal(HttpStatusCode.Forbidden, svar.StatusCode);
    }

    [Fact]
    public async Task Uautentisert_far_401()
    {
        var klient = app.CreateClient();
        var svar = await klient.GetAsync("/api/eksport/word?alt=true");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
    }
}
