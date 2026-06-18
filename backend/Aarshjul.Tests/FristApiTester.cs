using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer at HTTP-svaret fra <c>/api/frister</c> er synlighetsfiltrert på server —
/// ikke bare det grensesnittet viser (SYSTEMARKITEKTUR 4, Fase 1-verifikasjon).
/// </summary>
public class FristApiTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<List<string>> HentTitlerAsync(string rolle, string? grupper)
    {
        var klient = app.CreateClient();
        var melding = new HttpRequestMessage(HttpMethod.Get, "/api/frister?historikk=true");
        melding.Headers.Add("X-Test-Rolle", rolle);
        if (grupper is not null)
        {
            melding.Headers.Add("X-Test-Grupper", grupper);
        }

        var svar = await klient.SendAsync(melding);
        svar.EnsureSuccessStatusCode();

        var frister = await svar.Content.ReadFromJsonAsync<List<FristSvar>>(Json);
        return frister!.Select(f => f.Tittel).OrderBy(t => t).ToList();
    }

    [Fact]
    public async Task Fag_bruker_far_kun_FAG_frister_i_svaret()
    {
        var titler = await HentTitlerAsync("Leser", "FAG");
        Assert.Equal(["Kun FAG"], titler);
    }

    [Fact]
    public async Task Fa_bruker_far_FA_og_FA_POL_men_ikke_FAG()
    {
        var titler = await HentTitlerAsync("Bidragsyter", "FA");
        Assert.Equal(["FA og POL", "Kun FA"], titler);
    }

    [Fact]
    public async Task Administrator_far_alle_frister()
    {
        var titler = await HentTitlerAsync("Administrator", grupper: null);
        Assert.Equal(["FA og POL", "Kun FA", "Kun FAG"], titler);
    }

    [Fact]
    public async Task Uautentisert_forespoersel_gir_401()
    {
        var klient = app.CreateClient();
        var svar = await klient.GetAsync("/api/frister");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
    }

    private sealed record FristSvar(string Tittel, IReadOnlyList<string> Synlighet);
}
