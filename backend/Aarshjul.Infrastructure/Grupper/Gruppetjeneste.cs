using Aarshjul.Application.Grupper;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Grupper;

/// <summary>Leser synlighetsgrupper fra databasen for synlighetsvalg og administrasjon.</summary>
public class Gruppetjeneste(AppDbContext db) : IGruppetjeneste
{
    public async Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default)
        => await db.Synlighetsgrupper
            .Where(g => g.Aktiv)
            .OrderByDescending(g => g.ErStandard)
            .ThenBy(g => g.Navn)
            .Select(g => new GruppeDto(g.Kode, g.Navn, g.ErStandard))
            .ToListAsync(ct);
}
