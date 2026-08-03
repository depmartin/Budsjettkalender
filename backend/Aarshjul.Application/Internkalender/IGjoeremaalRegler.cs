using Aarshjul.Domain;

namespace Aarshjul.Application.Internkalender;

/// <summary>En generell regel (mal) slik den vises i regel-oversikten.</summary>
public sealed record RegelDto(
    Guid Id,
    string Tittel,
    string? Notat,
    IReadOnlyList<Rundetype> Rundetyper,
    Tidfestingstype Tidfestingstype,
    string Tidsangivelse,
    IReadOnlyList<AnsvarligDto> Ansvarlige);

/// <summary>Inndata fra regel-skjemaet (opprett/rediger en generell regel). Årsuavhengig tidfesting.</summary>
public sealed class RegelInndata
{
    public string Tittel { get; set; } = "";
    public string? Notat { get; set; }

    /// <summary>Rundetypene regelen genereres inn i (minst én; «hver runde» = alle genererbare).</summary>
    public List<Rundetype> Rundetyper { get; set; } = [];

    public Tidfestingstype Tidfestingstype { get; set; } = Tidfestingstype.Ingen;
    public int? Maaned { get; set; }
    public int? Dag { get; set; }
    public int AarforskyvningJustering { get; set; }
    public Datokvalifikator? Datokvalifikator { get; set; }
    public string? AnkerLoep { get; set; }
    public int AnkerOffsetDager { get; set; }
    public Rundeposisjon? Rundeposisjon { get; set; }
    public List<AnsvarligDto> Ansvarlige { get; set; } = [];
}

/// <summary>
/// Forvalter de generelle reglene (malene) i internkalenderen. En regel beskriver et gjøremål som
/// genereres inn i én eller flere rundetypers konkrete planer. Kun SBR/administrator (policy i web).
/// </summary>
public interface IGjoeremaalRegler
{
    Task<IReadOnlyList<RegelDto>> HentAlleAsync(CancellationToken ct = default);
    Task<RegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default);
    Task<Guid> OpprettAsync(RegelInndata inndata, CancellationToken ct = default);
    Task OppdaterAsync(Guid id, RegelInndata inndata, CancellationToken ct = default);
    Task SlettAsync(Guid id, CancellationToken ct = default);
}
