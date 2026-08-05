namespace Aarshjul.Application.Sidetekster;

/// <summary>
/// Leser og lagrer redigerbare flate-tekster (nøkkel → tekst). Lesing er åpen for alle innloggede
/// (teksten vises på flatene); lagring er en administratorhandling (policy håndheves i web-laget).
/// </summary>
public interface ISidetekster
{
    /// <summary>Lagret tekst for nøkkelen, eller null hvis administrator ikke har overstyrt standardteksten.</summary>
    Task<string?> HentAsync(string nokkel, CancellationToken ct = default);

    /// <summary>Lagrer (eller nullstiller ved tom tekst) administrators tekst for nøkkelen.</summary>
    Task LagreAsync(string nokkel, string? tekst, CancellationToken ct = default);
}
