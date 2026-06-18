using Aarshjul.Application.Brukere;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>
/// Bygger synlighetskonteksten for den innloggede brukeren fra claims (satt av
/// <see cref="BrukerClaimsTransformation"/>). Administrator (<see cref="ISynlighetskontekst.SerAlt"/>)
/// ser alt; øvrige ser kun frister som matcher gruppene i claims.
/// </summary>
public sealed class HttpSynlighetskontekst(IHttpContextAccessor accessor) : ISynlighetskontekst
{
    public bool SerAlt =>
        accessor.HttpContext?.User.FindFirst(Brukerclaims.Rolle)?.Value
        == nameof(Funksjonsrolle.Administrator);

    public IReadOnlyCollection<string> Grupper =>
        accessor.HttpContext?.User.FindAll(Brukerclaims.Gruppe).Select(c => c.Value).ToArray()
        ?? [];
}
