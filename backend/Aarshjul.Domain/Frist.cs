namespace Aarshjul.Domain;

/// <summary>
/// Den publiserte enheten brukeren ser (kravdok. 3.1, SYSTEMARKITEKTUR 3.1).
/// Synlighet (<see cref="Synlighet"/>) håndheves alltid på server.
/// </summary>
public class Frist
{
    public Guid Id { get; set; }

    public required string Tittel { get; set; }

    /// <summary>Referansedato. For månedspresisjon kan dagen være vilkårlig i måneden.</summary>
    public DateOnly Dato { get; set; }

    /// <summary>Presisjonsnivå (utvidelse). Normalt <see cref="Datopresisjon.Dag"/>.</summary>
    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;

    /// <summary>Kvalifikator ved månedspresisjon (primo/medio/ultimo), ellers null.</summary>
    public Datokvalifikator? Datokvalifikator { get; set; }

    /// <summary>Avledet entydig sorteringspunkt, beregnes ved lagring (se <see cref="Datoberegning"/>).</summary>
    public DateOnly Sorteringsdag { get; set; }

    /// <summary>Budsjettåret fristen tilhører (ikke nødvendigvis = kalenderår for <see cref="Dato"/>).</summary>
    public int Budsjettaar { get; set; }

    public Kategori Kategori { get; set; }

    /// <summary>Funksjonsnavn/løp, f.eks. "rammefordeling". Identifiserer fristens funksjon, ikke rundskrivnummer.</summary>
    public string? Loep { get; set; }

    /// <summary>Opphav: rundskriv-id (f.eks. "R-4/2026") eller "manuell".</summary>
    public string? Kilde { get; set; }

    /// <summary>Kobling til behandlet kildedokument, hvis fristen stammer fra et rundskriv.</summary>
    public Guid? DokumentId { get; set; }

    public FristStatus Status { get; set; } = FristStatus.Godkjent;

    public Opphav Opphav { get; set; } = Opphav.Admin;

    /// <summary>Brukeridentifikasjon når <see cref="Opphav"/> = Bruker; hentes fra innlogget identitet.</summary>
    public string? ForeslaattAv { get; set; }

    public string? Notat { get; set; }

    /// <summary>Kobling til gjentaksregel hvis fristen er årlig tilbakevendende.</summary>
    public Guid? GjentaRegelId { get; set; }

    /// <summary>Settet av synlighetsgrupper fristen er synlig for (koblingstabell).</summary>
    public ICollection<FristSynlighet> Synlighet { get; set; } = new List<FristSynlighet>();

    /// <summary>Beregner og setter <see cref="Sorteringsdag"/> ut fra dato, presisjon og kvalifikator.</summary>
    public void OppdaterSorteringsdag()
        => Sorteringsdag = Datoberegning.Sorteringsdag(Dato, Datopresisjon, Datokvalifikator);
}
