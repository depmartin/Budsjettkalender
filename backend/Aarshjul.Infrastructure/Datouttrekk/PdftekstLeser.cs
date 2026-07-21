using System.Text;
using Aarshjul.Application.Datouttrekk;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Henter rentekst ut av en PDF med PdfPig (ren .NET, ingen ekstern avhengighet). Bruker
/// innholds-ordnet tekstuttrekk slik at linjer/avsnitt bevares for segmentering i uttrekket.
/// </summary>
public sealed class PdftekstLeser : IPdftekst
{
    public string HentTekst(byte[] innhold)
    {
        using var dok = PdfDocument.Open(innhold);
        var sb = new StringBuilder();
        foreach (var side in dok.GetPages())
        {
            sb.AppendLine(ContentOrderTextExtractor.GetText(side));
        }
        return sb.ToString();
    }
}
