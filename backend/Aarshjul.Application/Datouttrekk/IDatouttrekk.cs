namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Utbyttbart datouttrekksledd (designintervju 2026-06-19, samme prinsipp som <c>IKilde</c>):
/// tar PDF-tekst inn, leverer et strukturert per-felt-resultat ut (SYSTEMARKITEKTUR 3.2).
/// Provider/lokasjon (ekstern Claude API vs. Azure-vertet) er et IT-styringsspørsmål som byttes
/// bak dette grensesnittet uten ombygging. Selve modell-/API-kallet implementeres i Steg E.
/// </summary>
public interface IDatouttrekk
{
    /// <param name="pdfTekst">Rentekst hentet fra rundskrivets PDF.</param>
    /// <param name="budsjettaar">Budsjettåret konteksten gjelder (brukes til fornuftssjekk av datoer).</param>
    Task<Uttrekksresultat> TrekkUtAsync(string pdfTekst, int budsjettaar, CancellationToken ct = default);
}
