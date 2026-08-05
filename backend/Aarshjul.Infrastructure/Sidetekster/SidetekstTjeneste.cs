using Aarshjul.Application.Sidetekster;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Sidetekster;

/// <summary>Lagrer redigerbare flate-tekster i databasen (nøkkel → tekst). Tom tekst = fjern overstyring (fall tilbake til standard).</summary>
public class SidetekstTjeneste(AppDbContext db) : ISidetekster
{
    public async Task<string?> HentAsync(string nokkel, CancellationToken ct = default)
        => await db.Sidetekster.AsNoTracking()
            .Where(s => s.Nokkel == nokkel)
            .Select(s => s.Tekst)
            .FirstOrDefaultAsync(ct);

    public async Task LagreAsync(string nokkel, string? tekst, CancellationToken ct = default)
    {
        var eksisterende = await db.Sidetekster.FirstOrDefaultAsync(s => s.Nokkel == nokkel, ct);

        if (string.IsNullOrWhiteSpace(tekst))
        {
            // Tom = fjern overstyringen, slik at standardteksten vises igjen.
            if (eksisterende is not null)
            {
                db.Sidetekster.Remove(eksisterende);
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        if (eksisterende is null)
        {
            db.Sidetekster.Add(new Sidetekst { Nokkel = nokkel, Tekst = tekst.Trim() });
        }
        else
        {
            eksisterende.Tekst = tekst.Trim();
        }
        await db.SaveChangesAsync(ct);
    }
}
