using System.Globalization;
using System.Text.RegularExpressions;
using Aarshjul.Application.Datouttrekk;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Deterministisk erstatning for det ekte språkmodell-uttrekket, brukt i utvikling/test og mens
/// den endelige Claude-provideren (ekstern API vs. Azure-vertet) er en åpen IT-avklaring
/// (kravdok. kap. 12). Gir forutsigbare per-felt-resultater slik at hele pipelinen og
/// godkjenningskøen kan kjøres ende-til-ende uten et API-kall. Byttes ut mot en
/// <c>ClaudeDatouttrekk</c> bak <see cref="IDatouttrekk"/> uten ombygging.
/// </summary>
public sealed class FakeDatouttrekk : IDatouttrekk
{
    // «23. januar 2026» / «23 januar 2026».
    private static readonly Regex TekstDato = new(
        @"\b(\d{1,2})\.?\s+(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)\s+(\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // «23.01.2026» / «23/1-2026».
    private static readonly Regex NumeriskDato = new(
        @"\b(\d{1,2})[./](\d{1,2})[.\-/](\d{4})\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] Maaneder =
    [
        "januar", "februar", "mars", "april", "mai", "juni",
        "juli", "august", "september", "oktober", "november", "desember"
    ];

    public Task<Uttrekksresultat> TrekkUtAsync(string pdfTekst, int budsjettaar, CancellationToken ct = default)
    {
        var felter = new List<Uttrekksfelt>();
        var tekst = pdfTekst ?? string.Empty;

        // Tittel: første meningsfulle linje.
        var tittel = FoersteMeningsfulleLinje(tekst);
        if (tittel is not null)
        {
            felter.Add(new Uttrekksfelt
            {
                Felt = Uttrekksfelter.Tittel,
                TolketVerdi = tittel,
                Kildeutdrag = tittel,
                Konfidens = 0.8
            });
        }

        // Dato: første gjenkjennelige dato, med kildeutdrag rundt treffet.
        var (dato, utdrag) = FoersteDato(tekst);
        felter.Add(new Uttrekksfelt
        {
            Felt = Uttrekksfelter.Dato,
            TolketVerdi = dato?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Kildeutdrag = utdrag,
            Konfidens = dato is not null ? 0.85 : 0.2
        });

        // Budsjettår: årstall fra teksten, ellers hintet.
        var aar = Datogjenkjenning.ProvAarstall(tekst) ?? budsjettaar;
        felter.Add(new Uttrekksfelt
        {
            Felt = Uttrekksfelter.Budsjettaar,
            TolketVerdi = aar.ToString(CultureInfo.InvariantCulture),
            Kildeutdrag = utdrag,
            Konfidens = 0.6
        });

        return Task.FromResult(new Uttrekksresultat { Felter = felter });
    }

    private static string? FoersteMeningsfulleLinje(string tekst)
    {
        foreach (var linje in tekst.Split('\n'))
        {
            var t = linje.Trim();
            if (t.Length >= 10)
                return t.Length > 200 ? t[..200] : t;
        }
        return null;
    }

    private static (DateOnly? Dato, string? Utdrag) FoersteDato(string tekst)
    {
        var m = TekstDato.Match(tekst);
        if (m.Success)
        {
            var dag = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var maaned = Array.IndexOf(Maaneder, m.Groups[2].Value.ToLowerInvariant()) + 1;
            var aar = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return (TrygtDato(aar, maaned, dag), Utdrag(tekst, m.Index, m.Length));
        }

        m = NumeriskDato.Match(tekst);
        if (m.Success)
        {
            var dag = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var maaned = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var aar = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return (TrygtDato(aar, maaned, dag), Utdrag(tekst, m.Index, m.Length));
        }

        return (null, null);
    }

    private static DateOnly? TrygtDato(int aar, int maaned, int dag)
    {
        try { return new DateOnly(aar, maaned, dag); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>~60 tegns vindu rundt treffet, så administrator ser konteksten datoen kom fra.</summary>
    private static string Utdrag(string tekst, int indeks, int lengde)
    {
        var start = Math.Max(0, indeks - 30);
        var slutt = Math.Min(tekst.Length, indeks + lengde + 30);
        return Regex.Replace(tekst[start..slutt], @"\s+", " ").Trim();
    }
}
