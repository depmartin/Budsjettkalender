using System.Net;
using Aarshjul.Application.Kalender;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer abonnements-feed-endepunktet (Endring 1): det er anonymt (tokenet er
/// autoriseringen), men serveren filtrerer alltid til lenkens gruppe — en POL-lenke gir kun
/// POL-settet, aldri FIN-interne frister. Avskrudde/ukjente tokens gir 404.
/// </summary>
public class KalenderfeedApiTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    private async Task<string> OpprettFeedAsync(string? gruppe, bool aktiv = true)
    {
        using var scope = app.Services.CreateScope();
        var tjeneste = scope.ServiceProvider.GetRequiredService<IKalenderabonnement>();
        var a = await tjeneste.OpprettAsync(gruppe, "admin");
        if (!aktiv)
        {
            await tjeneste.SettAktivAsync(a.Id, false);
        }
        return a.Token;
    }

    [Fact]
    public async Task Aktiv_POL_feed_gir_kun_POL_frister_uten_innlogging()
    {
        var token = await OpprettFeedAsync("POL");

        // Ingen X-Test-Rolle-header: forespørselen er uautentisert — feeden skal likevel svare.
        var svar = await app.CreateClient().GetAsync($"/kalender/feed/{token}.ics");

        svar.EnsureSuccessStatusCode();
        Assert.Equal("text/calendar", svar.Content.Headers.ContentType?.MediaType);
        var ics = await svar.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("FA og POL", ics);      // POL ser denne
        Assert.DoesNotContain("Kun FAG", ics);  // ikke synlig for POL
        Assert.DoesNotContain("Kun FA", ics);   // FA-intern, ikke synlig for POL
    }

    [Fact]
    public async Task Avskrudd_feed_gir_404()
    {
        var token = await OpprettFeedAsync("POL", aktiv: false);
        var svar = await app.CreateClient().GetAsync($"/kalender/feed/{token}.ics");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Ukjent_token_gir_404()
    {
        var svar = await app.CreateClient().GetAsync("/kalender/feed/finnes-ikke.ics");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
