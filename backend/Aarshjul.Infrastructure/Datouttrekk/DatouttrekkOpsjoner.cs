namespace Aarshjul.Infrastructure.Datouttrekk;

/// <summary>
/// Konfigurasjon for det ekte datouttrekket (Claude). Seksjon <c>Datouttrekk</c> i appsettings,
/// eller miljøvariabelen <c>ANTHROPIC_API_KEY</c> for nøkkelen. Er nøkkelen tom, brukes
/// <see cref="FakeDatouttrekk"/> i stedet (registreres i Program.cs).
/// </summary>
public sealed class DatouttrekkOpsjoner
{
    public const string Seksjon = "Datouttrekk";

    /// <summary>API-nøkkel for Claude. Tom → fake datouttrekk brukes.</summary>
    public string? ApiNokkel { get; set; }

    /// <summary>Modell-id. Default er nyeste kapable Opus.</summary>
    public string Modell { get; set; } = "claude-opus-4-8";

    /// <summary>Grunn-URL for API-et (byttbar hvis uttrekket skal gå mot en Azure-vertet Claude).</summary>
    public string BasisUrl { get; set; } = "https://api.anthropic.com";
}
