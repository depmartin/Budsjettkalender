using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;
using Aarshjul.Web.Sikkerhet;

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

        // Word-utskrift (kravdok. kap. 8): utvalget følger den valgte gruppens faktiske tilgang via
        // samme server-side synlighetsfilter («se som rolle»); «alt» gir administrators fulle innsyn.
        app.MapGet("/api/eksport/word", EksporterWord).RequireAuthorization(Autorisasjon.ErAdministrator);
    }

    private static async Task<IResult> EksporterWord(
        HttpContext http, IFristlesing lesing, IWordEksport eksport, IGruppetjeneste grupper, CancellationToken ct)
    {
        var q = http.Request.Query;
        var alt = q["alt"] == "true";
        var gruppeKode = q["gruppe"].FirstOrDefault();
        DateOnly? fra = DateOnly.TryParse(q["fra"].ToString(), out var f) ? f : null;
        DateOnly? til = DateOnly.TryParse(q["til"].ToString(), out var t) ? t : null;

        ISynlighetskontekst ctx;
        string etikett;
        if (alt)
        {
            ctx = new Synlighetskontekst(serAlt: true, grupper: []);
            etikett = "alle";
        }
        else if (!string.IsNullOrWhiteSpace(gruppeKode))
        {
            ctx = Synlighetskontekst.ForGruppe(gruppeKode);
            var aktive = await grupper.HentAktiveAsync(ct);
            etikett = aktive.FirstOrDefault(g => g.Kode == gruppeKode)?.Navn ?? gruppeKode;
        }
        else
        {
            return Results.BadRequest("Velg en synlighetsgruppe eller «alt».");
        }

        // Periodevinduet styrer utvalget; historikk tas med så fortidige frister i vinduet ikke faller bort.
        var filter = new FristFilter { FraDato = fra, TilDato = til, InkluderHistorikk = true };
        var frister = await lesing.HentSynligeAsync(ctx, filter, ct);

        var foresporsel = new Utskriftsforesporsel(alt ? null : gruppeKode, etikett, fra, til, alt);
        var bytes = eksport.GenererFristdokument(foresporsel, frister);

        var filnavnsdel = alt ? "alle" : gruppeKode!;
        var filnavn = $"frister-{filnavnsdel}-{fra:yyyyMMdd}_{til:yyyyMMdd}.docx";
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", filnavn);
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
