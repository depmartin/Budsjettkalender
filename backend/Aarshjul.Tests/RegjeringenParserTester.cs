using Aarshjul.Domain;
using Aarshjul.Kilder;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer <see cref="RegjeringenParser"/> (Fase 2, Steg B) mot en lagret kopi av FINs
/// rundskrivarkiv (se <c>Fixtures/README.md</c>). Dekker radparsing, robust nummer-/år-/URL-
/// tolkning, utfallsskillet (fant / parse-feil) og samspillet med <see cref="Totrinnsfilter"/>.
/// </summary>
public class RegjeringenParserTester
{
    private static string LesFixtur()
    {
        var sti = Path.Combine(AppContext.BaseDirectory, "Fixtures", "regjeringen-rundskriv-arkiv.html");
        return File.ReadAllText(sti);
    }

    private static OppdagResultat ParseFixtur() => RegjeringenParser.Parse(LesFixtur());

    private static Dokumentreferanse Finn(OppdagResultat res, string nokkel) =>
        res.Dokumenter.Single(d => d.Nokkel == nokkel);

    [Fact]
    public void Parser_arkivsiden_og_finner_mange_rundskriv()
    {
        var res = ParseFixtur();

        Assert.Equal(Oppdagutfall.FantDokumenter, res.Utfall);
        Assert.True(res.Dokumenter.Count > 100,
            $"forventet mange rundskrivrader, fant {res.Dokumenter.Count}");
        Assert.Null(res.Feilmelding);
    }

    [Fact]
    public void Alle_referanser_har_gyldige_kjernefelt()
    {
        var res = ParseFixtur();

        Assert.All(res.Dokumenter, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Nokkel));
            Assert.False(string.IsNullOrWhiteSpace(d.Tittel));
            Assert.StartsWith("https://www.regjeringen.no/", d.Url);
            Assert.NotNull(d.Nummer);
        });

        // PDF er normen, men kilden inneholder reell «skitten» data (ett gammelt rundskriv
        // lenket til en HTML-side, én rad feillenket til et bilde). Konservativ linje: behold
        // alle rader — totrinnsfilteret og godkjenningskøen siler nedstrøms. Verifiser at de
        // aller fleste likevel er PDF-er, så en fremtidig strukturendring fanges opp.
        var andelPdf = res.Dokumenter.Count(d => d.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                       / (double)res.Dokumenter.Count;
        Assert.True(andelPdf > 0.95, $"forventet at nesten alle lenker er PDF, var {andelPdf:P0}");
    }

    [Fact]
    public void Leser_pdf_url_fra_href_og_absolutterer()
    {
        var res = ParseFixtur();

        // Filnavnet er inkonsekvent («r4-2025.pdf»), så URL må komme fra href, ikke fra et mønster.
        var r4 = Finn(res, "r-4-2025");
        Assert.Equal(
            "https://www.regjeringen.no/globalassets/upload/fin/vedlegg/okstyring/rundskriv/arlige/2025/r4-2025.pdf",
            r4.Url);
        Assert.Equal(4, r4.Nummer);
        Assert.Equal(new DateOnly(2025, 4, 1), r4.Dato);
        Assert.Contains("hovudbudsjettskriv", r4.Tittel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tittel_over_flere_linjer_smelter_ikke_sammen()
    {
        var res = ParseFixtur();

        // R-4-tittelen er «HOVUDBUDSJETTSKRIV FOR 2026<br />Retningslinjer for arbeidet …».
        var r4 = Finn(res, "r-4-2025");
        Assert.Contains("2026 Retningslinjer", r4.Tittel);
        Assert.DoesNotContain("2026Retningslinjer", r4.Tittel);
    }

    [Fact]
    public void Tosifret_aar_i_gammelt_rundskriv_tolkes_riktig()
    {
        var res = ParseFixtur();

        // «R-10/06» med PDF-sti under /arlige/2006/ → år 2006.
        var gammelt = Finn(res, "r-10-2006");
        Assert.Equal(10, gammelt.Nummer);
        Assert.Contains("/2006/", gammelt.Url);
    }

    [Fact]
    public void Nokkel_er_uavhengig_av_inkonsekvent_filnavn()
    {
        var res = ParseFixtur();

        // Ulike filnavnvarianter (r-5-2025, r_110_2025) gir likevel kanonisk nøkkel r-{nr}-{aar}.
        Assert.Equal("r-5-2025", Finn(res, "r-5-2025").Nokkel);
        Assert.Equal("r-110-2025", Finn(res, "r-110-2025").Nokkel);
    }

    [Fact]
    public void Tomt_svar_gir_parsefeil_ikke_tom_liste()
    {
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, RegjeringenParser.Parse("").Utfall);
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, RegjeringenParser.Parse("   ").Utfall);
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, RegjeringenParser.Parse(null).Utfall);
    }

    [Fact]
    public void Manglende_tabell_gir_parsefeil()
    {
        var res = RegjeringenParser.Parse("<html><body><h1>Ingen tabell her</h1></body></html>");
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, res.Utfall);
        Assert.NotNull(res.Feilmelding);
    }

    [Fact]
    public void Tabell_uten_rundskrivrader_gir_parsefeil()
    {
        // Arkivsiden er aldri tom; en tabell uten gjenkjennelige rader = strukturendring, ikke stille periode.
        var res = RegjeringenParser.Parse(
            "<html><body><table><tr><th>Nummer</th><th>Tittel</th></tr></table></body></html>");
        Assert.Equal(Oppdagutfall.KlarteIkkeParse, res.Utfall);
    }

    [Fact]
    public void Spiller_sammen_med_totrinnsfilteret()
    {
        var res = ParseFixtur();

        // Årlig rammefordeling (R-4) gjenkjennes på tittel.
        var r4 = Finn(res, "r-4-2025");
        var k4 = Totrinnsfilter.Klassifiser(r4.Nummer, r4.Tittel);
        Assert.Equal(Klassifiseringsutfall.Gjenkjent, k4.Utfall);
        Assert.Equal("rammefordeling", k4.Loep);
        Assert.Equal(Kategori.Budsjett, k4.Kategori);

        // Gul bok (R-6) gjenkjennes på tittel.
        var r6 = Finn(res, "r-6-2025");
        var k6 = Totrinnsfilter.Klassifiser(r6.Nummer, r6.Tittel);
        Assert.Equal(Klassifiseringsutfall.Gjenkjent, k6.Utfall);
        Assert.Equal("gulbok", k6.Loep);

        // Varig regelverk (R-110) ignoreres på nummerserie.
        var r110 = Finn(res, "r-110-2025");
        var k110 = Totrinnsfilter.Klassifiser(r110.Nummer, r110.Tittel);
        Assert.Equal(Klassifiseringsutfall.Ignorer, k110.Utfall);
    }

    [Fact]
    public void Aarlig_rundskriv_uten_titteltreff_blir_ukjent_type()
    {
        var res = ParseFixtur();

        // Sikkerhetsnettet: minst ett årlig rundskriv (nr < 100) matcher ingen løpsmønster
        // og skal da havne som «ukjent type» framfor å slippes stille.
        var ukjente = res.Dokumenter
            .Where(d => d.Nummer is > 0 and < 100)
            .Select(d => Totrinnsfilter.Klassifiser(d.Nummer, d.Tittel))
            .Count(k => k.Utfall == Klassifiseringsutfall.UkjentType);

        Assert.True(ukjente > 0, "forventet minst ett årlig rundskriv uten titteltreff (ukjent type)");
    }
}
