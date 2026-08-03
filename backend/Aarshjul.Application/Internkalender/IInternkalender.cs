using Aarshjul.Domain;

namespace Aarshjul.Application.Internkalender;

/// <summary>En ansvarlig for et gjøremål: bruker-referanse (fra listen) eller fritekst.</summary>
public sealed record AnsvarligDto(string? BrukerId, string Navn);

/// <summary>En konkret runde i internkalenderens oversikt.</summary>
public sealed record RundeDto(
    Guid Id,
    Rundetype Rundetype,
    int? Aar,
    string Etikett,
    DateTimeOffset OpprettetTid,
    DateTimeOffset? SistSynkronisert,
    int AntallAktive,
    int AntallFullfoerte);

/// <summary>Et gjøremål slik det vises i en rundes aktiv-/ferdig-liste.</summary>
public sealed record GjoeremaalDto(
    Guid Id,
    Guid RundeId,
    string Tittel,
    string? Notat,
    Tidfestingstype Tidfestingstype,
    DateOnly? Sorteringsdag,
    bool VenterPaaAnker,
    string Tidsangivelse,
    GjoeremaalStatus Status,
    string? FullfoertAvNavn,
    DateTimeOffset? FullfoertTid,
    GjoeremaalOpphav Opphav,
    bool ManueltEndret,
    IReadOnlyList<AnsvarligDto> Ansvarlige);

/// <summary>Et gjøremål med rundekontekst — brukt i den personlige tverr-rundevisningen «Mine gjøremål».</summary>
public sealed record MittGjoeremaalDto(
    GjoeremaalDto Gjoeremaal,
    Rundetype Rundetype,
    int? Aar,
    string RundeEtikett);

/// <summary>
/// Ett forslag fra synkroniseringen av en konkret runde mot de generelle reglene (trinn 3):
/// legg til (ny regel), oppdater (regel endret) eller fjern (regel gjelder ikke lenger). Rører
/// aldri avhukede eller manuelt endrede gjøremål. Administrator godtar hvert forslag for seg.
/// </summary>
public sealed record SynkForslagDto(
    SynkHandling Handling,
    Guid? GjoeremaalId,
    Guid? RegelId,
    string Tittel,
    string Detalj);

/// <summary>Inndata fra gjøremål-skjemaet (opprett/rediger). Kun tittel er påkrevd.</summary>
public sealed class GjoeremaalInndata
{
    public string Tittel { get; set; } = "";
    public string? Notat { get; set; }
    public Tidfestingstype Tidfestingstype { get; set; } = Tidfestingstype.Ingen;
    public DateOnly? Dato { get; set; }
    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;
    public Datokvalifikator? Datokvalifikator { get; set; }
    public string? AnkerLoep { get; set; }
    public int AnkerOffsetDager { get; set; }
    public Rundeposisjon? Rundeposisjon { get; set; }
    public List<AnsvarligDto> Ansvarlige { get; set; } = [];
}

/// <summary>
/// Internkalenderen for SBR: en SBR-intern arbeidsliste atskilt fra de publiserte fristene, med
/// konkrete runder man legger inn og huker av gjøremål i. Alle handlinger er SBR-handlinger (policy
/// <c>ErSbr</c> håndheves i web-laget); dataene sendes aldri til en ikke-SBR-klient.
/// </summary>
public interface IInternkalender
{
    // --- Runder ---
    Task<IReadOnlyList<RundeDto>> HentRunderAsync(CancellationToken ct = default);
    Task<RundeDto?> HentRundeAsync(Guid rundeId, CancellationToken ct = default);

    /// <summary>Oppretter en tom konkret runde (uten regelgenerering). Feiler hvis runden finnes fra før.</summary>
    Task<Guid> OpprettRundeAsync(Rundetype type, int? aar, string? opprettetAv, CancellationToken ct = default);

    /// <summary>Henter (eller oppretter) den stående Øvrig-runden — én singleton uten år.</summary>
    Task<Guid> HentEllerOpprettOvrigAsync(string? opprettetAv, CancellationToken ct = default);

    /// <summary>
    /// Oppretter en konkret runde og fyller den med gjøremål generert fra de generelle reglene som
    /// gjelder rundetypen (trinn 2). Snapshot ved oppretting; feiler hvis runden finnes fra før
    /// (bruk synkronisering på en eksisterende runde). Øvrig kan ikke genereres.
    /// </summary>
    Task<Guid> GenererRundeAsync(Rundetype type, int aar, string? opprettetAv, CancellationToken ct = default);

    Task SlettRundeAsync(Guid rundeId, CancellationToken ct = default);

    // --- Gjøremål i en runde ---
    Task<IReadOnlyList<GjoeremaalDto>> HentGjoeremaalAsync(Guid rundeId, string? ansvarligBrukerId, CancellationToken ct = default);

    /// <summary>Hurtiginnlegging: oppretter et gjøremål med bare tittel (mangelfullt, kan fylles ut senere).</summary>
    Task<Guid> HurtiglaggAsync(Guid rundeId, string tittel, CancellationToken ct = default);

    Task<GjoeremaalInndata?> HentForRedigeringAsync(Guid gjoeremaalId, CancellationToken ct = default);

    /// <summary>Runden et gjøremål hører til (for retur-navigasjon). Null hvis gjøremålet ikke finnes.</summary>
    Task<Guid?> HentRundeIdForGjoeremaalAsync(Guid gjoeremaalId, CancellationToken ct = default);
    Task<Guid> OpprettGjoeremaalAsync(Guid rundeId, GjoeremaalInndata inndata, CancellationToken ct = default);
    Task OppdaterGjoeremaalAsync(Guid gjoeremaalId, GjoeremaalInndata inndata, CancellationToken ct = default);
    Task SlettGjoeremaalAsync(Guid gjoeremaalId, CancellationToken ct = default);

    // --- Avhuking ---
    Task FullfoerAsync(Guid gjoeremaalId, string brukerId, string brukerNavn, CancellationToken ct = default);
    Task GjenaapneAsync(Guid gjoeremaalId, CancellationToken ct = default);

    // --- Mine oppgaver på tvers av runder ---
    Task<IReadOnlyList<MittGjoeremaalDto>> HentMineAsync(string brukerId, CancellationToken ct = default);

    // --- Ansvarlig-liste (SBR-brukere) ---
    Task<IReadOnlyList<AnsvarligDto>> HentMuligeAnsvarligeAsync(CancellationToken ct = default);

    // --- Synkronisering mot de generelle reglene (trinn 3) ---

    /// <summary>Bygger forslagene til synkronisering av en runde mot dagens regler (ingen endring lagres).</summary>
    Task<IReadOnlyList<SynkForslagDto>> ForberedSynkAsync(Guid rundeId, CancellationToken ct = default);

    /// <summary>Utfører de godkjente synk-forslagene og oppdaterer «sist synkronisert».</summary>
    Task SynkroniserAsync(Guid rundeId, IReadOnlyList<SynkForslagDto> godkjente, CancellationToken ct = default);
}
