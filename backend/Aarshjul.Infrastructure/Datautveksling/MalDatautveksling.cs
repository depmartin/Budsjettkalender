using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Datautveksling;

/// <summary>
/// Eksporterer og importerer alle gjentaksregler (årsmalen) som en JSON-«database» — tilsvarende
/// <see cref="FristDatautveksling"/> for frister. Import erstatter alt: eksisterende regler fjernes
/// og fila blir fasiten. Regler med parametre som ikke lar seg tolke for regeltypen, hoppes over med
/// en advarsel, slik at en senere genereringskjøring ikke feiler på en ugyldig regel.
/// </summary>
public sealed class MalDatautveksling(AppDbContext db, TimeProvider klokke) : IMalDatautveksling
{
    public async Task<MalDatabase> EksporterAsync(CancellationToken ct = default)
    {
        var regler = await db.Gjentaksregler
            .AsNoTracking()
            .OrderBy(r => r.Loep)
            .ToListAsync(ct);

        return new MalDatabase
        {
            Versjon = 1,
            EksportertTid = klokke.GetUtcNow(),
            Regler = regler.Select(r => new MalRegelEksport
            {
                Id = r.Id,
                Loep = r.Loep,
                Tittel = r.Tittel,
                Kategori = r.Kategori,
                Regeltype = r.Regeltype,
                Regelparametre = r.Regelparametre,
                Valgaarssensitiv = r.Valgaarssensitiv
            }).ToList()
        };
    }

    public async Task<ImportResultat> ImporterAsync(MalDatabase database, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var advarsler = new List<string>();

        var eksisterende = await db.Gjentaksregler.ToListAsync(ct);
        var antallErstattet = eksisterende.Count;
        db.Gjentaksregler.RemoveRange(eksisterende);

        var importert = 0;
        foreach (var kilde in database.Regler)
        {
            if (string.IsNullOrWhiteSpace(kilde.Loep))
            {
                advarsler.Add($"Hoppet over en regel uten løp (tittel «{kilde.Tittel}»).");
                continue;
            }

            if (!ParametreErGyldige(kilde, out var grunn))
            {
                advarsler.Add($"«{(string.IsNullOrWhiteSpace(kilde.Tittel) ? kilde.Loep : kilde.Tittel)}»: hoppet over — {grunn}");
                continue;
            }

            db.Gjentaksregler.Add(new Gjentaksregel
            {
                Id = kilde.Id == Guid.Empty ? Guid.NewGuid() : kilde.Id,
                Loep = kilde.Loep.Trim(),
                Tittel = kilde.Tittel.Trim(),
                Kategori = kilde.Kategori,
                Regeltype = kilde.Regeltype,
                Regelparametre = string.IsNullOrWhiteSpace(kilde.Regelparametre) ? "{}" : kilde.Regelparametre.Trim(),
                Valgaarssensitiv = kilde.Valgaarssensitiv
            });
            importert++;
        }

        await db.SaveChangesAsync(ct);
        return new ImportResultat(importert, antallErstattet, advarsler);
    }

    /// <summary>Kontrollerer at parametrene kan tolkes for regeltypen (samme krav som ved manuell lagring).</summary>
    private static bool ParametreErGyldige(MalRegelEksport regel, out string grunn)
    {
        try
        {
            switch (regel.Regeltype)
            {
                case Regeltype.FastDato:
                    Regelparser.FastDato(regel.Regelparametre);
                    break;
                case Regeltype.RelativUkedag:
                    var u = Regelparser.RelativUkedag(regel.Regelparametre);
                    Regelparser.TolkUkedag(u.Ukedag);
                    break;
                case Regeltype.RelativTilMilepael:
                    var m = Regelparser.RelativTilMilepael(regel.Regelparametre);
                    if (string.IsNullOrWhiteSpace(m.AnkerLoep))
                    {
                        grunn = "mangler anker-løp.";
                        return false;
                    }
                    break;
            }
        }
        catch (Exception e) when (e is FormatException or System.Text.Json.JsonException)
        {
            grunn = "ugyldige regelparametre.";
            return false;
        }

        grunn = "";
        return true;
    }
}
