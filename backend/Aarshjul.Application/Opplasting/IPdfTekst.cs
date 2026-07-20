namespace Aarshjul.Application.Opplasting;

/// <summary>
/// Henter rentekst fra en PDF. Abstrahert (samme prinsipp som <c>IKilde</c>/<c>IDatouttrekk</c>)
/// slik at pipelinen kan testes uten et ekte PDF-bibliotek. Implementasjon i Infrastructure.
/// </summary>
public interface IPdfTekst
{
    /// <summary>Trekker ut tekstinnholdet fra PDF-bytes; tom streng hvis dokumentet ikke har tekst.</summary>
    string HentTekst(byte[] pdf);
}
