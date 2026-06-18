namespace Aarshjul.Domain;

/// <summary>
/// In-app-varsel til en bruker (SYSTEMARKITEKTUR 3.7). Opprettes når administrator godkjenner
/// eller avviser et forslag. Eneste push-mekanisme mot bruker; ingen utgående e-post.
/// Tas i full bruk i Fase 2.
/// </summary>
public class Varsel
{
    public Guid Id { get; set; }

    /// <summary>Bruker-id varselet gjelder.</summary>
    public required string BrukerId { get; set; }

    public required string Tekst { get; set; }

    /// <summary>Valgfri begrunnelse ved avvisning.</summary>
    public string? Begrunnelse { get; set; }

    public bool Lest { get; set; }

    public DateTimeOffset Opprettet { get; set; } = DateTimeOffset.UtcNow;
}
