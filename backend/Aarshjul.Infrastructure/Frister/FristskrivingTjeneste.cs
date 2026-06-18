using Aarshjul.Application;
using Aarshjul.Application.Frister;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Frister;

/// <summary>
/// Oppretter og oppdaterer frister på vegne av administrator. Håndhever at synlighet er valgt
/// aktivt (ingen stilltiende standard) og at valgte koder peker på eksisterende, aktive grupper.
/// </summary>
public class FristskrivingTjeneste(AppDbContext db) : IFristskriving
{
    public async Task<Guid> OpprettAsync(FristInndata inndata, CancellationToken ct = default)
    {
        var koder = await ValiderAsync(inndata, ct);

        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = inndata.Tittel.Trim(),
            Kilde = "manuell",
            Opphav = Opphav.Admin,
            Status = FristStatus.Godkjent
        };
        SettFelter(frist, inndata);
        foreach (var kode in koder)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = kode });
        }

        db.Frister.Add(frist);
        await db.SaveChangesAsync(ct);
        return frist.Id;
    }

    public async Task OppdaterAsync(Guid id, FristInndata inndata, CancellationToken ct = default)
    {
        var koder = await ValiderAsync(inndata, ct);

        var frist = await db.Frister
            .Include(f => f.Synlighet)
            .FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new Valideringsfeil("Fristen finnes ikke.");

        SettFelter(frist, inndata);

        // Diff framfor «clear + re-add»: å legge til en kode som allerede er tracket gir
        // EF en identitetskonflikt. Fjern kun de som utgår, legg til kun de som er nye.
        var ønsket = koder.ToHashSet();
        foreach (var utgår in frist.Synlighet.Where(s => !ønsket.Contains(s.GruppeKode)).ToList())
        {
            frist.Synlighet.Remove(utgår);
        }
        var finnes = frist.Synlighet.Select(s => s.GruppeKode).ToHashSet();
        foreach (var kode in koder.Where(k => !finnes.Contains(k)))
        {
            frist.Synlighet.Add(new FristSynlighet { FristId = id, GruppeKode = kode });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<FristInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default)
    {
        var frist = await db.Frister
            .AsNoTracking()
            .Include(f => f.Synlighet)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (frist is null)
        {
            return null;
        }

        return new FristInndata
        {
            Tittel = frist.Tittel,
            Dato = frist.Dato,
            Datopresisjon = frist.Datopresisjon,
            Datokvalifikator = frist.Datokvalifikator,
            Budsjettaar = frist.Budsjettaar,
            Kategori = frist.Kategori,
            Loep = frist.Loep,
            Notat = frist.Notat,
            Synlighetskoder = frist.Synlighet.Select(s => s.GruppeKode).ToList()
        };
    }

    private static void SettFelter(Frist frist, FristInndata inndata)
    {
        frist.Tittel = inndata.Tittel.Trim();
        frist.Dato = inndata.Dato;
        frist.Datopresisjon = inndata.Datopresisjon;
        // Kvalifikator er kun meningsfull ved månedspresisjon.
        frist.Datokvalifikator = inndata.Datopresisjon == Datopresisjon.Maaned ? inndata.Datokvalifikator : null;
        frist.Budsjettaar = inndata.Budsjettaar;
        frist.Kategori = inndata.Kategori;
        frist.Loep = string.IsNullOrWhiteSpace(inndata.Loep) ? null : inndata.Loep.Trim();
        frist.Notat = string.IsNullOrWhiteSpace(inndata.Notat) ? null : inndata.Notat.Trim();
    }

    /// <summary>Validerer skjemaet og returnerer normaliserte, gyldige gruppekoder.</summary>
    private async Task<List<string>> ValiderAsync(FristInndata inndata, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inndata.Tittel))
        {
            throw new Valideringsfeil("Tittel må fylles ut.");
        }

        var koder = inndata.Synlighetskoder
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct()
            .ToList();

        if (koder.Count == 0)
        {
            throw new Valideringsfeil("Velg minst én synlighetsgruppe.");
        }

        var gyldige = await db.Synlighetsgrupper
            .Where(g => g.Aktiv && koder.Contains(g.Kode))
            .Select(g => g.Kode)
            .ToListAsync(ct);

        var ukjente = koder.Except(gyldige).ToList();
        if (ukjente.Count > 0)
        {
            throw new Valideringsfeil($"Ukjente eller inaktive grupper: {string.Join(", ", ukjente)}.");
        }

        return gyldige;
    }
}
