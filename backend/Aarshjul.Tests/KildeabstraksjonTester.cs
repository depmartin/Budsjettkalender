using Aarshjul.Kilder;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer kjernekravet til kildeabstraksjonen (Fase 2, Steg A): grensesnittet uttrykker
/// utfall, slik at en vellykket, tom kjøring skilles fra en parse-feil.
/// </summary>
public class KildeabstraksjonTester
{
    private static Dokumentreferanse EnReferanse() => new()
    {
        Nokkel = "r-4-2026",
        Tittel = "Hovedbudsjettskriv for 2027",
        Url = "https://example/r-4-2026.pdf",
        Nummer = 4
    };

    [Fact]
    public void Fant_dokumenter_gir_utfall_med_referanser()
    {
        var res = OppdagResultat.Fant([EnReferanse()]);

        Assert.Equal(Oppdagutfall.FantDokumenter, res.Utfall);
        Assert.Single(res.Dokumenter);
        Assert.Null(res.Feilmelding);
    }

    [Fact]
    public void Tom_men_vellykket_kjoering_er_ikke_parsefeil()
    {
        var tom = OppdagResultat.Fant([]);
        var feil = OppdagResultat.ParseFeil("oversiktstabellen manglet");

        Assert.Equal(Oppdagutfall.IngenDokumenter, tom.Utfall);
        Assert.Empty(tom.Dokumenter);
        Assert.NotEqual(tom.Utfall, feil.Utfall);
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, feil.Utfall);
        Assert.Equal("oversiktstabellen manglet", feil.Feilmelding);
    }

    [Fact]
    public void Hent_skiller_lyktes_fra_feilet()
    {
        var ok = HentResultat.Ok([1, 2, 3], "application/pdf");
        var feil = HentResultat.Feil("404");

        Assert.Equal(Hentutfall.Lyktes, ok.Utfall);
        Assert.Equal(3, ok.Innhold!.Length);
        Assert.Equal("application/pdf", ok.MediaType);
        Assert.Equal(Hentutfall.Feilet, feil.Utfall);
        Assert.Equal("404", feil.Feilmelding);
    }

    [Fact]
    public async Task Kilde_kan_implementeres_bak_grensesnittet()
    {
        IKilde kilde = new FakeKilde();

        var oppdag = await kilde.OppdagAsync();
        var hent = await kilde.HentAsync(EnReferanse());

        Assert.Equal("fake", kilde.Kode);
        Assert.Equal(Oppdagutfall.FantDokumenter, oppdag.Utfall);
        Assert.Equal(Hentutfall.Lyktes, hent.Utfall);
    }

    private sealed class FakeKilde : IKilde
    {
        public string Kode => "fake";

        public Task<OppdagResultat> OppdagAsync(CancellationToken ct = default) =>
            Task.FromResult(OppdagResultat.Fant([EnReferanse()]));

        public Task<HentResultat> HentAsync(Dokumentreferanse referanse, CancellationToken ct = default) =>
            Task.FromResult(HentResultat.Ok([0x25, 0x50, 0x44, 0x46], "application/pdf"));
    }
}
