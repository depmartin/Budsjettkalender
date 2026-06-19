using Aarshjul.Application.Frister;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Utskrift;
using DocumentFormat.OpenXml.Packaging;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer Word-generatoren (Steg K): dokumentet bygges som en gyldig .docx med en topptekst
/// som speiler utvalgskriteriet (gruppe + periode), og «alt»-varianten merkes FIN-internt.
/// </summary>
public class WordEksportTester
{
    private static FristDto Frist(string tittel, DateOnly dato, int budsjettaar, Kategori kategori = Kategori.Budsjett)
        => new(Guid.NewGuid(), tittel, dato, Datopresisjon.Dag, null, dato, budsjettaar, kategori,
            null, null, null, null, FristStatus.Godkjent, []);

    private static string LesTekst(byte[] docx)
    {
        using var strøm = new MemoryStream(docx);
        using var dok = WordprocessingDocument.Open(strøm, false);
        return dok.MainDocumentPart!.Document.Body!.InnerText;
    }

    [Fact]
    public void Genererer_gyldig_docx_med_topptekst_og_frister()
    {
        var tjeneste = new WordEksportTjeneste();
        var foresporsel = new Utskriftsforesporsel("FA", "Finansavdelingen",
            new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31), ErAlt: false);
        var frister = new List<FristDto>
        {
            Frist("Hovedbudsjettskriv", new DateOnly(2027, 3, 15), 2028),
            Frist("Gul bok-innlevering", new DateOnly(2027, 9, 1), 2028, Kategori.Gulbok)
        };

        var docx = tjeneste.GenererFristdokument(foresporsel, frister);

        // Gyldig .docx er en zip — starter med «PK».
        Assert.True(docx.Length > 0);
        Assert.Equal((byte)'P', docx[0]);
        Assert.Equal((byte)'K', docx[1]);

        var tekst = LesTekst(docx);
        Assert.Contains("Finansavdelingen", tekst);
        Assert.Contains("01.01.2027", tekst);
        Assert.Contains("31.12.2027", tekst);
        Assert.Contains("Budsjettår 2028", tekst);
        Assert.Contains("Hovedbudsjettskriv", tekst);
        Assert.Contains("Gul bok-innlevering", tekst);
    }

    [Fact]
    public void Alt_variant_merkes_FIN_internt()
    {
        var tjeneste = new WordEksportTjeneste();
        var foresporsel = new Utskriftsforesporsel(null, "alle", null, null, ErAlt: true);

        var tekst = LesTekst(tjeneste.GenererFristdokument(foresporsel, []));

        Assert.Contains("FIN-internt", tekst);
        Assert.Contains("Ingen frister i utvalget", tekst);
    }
}
