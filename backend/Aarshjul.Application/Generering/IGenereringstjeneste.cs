using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>
/// Forespørsel om å generere et budsjettår. <paramref name="ManuelleAnkre"/> er løp → dato satt av
/// administrator i den to-trinns flyten: valgårssensitive ankre (typisk budsjettframleggelsen)
/// settes først, deretter beregnes resten fra dem (designintervju 2026-06-19).
/// </summary>
public sealed record GenereringsForespoersel(
    int Maalbudsjettaar,
    IReadOnlyDictionary<string, DateOnly>? ManuelleAnkre = null);

/// <summary>Oppsummering etter en genereringskjøring.</summary>
public sealed record GenereringsResultat(
    int Maalbudsjettaar,
    int AntallForslag,
    IReadOnlyList<string> Feil,
    IReadOnlyList<string> AnkreSomMaaSettesManuelt);

/// <summary>
/// Ett generert forslag slik det vises på generér-flaten: beregnet dato ved siden av fjorårets
/// faktiske dato, videreført synlighet, tentativ-/valgårsmarkering.
/// </summary>
public sealed record GenerertForslagDto
{
    public required Guid ForslagId { get; init; }
    public required string Tittel { get; init; }
    public string? Loep { get; init; }
    public Kategori Kategori { get; init; }
    public DateOnly? Dato { get; init; }
    public Datopresisjon Datopresisjon { get; init; }
    public bool Tentativ { get; init; }
    public bool Valgaarsflagg { get; init; }
    public IReadOnlyList<string> ForeslaattSynlighet { get; init; } = [];

    /// <summary>Fjorårets faktiske dato for samme løp (godkjent frist), om den finnes — for sammenligning.</summary>
    public DateOnly? FjoraarDato { get; init; }
}

/// <summary>
/// Genererer et kommende budsjettår fra årsmalen (kravdok. 5.4, SYSTEMARKITEKTUR 8). Leser
/// gjentaksreglene, beregner datoer, og legger forslagene (<see cref="ForslagType.Generert"/>) på
/// en egen gjennomgangsflate adskilt fra den løpende køen. Synligheten videreføres fra fjorårets
/// tilsvarende <b>godkjente</b> frist; ellers brukes synlighetsregelens default. POL videreføres
/// aldri stilltiende — det krever administrators aktive bekreftelse ved godkjenning.
/// Ingenting publiseres uten godkjenning. Administratorhandling (policy håndheves i web-laget).
/// </summary>
public interface IGenereringstjeneste
{
    /// <summary>Beregner og lagrer genererte forslag for målåret. Erstatter eventuelle tidligere
    /// åpne genererte forslag for samme år, slik at en ny kjøring ikke gir dubletter.</summary>
    Task<GenereringsResultat> GenererAsync(GenereringsForespoersel forespoersel, CancellationToken ct = default);

    /// <summary>Henter de åpne genererte forslagene for et målår, beriket med fjorårssammenligning.</summary>
    Task<IReadOnlyList<GenerertForslagDto>> HentGenererteAsync(int maalbudsjettaar, CancellationToken ct = default);

    /// <summary>Målbudsjettår som har åpne genererte forslag til gjennomgang.</summary>
    Task<IReadOnlyList<int>> HentAarMedForslagAsync(CancellationToken ct = default);
}
