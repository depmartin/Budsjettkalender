namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Henter rentekst ut av et opplastet dokument (PDF). Abstrahert som eget ledd slik at
/// PDF-biblioteket kan byttes, og slik at opplastingstjenesten kan testes uten ekte PDF-filer.
/// </summary>
public interface IPdftekst
{
    /// <summary>Trekker ut rentekst fra dokumentbytes. Kaster ved korrupt/ulesbart innhold.</summary>
    string HentTekst(byte[] innhold);
}
