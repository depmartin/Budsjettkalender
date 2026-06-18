using Aarshjul.Application.Synlighet;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>
/// Bygger synlighetskonteksten for Blazor-komponenter fra autentiseringstilstanden i kretsen
/// (der HttpContext ikke er tilgjengelig). Speiler det <see cref="HttpSynlighetskontekst"/>
/// gjør for HTTP-API-et.
/// </summary>
public class Synlighetskontekstkilde(AuthenticationStateProvider tilstand)
{
    public async Task<ISynlighetskontekst> HentAsync()
    {
        var state = await tilstand.GetAuthenticationStateAsync();
        return Synlighetskontekst.FraPrincipal(state.User);
    }
}
