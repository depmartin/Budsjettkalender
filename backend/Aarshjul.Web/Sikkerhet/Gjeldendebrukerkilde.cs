using Aarshjul.Application.Brukere;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>
/// Henter den innloggede brukeren (id + navn + rolle + grupper) for Blazor-komponenter ut fra
/// autentiseringstilstanden i kretsen. Identitet kommer alltid fra token — aldri fra inntasting.
/// Brukes der en komponent trenger innsenderens id, f.eks. ved forslagsinnsending.
/// </summary>
public class Gjeldendebrukerkilde(AuthenticationStateProvider tilstand, IBrukeroppslag oppslag)
{
    public async Task<GjeldendeBruker> HentAsync(CancellationToken ct = default)
    {
        var state = await tilstand.GetAuthenticationStateAsync();
        return await oppslag.HentEllerOpprettAsync(state.User, ct);
    }
}
