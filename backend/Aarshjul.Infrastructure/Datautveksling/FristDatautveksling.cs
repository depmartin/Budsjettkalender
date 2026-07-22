using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Datautveksling;

/// <summary>
/// Eksporterer og importerer alle frister som en JSON-«database» (endring #2). Import erstatter
/// alt: eksisterende frister (og deres synlighetskoblinger) fjernes, og fila blir fasiten.
/// Synlighetskoder som ikke finnes som grupper, hoppes over med en advarsel — da unngår vi å
/// bryte fremmednøkkelen mot <see cref="Synlighetsgruppe"/>. Ingen synlighet slippes til klienten
/// her; dette er en administratoroperasjon i backend.
/// </summary>
public sealed class FristDatautveksling(AppDbContext db, TimeProvider klokke) : IFristDatautveksling
{
    public async Task<FristDatabase> EksporterAsync(CancellationToken ct = default)
    {
        var frister = await db.Frister
            .AsNoTracking()
            .Include(f => f.Synlighet)
            .OrderBy(f => f.Budsjettaar)
            .ThenBy(f => f.Sorteringsdag)
            .ToListAsync(ct);

        var eksport = frister.Select(f => new FristEksport
        {
            Id = f.Id,
            Tittel = f.Tittel,
            Dato = f.Dato,
            Datopresisjon = f.Datopresisjon,
            Datokvalifikator = f.Datokvalifikator,
            Budsjettaar = f.Budsjettaar,
            Kategori = f.Kategori,
            Loep = f.Loep,
            Kilde = f.Kilde,
            DokumentId = f.DokumentId,
            Status = f.Status,
            Opphav = f.Opphav,
            ForeslaattAv = f.ForeslaattAv,
            Notat = f.Notat,
            GjentaRegelId = f.GjentaRegelId,
            SynligFor = f.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k).ToList()
        }).ToList();

        return new FristDatabase
        {
            Versjon = 1,
            EksportertTid = klokke.GetUtcNow(),
            Frister = eksport
        };
    }

    public async Task<ImportResultat> ImporterAsync(FristDatabase database, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var gyldigeKoder = (await db.Synlighetsgrupper.AsNoTracking().Select(g => g.Kode).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var advarsler = new List<string>();

        // Erstatt-alt: fjern eksisterende frister (synlighet kaskaderes via FristSynlighet-FK).
        var eksisterende = await db.Frister.Include(f => f.Synlighet).ToListAsync(ct);
        var antallErstattet = eksisterende.Count;
        db.Frister.RemoveRange(eksisterende);

        // Fjern tilbakestående synlighetsrader eksplisitt (robust dersom kaskade er avslått i test).
        db.FristSynlighet.RemoveRange(db.FristSynlighet);
        await db.SaveChangesAsync(ct);

        var importert = 0;
        foreach (var kilde in database.Frister)
        {
            var koder = new List<string>();
            foreach (var kode in kilde.SynligFor.Distinct(StringComparer.Ordinal))
            {
                if (gyldigeKoder.Contains(kode))
                {
                    koder.Add(kode);
                }
                else
                {
                    advarsler.Add($"«{kilde.Tittel}»: hoppet over ukjent synlighetsgruppe «{kode}».");
                }
            }

            var frist = new Frist
            {
                Id = kilde.Id == Guid.Empty ? Guid.NewGuid() : kilde.Id,
                Tittel = kilde.Tittel,
                Dato = kilde.Dato,
                Datopresisjon = kilde.Datopresisjon,
                Datokvalifikator = kilde.Datokvalifikator,
                Budsjettaar = kilde.Budsjettaar,
                Kategori = kilde.Kategori,
                Loep = kilde.Loep,
                Kilde = kilde.Kilde,
                DokumentId = kilde.DokumentId,
                Status = kilde.Status,
                Opphav = kilde.Opphav,
                ForeslaattAv = kilde.ForeslaattAv,
                Notat = kilde.Notat,
                GjentaRegelId = kilde.GjentaRegelId,
                Synlighet = koder.Select(k => new FristSynlighet { GruppeKode = k }).ToList()
            };
            frist.OppdaterSorteringsdag();
            db.Frister.Add(frist);
            importert++;
        }

        await db.SaveChangesAsync(ct);
        return new ImportResultat(importert, antallErstattet, advarsler);
    }
}
