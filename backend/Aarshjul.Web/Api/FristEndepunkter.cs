using System.Text.Json;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Kalender;
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

        // Kalender-eksport (.ics): samme utvalg som Word, men som iCalendar-fil til import i Outlook.
        app.MapGet("/api/eksport/ics", EksporterIcs).RequireAuthorization(Autorisasjon.ErAdministrator);

        // JSON-«database» over alle frister (endring #2): full nedlasting til senere import.
        // Kun administrator; inneholder FIN-interne frister, så ingen synlighetsfiltrering her.
        app.MapGet("/api/eksport/frister-json", EksporterJson).RequireAuthorization(Autorisasjon.ErAdministrator);

        // JSON-«database» over alle årsmaler (gjentaksregler). Kun administrator.
        app.MapGet("/api/eksport/maler-json", EksporterMalerJson).RequireAuthorization(Autorisasjon.ErAdministrator);
    }

    /// <summary>Delte JSON-innstillinger for frist-databasen (enum som navn, innrykk for lesbarhet).</summary>
    public static readonly JsonSerializerOptions DatabaseJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static async Task<IResult> EksporterJson(IFristDatautveksling data, CancellationToken ct)
    {
        var database = await data.EksporterAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(database, DatabaseJson);
        var filnavn = $"frister-database-{database.EksportertTid:yyyyMMdd-HHmmss}.json";
        return Results.File(bytes, "application/json", filnavn);
    }

    private static async Task<IResult> EksporterMalerJson(IMalDatautveksling data, CancellationToken ct)
    {
        var database = await data.EksporterAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(database, DatabaseJson);
        var filnavn = $"aarsmaler-database-{database.EksportertTid:yyyyMMdd-HHmmss}.json";
        return Results.File(bytes, "application/json", filnavn);
    }

    private static async Task<IResult> EksporterWord(
        HttpContext http, IFristlesing lesing, IWordEksport eksport, IGruppetjeneste grupper, CancellationToken ct)
    {
        var (utvalg, feil) = await ByggUtvalgAsync(http, lesing, grupper, ct);
        if (utvalg is null) return feil!;

        var bytes = eksport.GenererFristdokument(utvalg.Foresporsel, utvalg.Frister);
        var filnavn = $"frister-{utvalg.Filnavnsdel}.docx";
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", filnavn);
    }

    private static async Task<IResult> EksporterIcs(
        HttpContext http, IFristlesing lesing, IKalenderEksport eksport, IGruppetjeneste grupper, CancellationToken ct)
    {
        var (utvalg, feil) = await ByggUtvalgAsync(http, lesing, grupper, ct);
        if (utvalg is null) return feil!;

        var bytes = eksport.GenererIcs(utvalg.Foresporsel, utvalg.Frister);
        var filnavn = $"frister-{utvalg.Filnavnsdel}.ics";
        return Results.File(bytes, "text/calendar; charset=utf-8", filnavn);
    }

    /// <summary>Ferdig utvalg (kriterium + synlighetsfiltrerte frister) klart for en av eksportformene.</summary>
    private sealed record Utvalg(Utskriftsforesporsel Foresporsel, IReadOnlyList<FristDto> Frister, string Filnavnsdel);

    /// <summary>
    /// Delt utvalgsbygging for Word- og kalendereksporten: leser gruppe/«alt» + periode fra spørringen,
    /// bygger synlighetskonteksten («se som rolle» / fullt innsyn), og henter fristene gjennom det samme
    /// server-side synlighetsfilteret. Slik gir begge formater nøyaktig samme sett.
    /// </summary>
    private static async Task<(Utvalg? utvalg, IResult? feil)> ByggUtvalgAsync(
        HttpContext http, IFristlesing lesing, IGruppetjeneste grupper, CancellationToken ct)
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
            return (null, Results.BadRequest("Velg en synlighetsgruppe eller «alt»."));
        }

        // Periodevinduet styrer utvalget; historikk tas med så fortidige frister i vinduet ikke faller bort.
        var filter = new FristFilter { FraDato = fra, TilDato = til, InkluderHistorikk = true };
        var frister = await lesing.HentSynligeAsync(ctx, filter, ct);

        var foresporsel = new Utskriftsforesporsel(alt ? null : gruppeKode, etikett, fra, til, alt);
        var filnavnsdel = $"{(alt ? "alle" : gruppeKode!)}-{fra:yyyyMMdd}_{til:yyyyMMdd}";
        return (new Utvalg(foresporsel, frister, filnavnsdel), null);
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
