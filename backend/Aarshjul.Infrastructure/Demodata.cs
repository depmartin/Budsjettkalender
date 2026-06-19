using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure;

/// <summary>
/// Seeder demo-data for lokal kjøremodus (miljø <c>Demo</c>): et knippe personaer med ulik
/// funksjonsrolle og synlighetsgruppe, frister med variert synlighet over et par budsjettår, og
/// noen ventende forslag i godkjenningskøen. Kun for demonstrasjon — kjøres aldri i produksjon.
/// Idempotent: hopper over hvis demo-brukerne allerede finnes. Forutsetter at standardgruppene er
/// seedet (<see cref="Startdata"/>).
/// </summary>
public static class Demodata
{
    /// <summary>Demo-personaer som kan velges på dev-innloggingen. Grupper settes som manuelle
    /// medlemskap, slik at de overlever brukeroppslaget ved innlogging.</summary>
    public static readonly (string Id, string Navn, Funksjonsrolle Rolle, bool ErFin, string[] Grupper)[] Personaer =
    [
        ("demo-admin", "Dag Admin (administrator, FA)", Funksjonsrolle.Administrator, true, ["FA"]),
        ("demo-fa", "Frida FA (bidragsyter, FA)", Funksjonsrolle.Bidragsyter, true, ["FA"]),
        ("demo-finfag", "Finn FIN-FAG (bidragsyter, FIN-FAG)", Funksjonsrolle.Bidragsyter, true, ["FIN-FAG"]),
        ("demo-fag", "Frank Fagdep (leser, FAG)", Funksjonsrolle.Leser, false, ["FAG"]),
        ("demo-pol", "Pia Politiker (leser, POL)", Funksjonsrolle.Leser, false, ["POL"])
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Brukere.AnyAsync(u => u.Id == "demo-admin", ct))
        {
            return;
        }

        SeedBrukere(db);
        var frister = SeedFrister(db);
        await db.SaveChangesAsync(ct);

