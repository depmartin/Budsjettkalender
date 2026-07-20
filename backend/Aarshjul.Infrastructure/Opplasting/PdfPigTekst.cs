using System.Text;
using Aarshjul.Application.Opplasting;
using UglyToad.PdfPig;

namespace Aarshjul.Infrastructure.Opplasting;

/// <summary>
/// Henter rentekst fra en PDF med PdfPig (ren .NET, ingen native avhengigheter). Rundskrivene er
/// tekstbaserte (kravdok. 4.4), så en sidevis tekstuttrekking er tilstrekkelig som input til
/// datouttrekket.
/// </summary>
public sealed class PdfPigTekst : IPdfTekst
{
    public string HentTekst(byte[] pdf)
    {
        if (pdf is null || pdf.Length == 0)
            return string.Empty;

        try
        {
            using var dok = PdfDocument.Open(pdf);
            var sb = new StringBuilder();
            foreach (var side in dok.GetPages())
            {
                sb.AppendLine(side.Text);
            }
            return sb.ToString().Trim();
        }
        catch
        {
            // Ikke en gyldig/lesbar PDF — pipelinen håndterer tom tekst som «kunne ikke lese».
            return string.Empty;
        }
    }
}
