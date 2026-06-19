using System.Security.Claims;
using System.Text;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Authentication;

namespace Aarshjul.Web.Demo;

/// <summary>
/// Dev-innlogging for demo-modus (miljø <c>Demo</c>): velg en persona for å logge inn med en
/// cookie uten Entra. Funksjonsrolle og synlighetsgrupper hentes fra databasen av den vanlige
/// claims-transformasjonen, slik at autorisering og synlighetsfiltrering oppfører seg nøyaktig
/// som i produksjon — kun identitetskilden er byttet ut. Mappes aldri utenfor Demo.
/// </summary>
public static class DemoEndepunkter
{
    public static void MapDemoEndepunkter(this IEndpointRouteBuilder app)
    {
        app.MapGet("/demo", (HttpContext ctx) =>
        {
            var innlogget = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.Identity!.Name : null;
            return Results.Content(ByggSide(innlogget), "text/html; charset=utf-8");
        });

        app.MapGet("/demo/logg-inn/{id}", async (string id, HttpContext ctx) =>
        {
            var persona = Demodata.Personaer.FirstOrDefault(p => p.Id == id);
            if (persona.Id is null)
            {
                return Results.Redirect("/demo");
            }

            var identitet = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, persona.Id),
                new Claim(ClaimTypes.Name, persona.Navn)
            ], "Demo");

            await ctx.SignInAsync("Demo", new ClaimsPrincipal(identitet));
            return Results.Redirect("/");
        });

        app.MapGet("/demo/logg-ut", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync("Demo");
            return Results.Redirect("/demo");
        });
    }

    private static string ByggSide(string? innlogget)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!doctype html><html lang="no"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Demo-innlogging – Årshjul for budsjettfrister</title>
            <style>
              body { font-family: system-ui, sans-serif; max-width: 640px; margin: 3rem auto; padding: 0 1rem; color: #1a1a1a; }
              h1 { font-size: 1.4rem; } p { color: #555; }
              .persona { display: block; padding: 0.8rem 1rem; margin: 0.5rem 0; border: 1px solid #ccc; border-radius: 8px; text-decoration: none; color: #1a1a1a; }
              .persona:hover { background: #f0f4f8; border-color: #1f6feb; }
              .banner { background: #fff3cd; color: #856404; padding: 0.6rem 1rem; border-radius: 8px; margin-bottom: 1.5rem; }
              .gjeldende { background: #e7f5e7; padding: 0.6rem 1rem; border-radius: 8px; }
              a.app { font-weight: 600; }
            </style></head><body>
            <div class="banner">Demo-modus — lokal kjøring uten Entra/Azure SQL. Ikke ekte data.</div>
            <h1>Velg hvem du vil være</h1>
            <p>Innlogging skjer normalt med departementenes SSO (Entra ID). I demo velger du en persona for å oppleve de ulike rollene og synlighetsgruppene.</p>
            """);

        if (innlogget is not null)
        {
            sb.Append($"""<p class="gjeldende">Innlogget som <strong>{Html(innlogget)}</strong> — <a href="/">gå til appen</a> · <a href="/demo/logg-ut">logg ut</a></p>""");
        }

        foreach (var p in Demodata.Personaer)
        {
            sb.Append($"""<a class="persona" href="/demo/logg-inn/{Html(p.Id)}"><strong>{Html(p.Navn)}</strong><br><small>{p.Rolle} · grupper: {Html(string.Join(", ", p.Grupper))}</small></a>""");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Html(string s) => System.Net.WebUtility.HtmlEncode(s);
}
