namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Utbyttbart datouttrekksledd (designintervju 2026-06-19, samme prinsipp som <c>IKilde</c>):
/// tar PDF-tekst inn, leverer strukturerte per-felt-resultater ut (SYSTEMARKITEKTUR 3.2).
/// Provider/lokasjon (ekstern Claude API vs. Azure-vertet) er et IT-styringsspørsmål som byttes
/// bak dette grensesnittet uten ombygging. Selve modell-/API-kallet implementeres i Steg E.
/// </summary>
public interface IDatouttrekk
{
    /// <summary>
    /// Trekker ut <b>alle</b> fristene i et rundskriv. Ett rundskriv inneholder typisk flere
    /// frister med ulike datoer (kravdok. 4.4), så resultatet er <b>én <see cref="Uttrekksresultat"/>
    /// per frist</b> — ikke ett per dokument. Tom liste betyr at ingen frister ble gjenkjent (kilden
    /// legges likevel i køen som «uten dato» til manuell vurdering, jf. den konservative linjen).
    /// </summary>
    /// <param name="pdfTekst">Rentekst hentet fra rundskrivets PDF.</param>
    /// <param name="budsjettaar">Budsjettåret konteksten gjelder (brukes til fornuftssjekk av datoer).</param>
    Task<IReadOnlyList<Uttrekksresultat>> TrekkUtAsync(string pdfTekst, int budsjettaar, CancellationToken ct = default);
}
