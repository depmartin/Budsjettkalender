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
    protected string? Lastefeil { get; private set; }

    /// <summary>Henter fristene for denne visningen, gitt brukerens synlighetskontekst.</summary>
    protected abstract Task<IReadOnlyList<FristDto>> HentAsync(ISynlighetskontekst ctx);

    protected override void OnInitialized() => Tilstand.Endret += VedEndret;

    protected override Task OnInitializedAsync() => LastInnAsync();

    // async void er nødvendig for et synkront event; feil fanges inne i LastInnAsync slik at
    // de aldri propagerer ut hit og river SignalR-kretsen.
    private async void VedEndret() => await InvokeAsync(LastInnAsync);

    protected async Task LastInnAsync()
    {
        try
        {
            var ctx = await Kontekstkilde.HentAsync();
            ErAdministrator = ctx.SerAlt;
            Frister = await HentAsync(ctx);
            // Tilgjengelige år hentes uavhengig av årsvalget, så filteret ikke kollapser.
            TilgjengeligeAar = await Lesing.HentTilgjengeligeBudsjettaarAsync(ctx, Tilstand.InkluderHistorikk);
            Lastefeil = null;
        }
        catch (Exception)
        {
            Frister = [];
            Lastefeil = "Kunne ikke laste frister akkurat nå. Prøv igjen.";
        }
        Lastet = true;
        StateHasChanged();
    }

    public void Dispose() => Tilstand.Endret -= VedEndret;
}
