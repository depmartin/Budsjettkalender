using Aarshjul.Application.Frister;
using Aarshjul.Domain;

namespace Aarshjul.Web.Visning;

/// <summary>Felles formatering for visningene: ærlig dato-/tentativtekst og fargekoding på budsjettår.</summary>
public static class Visningformat
{
    private static readonly string[] Maaneder =
    [
        "januar", "februar", "mars", "april", "mai", "juni",
        "juli", "august", "september", "oktober", "november", "desember"
    ];

    /// <summary>Viser dagspresise frister som dato, tentative som måned (evt. primo/medio/ultimo) — aldri en falsk dag.</summary>
    public static string FormatDato(FristDto f)
    {
        var maaned = Maaneder[f.Dato.Month - 1];
        if (f.Datopresisjon == Datopresisjon.Dag)
        {
            var klokke = f.Klokkeslett is { } kl ? $" kl. {kl:HH\\:mm}" : "";
            return $"{f.Dato.Day}. {maaned} {f.Dato.Year}{klokke}";
        }

        var kvalifikator = f.Datokvalifikator switch
        {
            Datokvalifikator.Primo => "primo ",
            Datokvalifikator.Medio => "medio ",
            Datokvalifikator.Ultimo => "ultimo ",
            _ => ""
        };
        return $"{kvalifikator}{maaned} {f.Dato.Year}";
    }

    /// <summary>CSS-klasse for fargekoding på budsjettår, slik at overlappende løp skilles (kravdok. 7).</summary>
    public static string FargeklasseForAar(int budsjettaar) => $"aar-farge-{Fargeindeks(budsjettaar)}";

    /// <summary>FIN-datapaletten for budsjettår (samme rekkefølge som <see cref="FargeklasseForAar"/>-klassene),
    /// slik at f.eks. årshjulets SVG-punkter matcher kort-kant og filterprikk.</summary>
    public static string FargeForAar(int budsjettaar) => Aarfarger[Fargeindeks(budsjettaar)];

    private static readonly string[] Aarfarger =
        ["#181c62", "#4156a6", "#5b91cc", "#f15d61", "#97499c", "#008286"];

    private static int Fargeindeks(int budsjettaar) => ((budsjettaar % 6) + 6) % 6;

    public static string KategoriNavn(Kategori kategori) => kategori switch
    {
        Kategori.Budsjett => "Budsjett",
        Kategori.Gulbok => "Gul bok",
        Kategori.Regnskap => "Regnskap",
        _ => kategori.ToString()
    };
}
