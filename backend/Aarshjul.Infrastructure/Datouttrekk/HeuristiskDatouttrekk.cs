using System.Globalization;
using System.Text.RegularExpressions;
using Aarshjul.Application.Datouttrekk;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Deterministisk (offline) implementasjon av <see cref="IDatouttrekk"/>: finner en dato og en
/// tittel i et tekstavsnitt uten språkmodell. Brukes i demo og der en språkmodell ennå ikke er
/// koblet på. Den endelige Claude-API-implementasjonen plugges inn bak samme grensesnitt (Steg E,
/// IT-avklart) uten annen ombygging. Konfidensverdiene er bevisst moderate — usikkerhetsreglene
/// (SYSTEMARKITEKTUR 5) avgjør flagging, ikke denne selvvurderingen alene.
/// </summary>
public sealed class HeuristiskDatouttrekk : IDatouttrekk
{
    private static readonly string[] Maaneder =
    [
        "januar", "februar", "mars", "april", "mai", "juni",
        "juli", "august", "september", "oktober", "november", "desember"
    ];

    // «23. januar 2026», «23 januar», med valgfritt årstall.
    private static readonly Regex Maanedsdato = new(
        @"\b(\d{1,2})\.?\s+(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)(?:\s+(\d{4}))?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // «23.01.2026», «23/1-2026», «23-01-26».
    private static readonly Regex Numerisk = new(
        @"\b(\d{1,2})[./-](\d{1,2})[./-](\d{2,4})\b", RegexOptions.CultureInvariant);

    // ISO «2026-01-23».
    private static readonly Regex Iso = new(
        @"\b(\d{4})-(\d{1,2})-(\d{1,2})\b", RegexOptions.CultureInvariant);

    public Task<Uttrekksresultat> TrekkUtAsync(string pdfTekst, int budsjettaar, CancellationToken ct = default)
    {
        var avsnitt = (pdfTekst ?? string.Empty).Trim();
        var (dato, treff) = ProvDato(avsnitt, budsjettaar);
        var erRelativ = Datogjenkjenning.ErRelativFormulering(avsnitt);

        var datofelt = new Uttrekksfelt
        {
            Felt = Uttrekksfelter.Dato,
            TolketVerdi = dato?.ToString("yyyy-MM-dd", CultureInvariant),
            Kildeutdrag = Kort(avsnitt),
            // Konkret dato → moderat tillit; relativ formulering uten konkret dato → lav.
            Konfidens = dato is not null ? 0.65 : (erRelativ ? 0.35 : 0.2)
        };

        var tittel = Tittelkandidat(avsnitt, treff);
        var tittelfelt = new Uttrekksfelt
        {
            Felt = Uttrekksfelter.Tittel,
            TolketVerdi = tittel,
            Kildeutdrag = Kort(avsnitt),
            Konfidens = string.IsNullOrWhiteSpace(tittel) ? 0.2 : 0.5
        };

        return Task.FromResult(new Uttrekksresultat { Felter = [datofelt, tittelfelt] });
    }

    private static readonly CultureInfo CultureInvariant = CultureInfo.InvariantCulture;

    /// <summary>Finner første dato i teksten. Uten årstall i teksten brukes budsjettåret som beste gjett.</summary>
    private static (DateOnly? Dato, string? Treff) ProvDato(string tekst, int budsjettaar)
    {
        if (Iso.Match(tekst) is { Success: true } iso)
        {
            if (Bygg(int.Parse(iso.Groups[1].Value), int.Parse(iso.Groups[2].Value), int.Parse(iso.Groups[3].Value)) is { } d)
                return (d, iso.Value);
        }

        if (Numerisk.Match(tekst) is { Success: true } num)
        {
            var aar = NormaliserAar(int.Parse(num.Groups[3].Value));
            if (Bygg(aar, int.Parse(num.Groups[2].Value), int.Parse(num.Groups[1].Value)) is { } d)
                return (d, num.Value);
        }

        if (Maanedsdato.Match(tekst) is { Success: true } m)
        {
            var dag = int.Parse(m.Groups[1].Value);
            var maaned = Array.IndexOf(Maaneder, m.Groups[2].Value.ToLowerInvariant()) + 1;
            var aar = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : budsjettaar;
            if (Bygg(aar, maaned, dag) is { } d)
                return (d, m.Value);
        }

        return (null, null);
    }

    private static DateOnly? Bygg(int aar, int maaned, int dag)
    {
        if (maaned is < 1 or > 12 || dag < 1 || dag > DateTime.DaysInMonth(Math.Clamp(aar, 1, 9999), Math.Clamp(maaned, 1, 12)))
            return null;
        try { return new DateOnly(aar, maaned, dag); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static int NormaliserAar(int aar) => aar switch
    {
        < 100 => 2000 + aar, // «26» → 2026
        _ => aar
    };

    private static string? Tittelkandidat(string avsnitt, string? datotreff)
    {
        var uten = datotreff is null ? avsnitt : avsnitt.Replace(datotreff, " ", StringComparison.OrdinalIgnoreCase);
        uten = Regex.Replace(uten, @"\s+", " ").Trim(' ', '-', ':', '.', ',', ';');
        if (string.IsNullOrWhiteSpace(uten))
            uten = avsnitt.Trim();
        return uten.Length > 180 ? uten[..180].TrimEnd() + "…" : uten;
    }

    private static string Kort(string s) => s.Length > 240 ? s[..240].TrimEnd() + "…" : s;
}
