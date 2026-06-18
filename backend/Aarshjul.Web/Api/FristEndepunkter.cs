using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;

namespace Aarshjul.Web.Api;

/// <summary>
/// Tynt lese-API over frister. Filtreres på server mot brukerens synlighet; svaret kan
/// dermed inspiseres direkte for å verifisere at en bruker aldri mottar frister vedkommende
/// ikke har rett til (SYSTEMARKITEKTUR 4).
/// </summary>
public static class FristEndepunkter
{
    public static void MapFristEndepunkter(this IEndpointRouteBuilder app)
    {
        var gruppe = app.MapGroup("/api/frister").RequireAuthorization();

        gruppe.MapGet("", async (HttpContext http, ISynlighetskontekst ctx, IFristlesing lesing, CancellationToken ct) =>
        {
            var frister = await lesing.HentSynligeAsync(ctx, LesFilter(http.Request.Query), ct);
            return Results.Ok(frister);
        });

        gruppe.MapGet("/landing", async (HttpContext http, ISynlighetskontekst ctx, IFristlesing lesing, TimeProvider klokke, CancellationToken ct) =>
        {
            var idag = DateOnly.FromDateTime(klokke.GetUtcNow().UtcDateTime);
            var frister = await lesing.HentLandingsutvalgAsync(ctx, idag, LesFilter(http.Request.Query), ct);
            return Results.Ok(frister);
        });
    }

    private static FristFilter LesFilter(IQueryCollection q)
    {
        var kategorier = q["kategori"]
            .Where(v => Enum.TryParse<Kategori>(v, ignoreCase: true, out _))
            .Select(v => Enum.Parse<Kategori>(v!, ignoreCase: true))
            .ToArray();

        var budsjettaar = q["budsjettaar"]
            .Where(v => int.TryParse(v, out _))
            .Select(v => int.Parse(v!))
            .ToArray();

        return new FristFilter
        {
            Kategorier = kategorier.Length > 0 ? kategorier : null,
            Budsjettaar = budsjettaar.Length > 0 ? budsjettaar : null,
            InkluderHistorikk = q["historikk"] == "true"
        };
    }
}
