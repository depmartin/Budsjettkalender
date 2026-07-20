using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Aarshjul.Kilder;

/// <summary>
/// Ren parsing av regjeringen.no sin rundskrivoversikt (kravdok. 4.2) til
/// <see cref="Dokumentreferanse"/>-rader, adskilt fra selve nedlastingen slik at logikken kan
/// verifiseres offline mot en lagret kopi av arkivsiden. <see cref="RegjeringenKilde"/> henter
/// HTML-en og kaller denne.
/// </summary>
/// <remarks>
/// Forankret i ekte markup (mars 2026): hver rad er en <c>&lt;tr&gt;</c> med fire celler —
/// nummer (med PDF-lenke), tittel, dato (dd.mm.yyyy) og status («Årlig»/«Fast»/«Utgått»).
/// <para>
/// <b>PDF-URL leses fra <c>href</c>, aldri utledes fra et mønster.</b> Filnavnene er inkonsekvente
/// over årene (<c>r6-2025.pdf</c>, <c>r-5-2025.pdf</c>, <c>r_110_2025.pdf</c>), og lenka er den
/// eneste robuste kilden. Nummeret leses kun som svakt hint til totrinnsfilteret (kravdok. 4.3),
/// aldri som nøkkel — det kan skifte mellom år.
/// </para>
/// </remarks>
public static class RegjeringenParser
{
    /// <summary>Basisadresse relative lenker absolutteres mot.</summary>
    public const string Basisurl = "https://www.regjeringen.no";

    // «R-6/2025», «R-10/06», «R-110/2025» → nummer + (to- eller firesifret) år.
    private static readonly Regex Nummermonster =
        new(@"R-?\s*(\d+)\s*/\s*(\d{2,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // dd.mm.yyyy et sted i datocellen.
    private static readonly Regex Datomonster =
        new(@"(\d{1,2})\.(\d{1,2})\.(\d{4})", RegexOptions.Compiled);

    // Firesifret årstall i PDF-stien, f.eks. «/arlige/2006/» — autoritativt der det finnes.
    private static readonly Regex Aaristimonster =
        new(@"/((?:19|20)\d{2})/", RegexOptions.Compiled);

    /// <summary>
    /// Parser oversikts-HTML til et <see cref="OppdagResultat"/>. Kaster aldri: en uventet
    /// struktur eller et unntak gir <see cref="Oppdagutfall.KlarteIkkeParse"/>, slik at en stille
    /// feil varsles framfor å se ut som en stille periode (SYSTEMARKITEKTUR 5). Arkivsiden er i
    /// praksis aldri tom, så «fant tabell, men ingen rader» tolkes også som en parse-feil.
    /// </summary>
    public static OppdagResultat Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return OppdagResultat.ParseFeil("Tomt svar fra oversikten.");

        try
        {
            var parser = new HtmlParser();
            using var dok = parser.ParseDocument(html);

            var tabeller = dok.QuerySelectorAll("table");
            if (tabeller.Length == 0)
                return OppdagResultat.ParseFeil("Fant ingen tabell i oversikten — sidestrukturen kan være endret.");

            var referanser = new List<Dokumentreferanse>();
            var settNokler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rad in dok.QuerySelectorAll("tr"))
            {
                var referanse = ParseRad(rad);
                if (referanse is null)
                    continue;

                // Samme rundskriv kan i teorien stå flere steder; behold første forekomst.
                if (settNokler.Add(referanse.Nokkel))
                    referanser.Add(referanse);
            }

            if (referanser.Count == 0)
                return OppdagResultat.ParseFeil(
                    "Fant tabell(er), men ingen rundskrivrader — radmarkupen kan være endret.");

            return OppdagResultat.Fant(referanser);
        }
        catch (Exception e)
        {
            return OppdagResultat.ParseFeil($"Uventet feil under parsing: {e.Message}");
        }
    }

    /// <summary>
    /// Parser én tabellrad til en <see cref="Dokumentreferanse"/>, eller <c>null</c> hvis raden
    /// ikke er en rundskrivrad (topptekstrader, rader uten PDF-lenke i første celle).
    /// </summary>
    private static Dokumentreferanse? ParseRad(IElement rad)
    {
        var celler = rad.QuerySelectorAll("td");
        if (celler.Length < 3)
            return null;

        // Lenka til dokumentet ligger i første celle.
        var lenke = celler[0].QuerySelector("a[href]");
        var href = lenke?.GetAttribute("href");
        if (lenke is null || string.IsNullOrWhiteSpace(href))
            return null;

        var nummertekst = CelleTekst(lenke); // f.eks. «R-4/2025»
        var m = Nummermonster.Match(nummertekst);
        if (!m.Success)
            return null; // ikke en gjenkjennelig rundskrivlenke

        var nummer = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

        var url = Absoluttera(href);
        var aar = AarFraUrl(url) ?? AarFraTosifret(m.Groups[2].Value);

        var tittel = CelleTekst(celler[1]);
        var dato = celler.Length >= 3 ? ParseDato(CelleTekst(celler[2])) : null;

        return new Dokumentreferanse
        {
            Nokkel = $"r-{nummer}-{aar}",
            Tittel = tittel,
            Dato = dato,
            Url = url,
            Nummer = nummer
        };
    }

    /// <summary>
    /// Absolutterer en relativ lenke mot <see cref="Basisurl"/>; lar http(s)-absolutte stå.
    /// Sjekker http(s) eksplisitt fordi en sti som «/globalassets/…» ellers tolkes som en
    /// <c>file://</c>-URI på Unix.
    /// </summary>
    private static string Absoluttera(string href)
    {
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return href;
        if (Uri.TryCreate(new Uri(Basisurl), href, out var kombinert))
            return kombinert.ToString();
        return href;
    }

    private static int? AarFraUrl(string url)
    {
        var m = Aaristimonster.Match(url);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Utleder årstall fra to- eller firesifret verdi i nummeret; pivot 70 for tosifret.</summary>
    private static int AarFraTosifret(string raa)
    {
        var v = int.Parse(raa, CultureInfo.InvariantCulture);
        if (raa.Length == 4)
            return v;
        return v < 70 ? 2000 + v : 1900 + v;
    }

    private static DateOnly? ParseDato(string tekst)
    {
        var m = Datomonster.Match(tekst);
        if (!m.Success)
            return null;
        var dag = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var maaned = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var aar = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        try
        {
            return new DateOnly(aar, maaned, dag);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Leser tekstinnholdet i en celle med linjeskift (<c>&lt;br&gt;</c>) og avsnittsgrenser
    /// oversatt til mellomrom, slik at titler over flere linjer ikke smelter sammen til ett ord.
    /// </summary>
    private static string CelleTekst(IElement element)
    {
        var html = element.InnerHtml ?? string.Empty;
        html = Regex.Replace(html, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</(p|div|li)>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", string.Empty);
        html = System.Net.WebUtility.HtmlDecode(html);
        return SlaSammenMellomrom(html);
    }

    private static string SlaSammenMellomrom(string s) =>
        Regex.Replace(s, @"\s+", " ").Trim();
}
