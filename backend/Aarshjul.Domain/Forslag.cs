namespace Aarshjul.Domain;

/// <summary>
/// Et forslag i godkjenningskøen (SYSTEMARKITEKTUR 3.2). Skiller seg fra den publiserte
/// fristen fordi det bærer informasjon som ikke hører hjemme på fristen (uttrekksbevis,
/// referanse til endret frist, foreslått synlighet). Tas i full bruk i Fase 2/3.
/// </summary>
public class Forslag
{
    public Guid Id { get; set; }

    public ForslagType ForslagType { get; set; } = ForslagType.NyFrist;

    public Opphav Opphav { get; set; }

    /// <summary>Kilde (rundskriv) eller innsenders identitet, avhengig av opphav.</summary>
    public string? KildeEllerInnsender { get; set; }

    // Foreslåtte fristfelter:
    public required string Tittel { get; set; }
    public DateOnly? Dato { get; set; }
    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;
    public Datokvalifikator? Datokvalifikator { get; set; }
    public int Budsjettaar { get; set; }
    public Kategori Kategori { get; set; }
    public string? Loep { get; set; }
    public string? Notat { get; set; }

    /// <summary>Foreslått synlighet (gruppekoder), serialisert. Administrator beslutter endelig.</summary>
    public string ForeslaattSynlighet { get; set; } = "[]";

    public FristStatus Status { get; set; } = FristStatus.Forslag;

    /// <summary>For endringsforslag: den publiserte fristen som foreslås endret.</summary>
    public Guid? EndrerFristId { get; set; }

    /// <summary>Kobling til behandlet kildedokument for robotforslag.</summary>
    public Guid? DokumentId { get; set; }

    /// <summary>Per-felt uttrekksbevis på robotforslag (tolket verdi, kildeutdrag, konfidens).</summary>
    public ICollection<UttrekksBevis> UttrekksBevis { get; set; } = new List<UttrekksBevis>();
}
