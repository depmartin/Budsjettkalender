using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Web.Sikkerhet;
using Aarshjul.Web.Visning;
using Microsoft.AspNetCore.Components;

namespace Aarshjul.Web.Komponenter;

/// <summary>
/// Basis for de tre visningene. Henter synlige frister via det felles synlighetsfilteret,
/// reagerer på endringer i det delte filteret, og eksponerer tilgjengelige budsjettår og om
/// brukeren er administrator (for gruppemerking).
/// </summary>
public abstract class Visningsside : ComponentBase, IDisposable
{
    [Inject] protected Synlighetskontekstkilde Kontekstkilde { get; set; } = default!;
    [Inject] protected IFristlesing Lesing { get; set; } = default!;
    [Inject] protected Visningstilstand Tilstand { get; set; } = default!;

    protected IReadOnlyList<FristDto> Frister { get; private set; } = [];
    protected IReadOnlyList<int> TilgjengeligeAar { get; private set; } = [];
    protected bool ErAdministrator { get; private set; }
    protected bool Lastet { get; private set; }

    /// <summary>Henter fristene for denne visningen, gitt brukerens synlighetskontekst.</summary>
    protected abstract Task<IReadOnlyList<FristDto>> HentAsync(ISynlighetskontekst ctx);

    protected override void OnInitialized() => Tilstand.Endret += VedEndret;

    protected override Task OnInitializedAsync() => LastInnAsync();

    private async void VedEndret() => await InvokeAsync(LastInnAsync);

    protected async Task LastInnAsync()
    {
        var ctx = await Kontekstkilde.HentAsync();
        ErAdministrator = ctx.SerAlt;
        Frister = await HentAsync(ctx);
        TilgjengeligeAar = Frister.Select(f => f.Budsjettaar).Distinct().OrderBy(a => a).ToList();
        Lastet = true;
        StateHasChanged();
    }

    public void Dispose() => Tilstand.Endret -= VedEndret;
}
