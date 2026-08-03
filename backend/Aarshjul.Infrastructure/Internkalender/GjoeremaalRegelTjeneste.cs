using Aarshjul.Application;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Internkalender;

/// <summary>
/// Forvalter de generelle reglene (malene) i internkalenderen (trinn 2). En regel er årsuavhengig
/// og knyttes til én eller flere rundetyper; ved generering (se <see cref="InternkalenderTjeneste"/>)
/// beregnes et konkret gjøremål per runde. Alle handlinger er SBR-handlinger (policy i web-laget).
/// </summary>
public class GjoeremaalRegelTjeneste(AppDbContext db) : IGjoeremaalRegler
{
    public async Task<IReadOnlyList<RegelDto>> HentAlleAsync(CancellationToken ct = default)
    {
        var regler = await db.GjoeremaalRegler.AsNoTracking()
            .Include(r => r.Rundetyper)
            .Include(r => r.Ansvarlige)
            .OrderBy(r => r.Tittel)
            .ToListAsync(ct);

        return regler.Select(r => new RegelDto(
            r.Id,
            r.Tittel,
            r.Notat,
            r.Rundetyper.Select(x => x.Rundetype).OrderBy(x => x).ToList(),
            r.Tidfestingstype,
            Tidfestingsformat.ByggRegel(r.Tidfestingstype, r.Maaned, r.Dag, r.Datokvalifikator, r.AnkerLoep, r.AnkerOffsetDager, r.Rundeposisjon),
            r.Ansvarlige.Select(a => new AnsvarligDto(a.BrukerId, a.Navn)).ToList())).ToList();
    }

    public async Task<RegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.GjoeremaalRegler.AsNoTracking()
            .Include(x => x.Rundetyper)
            .Include(x => x.Ansvarlige)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return null;
        }

        return new RegelInndata
        {
            Tittel = r.Tittel,
            Notat = r.Notat,
            Rundetyper = r.Rundetyper.Select(x => x.Rundetype).ToList(),
            Tidfestingstype = r.Tidfestingstype,
            Maaned = r.Maaned,
            Dag = r.Dag,
            AarforskyvningJustering = r.AarforskyvningJustering,
            Datokvalifikator = r.Datokvalifikator,
            AnkerLoep = r.AnkerLoep,
            AnkerOffsetDager = r.AnkerOffsetDager,
            Rundeposisjon = r.Rundeposisjon,
            Ansvarlige = r.Ansvarlige.Select(a => new AnsvarligDto(a.BrukerId, a.Navn)).ToList()
        };
    }

    public async Task<Guid> OpprettAsync(RegelInndata inndata, CancellationToken ct = default)
    {
        Valider(inndata);
        var r = new GjoeremaalRegel { Id = Guid.NewGuid(), Tittel = inndata.Tittel.Trim() };
        SettFelter(r, inndata);
        db.GjoeremaalRegler.Add(r);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    public async Task OppdaterAsync(Guid id, RegelInndata inndata, CancellationToken ct = default)
    {
        Valider(inndata);
        var r = await db.GjoeremaalRegler
            .Include(x => x.Rundetyper)
            .Include(x => x.Ansvarlige)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new Valideringsfeil("Regelen finnes ikke.");

        r.Tittel = inndata.Tittel.Trim();
        db.RegelRundetyper.RemoveRange(r.Rundetyper);
        r.Rundetyper.Clear();
        db.RegelAnsvarlige.RemoveRange(r.Ansvarlige);
        r.Ansvarlige.Clear();
        SettFelter(r, inndata);
        await db.SaveChangesAsync(ct);
    }

    public async Task SlettAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.GjoeremaalRegler.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is not null)
        {
            db.GjoeremaalRegler.Remove(r);
            await db.SaveChangesAsync(ct);
        }
    }

    private static void Valider(RegelInndata inndata)
    {
        if (string.IsNullOrWhiteSpace(inndata.Tittel))
        {
            throw new Valideringsfeil("Tittel må fylles ut.");
        }
        if (inndata.Rundetyper.Count == 0)
        {
            throw new Valideringsfeil("Regelen må gjelde minst én rundetype.");
        }
        if (inndata.Rundetyper.Any(t => !Runder.Genererbare.Contains(t)))
        {
            throw new Valideringsfeil("En regel kan bare gjelde genererbare runder (ikke Øvrig).");
        }
        switch (inndata.Tidfestingstype)
        {
            case Tidfestingstype.KonkretDato:
                if (inndata.Maaned is not (>= 1 and <= 12) || inndata.Dag is not (>= 1 and <= 31))
                {
                    throw new Valideringsfeil("Konkret dato krever måned (1–12) og dag (1–31).");
                }
                break;
            case Tidfestingstype.TentativMaaned:
                if (inndata.Maaned is not (>= 1 and <= 12))
                {
                    throw new Valideringsfeil("Tentativ måned krever måned (1–12).");
                }
                break;
            case Tidfestingstype.AnkerRelativ:
                if (string.IsNullOrWhiteSpace(inndata.AnkerLoep))
                {
                    throw new Valideringsfeil("Anker-løp må velges for en anker-relativ tidfesting.");
                }
                break;
            case Tidfestingstype.Rundeposisjon:
                if (inndata.Rundeposisjon is null)
                {
                    throw new Valideringsfeil("Velg en posisjon i runden.");
                }
                break;
        }
    }

    private static void SettFelter(GjoeremaalRegel r, RegelInndata inndata)
    {
        r.Notat = string.IsNullOrWhiteSpace(inndata.Notat) ? null : inndata.Notat.Trim();
        r.Tidfestingstype = inndata.Tidfestingstype;
        r.Maaned = inndata.Maaned;
        r.Dag = inndata.Dag;
        r.AarforskyvningJustering = inndata.AarforskyvningJustering;
        r.Datokvalifikator = inndata.Datokvalifikator;
        r.AnkerLoep = string.IsNullOrWhiteSpace(inndata.AnkerLoep) ? null : inndata.AnkerLoep.Trim();
        r.AnkerOffsetDager = inndata.AnkerOffsetDager;
        r.Rundeposisjon = inndata.Rundeposisjon;

        foreach (var t in inndata.Rundetyper.Distinct())
        {
            r.Rundetyper.Add(new RegelRundetype { RegelId = r.Id, Rundetype = t });
        }
        foreach (var a in inndata.Ansvarlige.Where(a => !string.IsNullOrWhiteSpace(a.Navn)))
        {
            r.Ansvarlige.Add(new RegelAnsvarlig
            {
                Id = Guid.NewGuid(),
                RegelId = r.Id,
                BrukerId = string.IsNullOrWhiteSpace(a.BrukerId) ? null : a.BrukerId,
                Navn = a.Navn.Trim()
            });
        }
    }
}
