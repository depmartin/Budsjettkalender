namespace Aarshjul.Application.Varsler;

/// <summary>Et varsel slik brukeren ser det i innboksen.</summary>
public sealed record VarselDto(Guid Id, string Tekst, string? Begrunnelse, bool Lest, DateTimeOffset Opprettet);

/// <summary>
/// In-app-varsler til bidragsytere (SYSTEMARKITEKTUR 3.7, BRUKERHISTORIER 3.3). Eneste
/// push-mekanisme mot bruker — ingen utgående e-post. Varsler opprettes når administrator
/// godkjenner eller avviser et forslag (i godkjenningskøen).
/// </summary>
public interface IVarseltjeneste
{
    Task<IReadOnlyList<VarselDto>> HentForBrukerAsync(string brukerId, CancellationToken ct = default);

    Task<int> AntallUlesteAsync(string brukerId, CancellationToken ct = default);

    /// <summary>Markerer ett varsel som lest. Eierskap kontrolleres (kan kun lese egne varsler).</summary>
    Task MarkerLestAsync(Guid varselId, string brukerId, CancellationToken ct = default);

    Task MarkerAlleLestAsync(string brukerId, CancellationToken ct = default);
}