        SeedForslag(db, frister);
        await db.SaveChangesAsync(ct);
    }

    private static void SeedBrukere(AppDbContext db)
    {
        foreach (var (id, navn, rolle, erFin, grupper) in Personaer)
        {
            var bruker = new Bruker { Id = id, Navn = navn, Funksjonsrolle = rolle, ErFin = erFin };
            db.Brukere.Add(bruker);
            foreach (var kode in grupper)
            {
                db.BrukerGrupper.Add(new BrukerGruppe
                {
                    BrukerId = id,
                    GruppeKode = kode,
                    Kilde = GruppeMedlemskapKilde.Manuell
                });
            }
        }
    }

    private static List<Frist> SeedFrister(AppDbContext db)
    {
        // Datoer er lagt rundt sommeren/høsten 2026 slik at landingsflaten («innen 30 dager /
        // de neste fem») viser noe, og med ett tidligere år for historikk.
        var data = new (string Tittel, DateOnly Dato, Datopresisjon Presisjon, Datokvalifikator? Kval, int Budsjettaar, Kategori Kategori, string? Loep, string[] Synlig)[]
        {
            ("Frist for innspill til rammefordeling", new(2026, 6, 25), Datopresisjon.Dag, null, 2027, Kategori.Budsjett, "rammefordeling", ["FA", "FIN-FAG"]),
            ("Møte om budsjettrammer", new(2026, 6, 29), Datopresisjon.Dag, null, 2027, Kategori.Budsjett, null, ["FA", "POL"]),
            ("Innspill til regjeringens budsjettkonferanse", new(2026, 7, 10), Datopresisjon.Dag, null, 2027, Kategori.Budsjett, "marskonferanse", ["FA"]),
            ("Regjeringens budsjettkonferanse i august", new(2026, 8, 1), Datopresisjon.Maaned, Datokvalifikator.Medio, 2027, Kategori.Budsjett, "haustkonferanse", ["FA", "POL"]),
            ("Innlevering av tekst til Gul bok", new(2026, 9, 15), Datopresisjon.Dag, null, 2027, Kategori.Gulbok, "gulbok", ["FA", "FIN-FAG", "FAG"]),
            ("Statsbudsjettet legges fram", new(2026, 10, 6), Datopresisjon.Dag, null, 2027, Kategori.Budsjett, "fremleggelse", ["FA", "FIN-FAG", "FAG", "POL"]),
            ("Nysaldering – tilleggsbevilgninger høst", new(2026, 11, 20), Datopresisjon.Dag, null, 2026, Kategori.Budsjett, "nysaldering", ["FA", "FIN-FAG"]),
            ("Frist intern rapportering til statsregnskapet", new(2026, 12, 10), Datopresisjon.Dag, null, 2027, Kategori.Regnskap, "rapportering", ["FIN-FAG"]),
            ("Årsavslutning statsregnskapet", new(2027, 2, 15), Datopresisjon.Dag, null, 2026, Kategori.Regnskap, "statsregnskap", ["FA", "FIN-FAG"]),
            ("Revidert nasjonalbudsjett", new(2026, 5, 15), Datopresisjon.Dag, null, 2026, Kategori.Budsjett, "rnb", ["FA", "FIN-FAG", "FAG"])
        };

        var frister = new List<Frist>();
        foreach (var d in data)
        {
            var frist = new Frist
            {
                Id = Guid.NewGuid(),
                Tittel = d.Tittel,
                Dato = d.Dato,
                Datopresisjon = d.Presisjon,
                Datokvalifikator = d.Kval,
                Budsjettaar = d.Budsjettaar,
                Kategori = d.Kategori,
                Loep = d.Loep,
                Kilde = "manuell",
                Opphav = Opphav.Admin,
                Status = FristStatus.Godkjent
            };
            foreach (var kode in d.Synlig)
            {
                frist.Synlighet.Add(new FristSynlighet { GruppeKode = kode });
            }
            db.Frister.Add(frist);
            frister.Add(frist);
        }
        return frister;
    }

    private static void SeedForslag(AppDbContext db, List<Frist> frister)
    {
        // Robotforslag (med uttrekksbevis) i køen.
        var robot = new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = ForslagType.NyFrist,
            Opphav = Opphav.Robot,
            KildeEllerInnsender = "R-4/2027",
            Tittel = "Hovedbudsjettskriv for 2028",
            Dato = new DateOnly(2026, 12, 15),
            Budsjettaar = 2028,
            Kategori = Kategori.Budsjett,
            Loep = "rammefordeling",
            ForeslaattSynlighet = "[\"FA\",\"FIN-FAG\"]",
            Status = FristStatus.Forslag
        };
        robot.UttrekksBevis.Add(new UttrekksBevis { Id = Guid.NewGuid(), Felt = "dato", TolketVerdi = "15.12.2026", Kildeutdrag = "Frist for tilbakemelding er 15. desember 2026", Konfidens = 0.92 });
        robot.UttrekksBevis.Add(new UttrekksBevis { Id = Guid.NewGuid(), Felt = "tittel", TolketVerdi = "Hovedbudsjettskriv for 2028", Kildeutdrag = "R-4/2027 Hovedbudsjettskriv", Konfidens = 0.97 });
        db.Forslag.Add(robot);

        // Brukerforslag fra en bidragsyter i køen.
        db.Forslag.Add(new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = ForslagType.NyFrist,
            Opphav = Opphav.Bruker,
            KildeEllerInnsender = "Frida FA (bidragsyter, FA)",
            Tittel = "Internt arbeidsmøte om satsingsforslag",
            Dato = new DateOnly(2026, 9, 1),
            Budsjettaar = 2027,
            Kategori = Kategori.Budsjett,
            Notat = "Forslag fra FA – bør være synlig for FA.",
            ForeslaattSynlighet = "[\"FA\"]",
            Status = FristStatus.Forslag
        });

        // Endringsforslag mot en publisert frist (før/etter i køen).
        var maalfrist = frister.First(f => f.Loep == "gulbok");
        db.Forslag.Add(new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = ForslagType.Endring,
            Opphav = Opphav.Bruker,
            KildeEllerInnsender = "Finn FIN-FAG (bidragsyter, FIN-FAG)",
            Tittel = maalfrist.Tittel,
            Dato = new DateOnly(2026, 9, 18),
            Budsjettaar = maalfrist.Budsjettaar,
            Kategori = maalfrist.Kategori,
            Loep = maalfrist.Loep,
            Notat = "Foreslår å flytte fristen tre dager.",
            EndrerFristId = maalfrist.Id,
            Status = FristStatus.Forslag
        });

        // Et ulest varsel til bidragsyteren (lukket tilbakemeldingssløyfe).
        db.Varsler.Add(new Varsel
        {
            Id = Guid.NewGuid(),
            BrukerId = "demo-fa",
            Tekst = "Forslaget ditt er godkjent.",
            Lest = false
        });
    }
}
