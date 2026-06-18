using System.Security.Claims;
using System.Text.Encodings.Web;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

/// <summary>
/// Starter web-appen i testmiljø: bytter Azure SQL mot SQLite in-memory og injiserer en
/// test-autentiseringsordning der rolle og grupper styres via request-headere. Lar oss
/// verifisere at selve HTTP-svaret fra API-et er synlighetsfiltrert.
/// </summary>
public class TestApplikasjon : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestApplikasjon()
    {
        // Bygg host (kjører ConfigureWebHost), opprett skjema og seed mot SQLite-tilkoblingen.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        Seed(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(_connection);
            services.AddDbContext<AppDbContext>((sp, o) =>
                o.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            });
        });
    }

    private static void Seed(AppDbContext db)
    {
        if (db.Synlighetsgrupper.Any())
        {
            return;
        }

        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        db.SaveChanges();

        LeggTil(db, "Kun FA", "FA");
        LeggTil(db, "FA og POL", "FA", "POL");
        LeggTil(db, "Kun FAG", "FAG");
        db.SaveChanges();
    }

    private static void LeggTil(AppDbContext db, string tittel, params string[] grupper)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = tittel,
            Dato = new DateOnly(2027, 6, 1),
            Budsjettaar = 2028,
            Kategori = Kategori.Budsjett,
            Status = FristStatus.Godkjent
        };
        foreach (var g in grupper)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        }
        db.Frister.Add(frist);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

/// <summary>
/// Test-autentisering: leser <c>X-Test-Rolle</c> og <c>X-Test-Grupper</c> (kommaseparert) fra
/// forespørselen og bygger en principal med de berikede claimsene. Uten rolle-header er
/// brukeren uautentisert (gir 401).
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Rolle", out var rolle))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-bruker"),
            new(Brukerclaims.Rolle, rolle.ToString())
        };

        if (Request.Headers.TryGetValue("X-Test-Grupper", out var grupper))
        {
            foreach (var g in grupper.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(Brukerclaims.Gruppe, g));
            }
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
