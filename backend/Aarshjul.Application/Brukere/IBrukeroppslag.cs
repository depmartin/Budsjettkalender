using System.Security.Claims;
using Aarshjul.Domain;

namespace Aarshjul.Application.Brukere;

/// <summary>
/// Slår opp (og oppretter/oppdaterer) den innloggede brukeren ut fra Entra-token-claims, og
/// utleder synlighetsgrupper. Identitet og navn hentes alltid fra token — aldri fra manuell
/// inntasting (kravdok. 3.5).
/// </summary>
public interface IBrukeroppslag
{
    Task<GjeldendeBruker> HentEllerOpprettAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}

/// <summary>Den innloggede brukeren slik resten av løsningen trenger den.</summary>
public record GjeldendeBruker(string Id, string Navn, Funksjonsrolle Funksjonsrolle, IReadOnlyList<string> Grupper)
{
    public bool ErAdministrator => Funksjonsrolle == Funksjonsrolle.Administrator;
}
