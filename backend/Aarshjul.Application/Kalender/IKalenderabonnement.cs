namespace Aarshjul.Application.Kalender;

/// <summary>
/// Forvalter delbare kalender-abonnementslenker (Endring 1). En administrator oppretter én lenke
/// per synlighetsgruppe (eller «alt»), kan skru dem av/på, og feed-endepunktet slår opp aktiv lenke
/// via token. All synlighetsfiltrering skjer fortsatt på server ut fra lenkens gruppe.
/// </summary>
public interface IKalenderabonnement
{
    /// <summary>Oppretter en ny, aktiv lenke for en gruppe (null = «alt») og returnerer den med token.</summary>
    Task<KalenderabonnementDto> OpprettAsync(string? gruppeKode, string opprettetAv, CancellationToken ct = default);

    /// <summary>Alle lenker (aktive og avskrudde) for admin-oversikten.</summary>
    Task<IReadOnlyList<KalenderabonnementDto>> HentAlleAsync(CancellationToken ct = default);

    /// <summary>Skrur en lenke av eller på (tilbakekalling uten sletting).</summary>
    Task SettAktivAsync(Guid id, bool aktiv, CancellationToken ct = default);

    /// <summary>Sletter en lenke permanent.</summary>
    Task SlettAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Slår opp en aktiv lenke via token for feed-endepunktet. Returnerer utvalgskriteriet
    /// (gruppe/«alt» + etikett) eller null hvis token er ukjent eller lenken er avskrudd.
    /// </summary>
    Task<Feedutvalg?> HentAktivtUtvalgAsync(string token, CancellationToken ct = default);
}

/// <summary>Én abonnementslenke slik admin-oversikten trenger den.</summary>
public record KalenderabonnementDto(
    Guid Id,
    string Token,
    string? GruppeKode,
    string Etikett,
    bool Aktiv,
    DateTime OpprettetTid)
{
    public bool ErAlt => GruppeKode is null;
}

/// <summary>Utvalgskriteriet en aktiv feed-token peker på.</summary>
public record Feedutvalg(string? GruppeKode, string Etikett)
{
    public bool ErAlt => GruppeKode is null;
}
