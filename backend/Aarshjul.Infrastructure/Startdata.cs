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

    /// <summary>
    /// Standard gjentaksregler for de syv kjente løpene (kravdok. 4.3). Datoene er maler
    /// administrator kan justere; <c>aar_forskyvning</c> plasserer fristen i riktig kalenderår
    /// relativt til budsjettåret (årshjulet spenner ~18 mnd). Høstløpets frister er
    /// valgårssensitive (kravdok. 6).
    /// </summary>
    public static readonly (string Loep, string Tittel, Kategori Kategori, Regeltype Regeltype, string Parametre, bool Valgaarssensitiv)[] Standardregler =
    [
        ("marskonferanse", "Materiale til regjeringens budsjettkonferanse i mars", Kategori.Budsjett, Regeltype.FastDato, "{\"maaned\":3,\"dag\":1,\"aar_forskyvning\":-1}", false),
        ("rammefordeling", "Hovedbudsjettskriv (rammefordeling)", Kategori.Budsjett, Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":-1}", false),
        ("rnb", "Revidert nasjonalbudsjett – tilleggsbevilgninger og omprioriteringer", Kategori.Budsjett, Regeltype.FastDato, "{\"maaned\":5,\"dag\":15,\"aar_forskyvning\":0}", false),
        ("gulbok", "Bekreftelsesbrev og innlevering av tekst til Gul bok", Kategori.Gulbok, Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":-1}", true),
        ("nysaldering", "Nysaldering – tilleggsbevilgninger i høstsesjonen", Kategori.Budsjett, Regeltype.FastDato, "{\"maaned\":11,\"dag\":20,\"aar_forskyvning\":-1}", true),
        ("statsregnskap", "Årsavslutning og frister for innrapportering til statsregnskapet", Kategori.Regnskap, Regeltype.FastDato, "{\"maaned\":2,\"dag\":15,\"aar_forskyvning\":1}", false),
        ("rapportering", "Rapportering til statsregnskapet", Kategori.Regnskap, Regeltype.FastDato, "{\"maaned\":1,\"dag\":31,\"aar_forskyvning\":1}", false)
    ];

    public static async Task SeedAsync(AppDbContext db, StartdataOpsjoner opsjoner, CancellationToken ct = default)
    {
        await SeedStandardgrupperAsync(db, ct);
        await SeedStandardreglerAsync(db, ct);
        await SeedFoersteAdminAsync(db, opsjoner, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Seeder standardreglene kun når malen er tom, slik at administrators senere
    /// endringer/sletting av regler ikke overskrives ved oppstart.</summary>
    private static async Task SeedStandardreglerAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Gjentaksregler.AnyAsync(ct))
        {
            return;
        }

        foreach (var (loep, tittel, kategori, regeltype, parametre, valgaarssensitiv) in Standardregler)
        {
            db.Gjentaksregler.Add(new Gjentaksregel
            {
                Id = Guid.NewGuid(),
                Loep = loep,
                Tittel = tittel,
                Kategori = kategori,
                Regeltype = regeltype,
                Regelparametre = parametre,
                Valgaarssensitiv = valgaarssensitiv
            });
        }
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
