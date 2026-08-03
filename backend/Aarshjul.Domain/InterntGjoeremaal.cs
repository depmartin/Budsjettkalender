namespace Aarshjul.Domain;

/// <summary>
/// Et internt gjøremål (oppgave) i en konkret runde. Kun tittel er påkrevd — resten kan fylles ut
/// senere («mangelfulle gjøremål»). Tidfestingen kan være konkret dato, tentativ måned, relativ til
/// et anker-løp, eller en rundeposisjon; <see cref="Sorteringsdag"/> er det entydige punktet
/// «aktiv»-visningen sorterer på (null = udatert, sorteres sist).
/// </summary>
public class InterntGjoeremaal
{
    public Guid Id { get; set; }

    public Guid RundeId { get; set; }
    public InternRunde? Runde { get; set; }

    public required string Tittel { get; set; }

    public string? Notat { get; set; }

    // --- Tidfesting ---

    public Tidfestingstype Tidfestingstype { get; set; } = Tidfestingstype.Ingen;

    /// <summary>Referansedato ved KonkretDato/TentativMaaned, eller oppløst ankerdato. Null ellers.</summary>
    public DateOnly? Dato { get; set; }

    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;

    public Datokvalifikator? Datokvalifikator { get; set; }

    /// <summary>Anker-løp (publisert frist) ved AnkerRelativ tidfesting.</summary>
    public string? AnkerLoep { get; set; }

    /// <summary>Forskyvning i dager fra ankeret (negativ = før, positiv = etter).</summary>
    public int AnkerOffsetDager { get; set; }

    public Rundeposisjon? Rundeposisjon { get; set; }

    /// <summary>Avledet entydig sorteringspunkt. Null når oppgaven er udatert eller venter på anker.</summary>
    public DateOnly? Sorteringsdag { get; set; }

    /// <summary>Sant når AnkerRelativ ikke lot seg oppløse (ankerfristen finnes ikke ennå).</summary>
    public bool VenterPaaAnker { get; set; }

    // --- Status / avhuking ---

    public GjoeremaalStatus Status { get; set; } = GjoeremaalStatus.Aktiv;

    /// <summary>Bruker-id som huket av (fra innlogget identitet). Null når ikke fullført.</summary>
    public string? FullfoertAvId { get; set; }

    /// <summary>Visningsnavn på den som huket av (alle i SBR ser hvem det var).</summary>
    public string? FullfoertAvNavn { get; set; }

    public DateTimeOffset? FullfoertTid { get; set; }

    // --- Opphav / synkronisering ---

    public GjoeremaalOpphav Opphav { get; set; } = GjoeremaalOpphav.Manuell;

    /// <summary>Regelen gjøremålet ble generert fra (for synkronisering). Null for manuelle.</summary>
    public Guid? GenerertFraRegelId { get; set; }

    /// <summary>Sant når et generert gjøremåls innhold/tidfesting er manuelt endret — sync rører det aldri.</summary>
    public bool ManueltEndret { get; set; }

    public ICollection<GjoeremaalAnsvarlig> Ansvarlige { get; set; } = new List<GjoeremaalAnsvarlig>();
}
