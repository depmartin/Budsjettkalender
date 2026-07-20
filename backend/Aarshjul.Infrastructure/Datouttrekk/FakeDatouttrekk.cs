using System.Globalization;
using System.Text.RegularExpressions;
using Aarshjul.Application.Datouttrekk;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Deterministisk erstatning for det ekte språkmodell-uttrekket, brukt i utvikling/test og mens
/// den endelige Claude-provideren (ekstern API vs. Azure-vertet) er en åpen IT-avklaring
/// (kravdok. kap. 12). Finner <b>alle</b> datoene i teksten og gir én <see cref="Uttrekksresultat"/>
/// per frist, slik at et rundskriv med mange frister blir mange forslag i køen. Byttes ut mot en
/// <c>ClaudeDatouttrekk</c> bak <see cref="IDatouttrekk"/> uten ombygging; den ekte modellen tolker
/// tidsplan-seksjonen langt mer presist enn dette regelbaserte stand-in-et.
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

    public Task<IReadOnlyList<Uttrekksresultat>> TrekkUtAsync(
        string pdfTekst, int budsjettaar, CancellationToken ct = default)
    {
        var tekst = pdfTekst ?? string.Empty;
        var aar = Datogjenkjenning.ProvAarstall(tekst) ?? budsjettaar;

        // Finn alle datoforekomster (begge formater), i tekstrekkefølge.
        var treff = new List<(int Indeks, int Lengde, DateOnly? Dato)>();
        foreach (Match m in TekstDato.Matches(tekst))
        {
            var dag = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var maaned = Array.IndexOf(Maaneder, m.Groups[2].Value.ToLowerInvariant()) + 1;
            var å = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            treff.Add((m.Index, m.Length, TrygtDato(å, maaned, dag)));
        }
        foreach (Match m in NumeriskDato.Matches(tekst))
        {
            var dag = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var maaned = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var å = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            treff.Add((m.Index, m.Length, TrygtDato(å, maaned, dag)));
        }

        var resultater = new List<Uttrekksresultat>();
        var setteNokler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in treff.OrderBy(x => x.Indeks))
        {
            if (t.Dato is null)
                continue;

            var (tittel, utdrag) = SetningRundt(tekst, t.Indeks, t.Lengde);

            // Unngå duplikater når samme dato + oppgave nevnes flere steder.
            var nokkel = $"{t.Dato:yyyy-MM-dd}|{tittel}";
            if (!setteNokler.Add(nokkel))
                continue;

            resultater.Add(new Uttrekksresultat
            {
                Felter =
                [
                    new Uttrekksfelt
                    {
                        Felt = Uttrekksfelter.Tittel,
                        TolketVerdi = tittel,
                        Kildeutdrag = utdrag,
                        Konfidens = 0.75
                    },
                    new Uttrekksfelt
                    {
                        Felt = Uttrekksfelter.Dato,
                        TolketVerdi = t.Dato.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Kildeutdrag = utdrag,
                        Konfidens = 0.85
                    },
                    new Uttrekksfelt
                    {
                        Felt = Uttrekksfelter.Budsjettaar,
                        TolketVerdi = aar.ToString(CultureInfo.InvariantCulture),
                        Kildeutdrag = utdrag,
                        Konfidens = 0.6
                    }
                ]
            });
        }

        return Task.FromResult<IReadOnlyList<Uttrekksresultat>>(resultater);
    }

    private static DateOnly? TrygtDato(int aar, int maaned, int dag)
    {
        try { return new DateOnly(aar, maaned, dag); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Finner setningen datoen står i. Tittelen blir teksten fram til datoen (oppgavebeskrivelsen),
    /// f.eks. «Frist for innsending av satsingsforslag»; kildeutdraget er hele setningen.
    /// </summary>
    private static (string Tittel, string Utdrag) SetningRundt(string tekst, int indeks, int lengde)
    {
        var start = indeks;
        while (start > 0 && tekst[start - 1] is not ('.' or '\n' or ';' or ':'))
            start--;
        var slutt = indeks + lengde;
        while (slutt < tekst.Length && tekst[slutt] is not ('.' or '\n' or ';'))
            slutt++;

        var setning = Rens(tekst[start..slutt]);
        var foerDato = Rens(tekst[start..indeks]).TrimEnd(' ', ',', '-', '–');

        var tittel = foerDato.Length >= 8 ? foerDato : setning;
        return (Kutt(tittel, 200), Kutt(setning, 300));
    }

    private static string Rens(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    private static string Kutt(string s, int maks) => s.Length > maks ? s[..maks] : s;
}
