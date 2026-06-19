using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Application.Utskrift;
using Aarshjul.Application.Varsler;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Infrastructure.Brukerforslag;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Godkjenningsko;
using Aarshjul.Infrastructure.Grupper;
using Aarshjul.Infrastructure.Utskrift;
using Aarshjul.Infrastructure.Varsler;
using Aarshjul.Web.Api;
using Aarshjul.Web.Components;
using Aarshjul.Web.Demo;
using Aarshjul.Web.Sikkerhet;
using Aarshjul.Web.Visning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);
var erTesting = builder.Environment.IsEnvironment("Testing");
// Demo-modus: lokal kjøring uten Azure SQL/Entra (SQLite + dev-innlogging + demo-data).
// Rører ikke produksjonsstien. Aktiveres med ASPNETCORE_ENVIRONMENT=Demo.
var erDemo = builder.Environment.IsEnvironment("Demo");
var brukEntra = !erTesting && !erDemo;

// --- Database. Produksjon: Azure SQL. Demo: SQLite-fil. Test: registreres av testene. ---
if (erDemo)
{
    // Tom streng i appsettings teller som «ikke satt» → fil-DB (en tom DSN gir ellers en
    // privat temp-database per tilkobling, så skjemaet «forsvinner» mellom kall).
    var demoConn = builder.Configuration.GetConnectionString("Aarshjul");
    if (string.IsNullOrWhiteSpace(demoConn))
    {
        demoConn = "Data Source=aarshjul-demo.db";
    }
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(demoConn));
}
else if (!erTesting)
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("Aarshjul")
            ?? "Server=(localdb)\\mssqllocaldb;Database=Aarshjul;Trusted_Connection=True;"));
}

// --- Konfigurasjon ---
builder.Services.Configure<EntraGruppeOpsjoner>(builder.Configuration.GetSection(EntraGruppeOpsjoner.Seksjon));
builder.Services.Configure<StartdataOpsjoner>(builder.Configuration.GetSection(StartdataOpsjoner.Seksjon));
builder.Services.Configure<SynlighetsregelOpsjoner>(builder.Configuration.GetSection(SynlighetsregelOpsjoner.Seksjon));

// --- Tjenester ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFristlesing, FristTjeneste>();
builder.Services.AddScoped<IFristskriving, FristskrivingTjeneste>();
builder.Services.AddScoped<IGodkjenningsko, GodkjenningskoTjeneste>();
builder.Services.AddScoped<IForslagsinnsending, ForslagsinnsendingTjeneste>();
builder.Services.AddScoped<IVarseltjeneste, Varseltjeneste>();
builder.Services.AddScoped<IGruppetjeneste, Gruppetjeneste>();
builder.Services.AddScoped<IWordEksport, WordEksportTjeneste>();
builder.Services.AddSingleton<ISynlighetsregel, Synlighetsregel>();
builder.Services.AddScoped<IGenereringstjeneste, GenereringsTjeneste>();
builder.Services.AddScoped<IMaltjeneste, Maltjeneste>();
builder.Services.AddScoped<IBrukeroppslag, BrukeroppslagTjeneste>();
builder.Services.AddScoped<ISynlighetskontekst, HttpSynlighetskontekst>();
builder.Services.AddScoped<IClaimsTransformation, BrukerClaimsTransformation>();
builder.Services.AddScoped<Synlighetskontekstkilde>();
builder.Services.AddScoped<Gjeldendebrukerkilde>();
builder.Services.AddScoped<Visningstilstand>();

// --- Autentisering. Produksjon: Entra ID (OIDC). Demo: cookie + dev-innlogging (ingen Entra).
//     Test: injiseres av testene. ---
if (brukEntra)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
}
else if (erDemo)
{
    builder.Services.AddAuthentication("Demo")
        .AddCookie("Demo", o =>
        {
            o.LoginPath = "/demo";
            o.AccessDeniedPath = "/demo";
        });
}

builder.Services.AddAuthorization(Autorisasjon.LeggTilPolicyer);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapFristEndepunkter();
if (brukEntra)
{
    app.MapControllers();
}
if (erDemo)
{
    app.MapDemoEndepunkter();
}

// --- Migrasjon/skjema + seeding ved oppstart (testene styrer egen database). ---
if (erDemo)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // SQLite-skjema fra modellen (EF-migrasjonene er SqlServer-spesifikke).
    db.Database.EnsureCreated();
    var opsjoner = scope.ServiceProvider.GetRequiredService<IOptions<StartdataOpsjoner>>().Value;
    await Startdata.SeedAsync(db, opsjoner);
    await Demodata.SeedAsync(db);
}
else if (!erTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    var opsjoner = scope.ServiceProvider.GetRequiredService<IOptions<StartdataOpsjoner>>().Value;
    await Startdata.SeedAsync(db, opsjoner);
}

app.Run();

/// <summary>Eksponert for integrasjonstester (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
