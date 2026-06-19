using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Infrastructure.Brukerforslag;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Godkjenningsko;
using Aarshjul.Infrastructure.Grupper;
using Aarshjul.Web.Api;
using Aarshjul.Web.Components;
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

// --- Database (Azure SQL i drift). Utelates i testmiljø; testene registrerer egen provider. ---
if (!erTesting)
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("Aarshjul")
            ?? "Server=(localdb)\\mssqllocaldb;Database=Aarshjul;Trusted_Connection=True;"));
}

// --- Konfigurasjon ---
builder.Services.Configure<EntraGruppeOpsjoner>(builder.Configuration.GetSection(EntraGruppeOpsjoner.Seksjon));
builder.Services.Configure<StartdataOpsjoner>(builder.Configuration.GetSection(StartdataOpsjoner.Seksjon));

// --- Tjenester ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFristlesing, FristTjeneste>();
builder.Services.AddScoped<IFristskriving, FristskrivingTjeneste>();
builder.Services.AddScoped<IGodkjenningsko, GodkjenningskoTjeneste>();
builder.Services.AddScoped<IForslagsinnsending, ForslagsinnsendingTjeneste>();
builder.Services.AddScoped<IGruppetjeneste, Gruppetjeneste>();
builder.Services.AddScoped<IBrukeroppslag, BrukeroppslagTjeneste>();
builder.Services.AddScoped<ISynlighetskontekst, HttpSynlighetskontekst>();
builder.Services.AddScoped<IClaimsTransformation, BrukerClaimsTransformation>();
builder.Services.AddScoped<Synlighetskontekstkilde>();
builder.Services.AddScoped<Gjeldendebrukerkilde>();
builder.Services.AddScoped<Visningstilstand>();

// --- Autentisering (Entra ID). Utelates i testmiljø; testene injiserer egen ordning. ---
if (!erTesting)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
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
if (!erTesting)
{
    app.MapControllers();
}

// --- Migrasjon + seeding ved oppstart (ikke i testmiljø; testene styrer egen database) ---
if (!erTesting)
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
