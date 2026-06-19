using Aarshjul.Application.Varsler;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Varsler;

/// <summary>
/// Leser og kvitterer varsler for en bruker. All filtrering er på <see cref="Varsel.BrukerId"/>,
/// slik at en bruker bare ser og kan kvittere ut sine egne varsler.
/// </summary>
public class Varseltjeneste(AppDbContext db) : IVarseltjeneste
{
    public async Task<IReadOnlyList<VarselDto>> HentForBrukerAsync(string brukerId, CancellationToken ct = default)
    {
        // Sorter klientside: SQLite (testprovideren) støtter ikke ORDER BY på DateTimeOffset.
        var varsler = await db.Varsler.AsNoTracking()
            .Where(v => v.BrukerId == brukerId)
            .ToListAsync(ct);

        return varsler
            .OrderByDescending(v => v.Opprettet)
            .Select(v => new VarselDto(v.Id, v.Tekst, v.Begrunnelse, v.Lest, v.Opprettet))
            .ToList();
    }

    public async Task<int> AntallUlesteAsync(string brukerId, CancellationToken ct = default) =>
        await db.Varsler.CountAsync(v => v.BrukerId == brukerId && !v.Lest, ct);

    public async Task MarkerLestAsync(Guid varselId, string brukerId, CancellationToken ct = default)
    {
        var varsel = await db.Varsler.FirstOrDefaultAsync(v => v.Id == varselId && v.BrukerId == brukerId, ct);
        if (varsel is { Lest: false })
        {
            varsel.Lest = true;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkerAlleLestAsync(string brukerId, CancellationToken ct = default)
    {
        var uleste = await db.Varsler.Where(v => v.BrukerId == brukerId && !v.Lest).ToListAsync(ct);
        if (uleste.Count == 0)
            return;
        foreach (var v in uleste)
            v.Lest = true;
        await db.SaveChangesAsync(ct);
    }
}
