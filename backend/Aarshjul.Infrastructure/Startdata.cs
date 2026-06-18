using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure;

/// <summary>Konfigurasjon for seeding av første administrator ved idriftsetting (kravdok. 2.2).</summary>
public class StartdataOpsjoner
{
    public const string Seksjon = "Startdata";

    /// <summary>Entra oid/sub for den første administratoren. Tom = ingen admin seedes.</summary>
    public string? FoersteAdminId { get; set; }

    /// <summary>Visningsnavn for den første administratoren.</summary>
    public string? FoersteAdminNavn { get; set; }
}

/// <summary>
/// Seeder de fire innebygde standardgruppene (FA, FIN-FAG, FAG, POL) og — hvis konfigurert —
/// den første administratoren. Idempotent: kjører trygt ved hver oppstart. Re-seed av admin
/// når stolen er tom skjer via drift (samme mekanisme), ikke via grensesnittet.
/// </summary>
public static class Startdata
{
    public static readonly (string Kode, string Navn)[] Standardgrupper =
    [
        ("FA", "Finansavdelingen"),
        ("FIN-FAG", "Øvrige avdelinger i FIN"),
        ("FAG", "Fagdepartementene"),
        ("POL", "Politisk ledelse")
    ];

    public static async Task SeedAsync(AppDbContext db, StartdataOpsjoner opsjoner, CancellationToken ct = default)
    {
        await SeedStandardgrupperAsync(db, ct);
        await SeedFoersteAdminAsync(db, opsjoner, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedStandardgrupperAsync(AppDbContext db, CancellationToken ct)
    {
        var eksisterende = await db.Synlighetsgrupper
            .Select(g => g.Kode)
            .ToListAsync(ct);

        foreach (var (kode, navn) in Standardgrupper)
        {
            if (!eksisterende.Contains(kode))
            {
                db.Synlighetsgrupper.Add(new Synlighetsgruppe
                {
                    Id = Guid.NewGuid(),
                    Kode = kode,
                    Navn = navn,
                    Aktiv = true,
                    ErStandard = true
                });
            }
        }
    }

    private static async Task SeedFoersteAdminAsync(AppDbContext db, StartdataOpsjoner opsjoner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opsjoner.FoersteAdminId))
        {
            return;
        }

        // Seed kun hvis det ikke allerede finnes en administrator.
        var harAdmin = await db.Brukere.AnyAsync(u => u.Funksjonsrolle == Funksjonsrolle.Administrator, ct);
        if (harAdmin)
        {
            return;
        }

        var bruker = await db.Brukere.FindAsync([opsjoner.FoersteAdminId], ct);
        if (bruker is null)
        {
            bruker = new Bruker
            {
                Id = opsjoner.FoersteAdminId,
                Navn = opsjoner.FoersteAdminNavn ?? "Administrator",
                ErFin = true,
                Funksjonsrolle = Funksjonsrolle.Administrator
            };
            db.Brukere.Add(bruker);
        }
        else
        {
            bruker.Funksjonsrolle = Funksjonsrolle.Administrator;
            bruker.ErFin = true;
        }

        // Første administrator er FA-ansatt; gi FA-medlemskap hvis det mangler.
        var harFa = await db.BrukerGrupper.AnyAsync(g => g.BrukerId == bruker.Id && g.GruppeKode == "FA", ct);
        if (!harFa)
        {
            db.BrukerGrupper.Add(new BrukerGruppe
            {
                BrukerId = bruker.Id,
                GruppeKode = "FA",
                Kilde = GruppeMedlemskapKilde.Manuell
            });
        }
    }
}
