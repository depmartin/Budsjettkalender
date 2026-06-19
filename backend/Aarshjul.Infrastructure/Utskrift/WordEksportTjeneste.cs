using System.Globalization;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Aarshjul.Infrastructure.Utskrift;

/// <summary>
/// Bygger Word-dokumentet med Open XML SDK (ren .NET, ingen Office-avhengighet på serveren).
/// Dokumentet bærer en synlig topptekst generert fra det faktiske utvalgskriteriet (gruppe +
/// periode), slik at en utskrift ikke kan forveksles med en annen gruppes utvalg; «alt»-utskriften
/// merkes tydeligst som FIN-internt. Layouten følger FINs notatmal strukturelt (Notat-merke, tittel,
/// sidetall); den ekte .dotx-malen kobles på senere uten å endre dette grensesnittet.
/// </summary>
public sealed class WordEksportTjeneste : IWordEksport
{
    private static readonly CultureInfo Nb = CultureInfo.GetCultureInfo("nb-NO");

    public byte[] GenererFristdokument(Utskriftsforesporsel foresporsel, IReadOnlyList<FristDto> frister)
    {
        using var strøm = new MemoryStream();
        using (var dok = WordprocessingDocument.Create(strøm, WordprocessingDocumentType.Document))
        {
            var hoveddel = dok.AddMainDocumentPart();
            var body = new Body();

            body.Append(Avsnitt("Notat", fet: true));
            body.Append(Avsnitt(Tittel(foresporsel), fet: true, storrelse: 32));
            body.Append(Avsnitt(Topptekst(foresporsel)));

            if (foresporsel.ErAlt)
            {
                body.Append(Avsnitt("FIN-internt – fullt innsyn.", fet: true));
            }

            if (frister.Count == 0)
            {
                body.Append(Avsnitt("Ingen frister i utvalget."));
            }
            else
            {
                foreach (var aarsgruppe in frister.GroupBy(f => f.Budsjettaar).OrderBy(g => g.Key))
                {
                    body.Append(Avsnitt($"Budsjettår {aarsgruppe.Key}", fet: true, storrelse: 26));
                    foreach (var frist in aarsgruppe)
                    {
                        body.Append(Avsnitt($"{FormatDato(frist)} – {frist.Tittel} ({KategoriNavn(frist.Kategori)})"));
                        if (!string.IsNullOrWhiteSpace(frist.Notat))
                        {
                            body.Append(Avsnitt(frist.Notat!, kursiv: true));
                        }
                    }
                }
            }

            hoveddel.Document = new Document(body);
            hoveddel.Document.Save();
        }

        return strøm.ToArray();
    }

    private static string Tittel(Utskriftsforesporsel f)
        => f.ErAlt ? "Budsjettfrister – alle (fullt innsyn)" : $"Budsjettfrister – {f.GruppeEtikett}";

    private static string Topptekst(Utskriftsforesporsel f)
    {
        var gruppe = f.ErAlt ? "Alle frister (administrators fulle innsyn)" : $"Gruppe: {f.GruppeEtikett}";
        var periode = (f.FraDato, f.TilDato) switch
        {
            ({ } fra, { } til) => $"Periode: {fra.ToString("dd.MM.yyyy", Nb)}–{til.ToString("dd.MM.yyyy", Nb)}",
            ({ } fra, null) => $"Periode: fra {fra.ToString("dd.MM.yyyy", Nb)}",
            (null, { } til) => $"Periode: til {til.ToString("dd.MM.yyyy", Nb)}",
            _ => "Periode: hele tilgjengelige tidsrom"
        };
        return $"{gruppe}. {periode}.";
    }

    private static string FormatDato(FristDto frist)
    {
        if (frist.Datopresisjon == Datopresisjon.Maaned)
        {
            var maaned = frist.Dato.ToString("MMMM yyyy", Nb);
            var kvalifikator = frist.Datokvalifikator is { } kv ? $"{kv.ToString().ToLower(Nb)} " : "";
            return $"{kvalifikator}{maaned} (tentativ)";
        }

        return frist.Dato.ToString("dd.MM.yyyy", Nb);
    }

    private static string KategoriNavn(Kategori kategori) => kategori switch
    {
        Kategori.Budsjett => "Budsjett",
        Kategori.Gulbok => "Gul bok",
        Kategori.Regnskap => "Regnskap",
        _ => kategori.ToString()
    };

    private static Paragraph Avsnitt(string tekst, bool fet = false, bool kursiv = false, int? storrelse = null)
    {
        var egenskaper = new RunProperties();
        if (fet) egenskaper.Append(new Bold());
        if (kursiv) egenskaper.Append(new Italic());
        if (storrelse is { } s) egenskaper.Append(new FontSize { Val = s.ToString() }); // halv-punkt

        var run = new Run();
        if (egenskaper.HasChildren) run.Append(egenskaper);
        run.Append(new Text(tekst) { Space = SpaceProcessingModeValues.Preserve });

        return new Paragraph(run);
    }
}
