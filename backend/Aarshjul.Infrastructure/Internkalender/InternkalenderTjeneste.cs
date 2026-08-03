using Aarshjul.Application;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Internkalender;

/// <summary>
/// Forvalter internkalenderens konkrete runder og gjøremål (SBR-intern arbeidsliste). Løser
/// tidfesting (konkret/tentativ/anker/rundeposisjon) til en entydig sorteringsdag, med
/// «venter på ankerdato» når en anker-frist ennå ikke finnes. Generering og synkronisering fra
/// generelle regler ligger i egne tjenester (trinn 2/3).
/// </summary>
public class InternkalenderTjeneste(AppDbContext db, TimeProvider klokke) : IInternkalender
{
    // ---------------------------------------------------------------- Runder

    public async Task<IReadOnlyList<RundeDto>> HentRunderAsync(CancellationToken ct = default)
    {
        var runder = await db.InternRunder
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Rundetype,
                r.Aar,
                r.OpprettetTid,
                r.SistSynkronisert,
                Aktive = r.Gjoeremaal.Count(g => g.Status == GjoeremaalStatus.Aktiv),
                Fullfoerte = r.Gjoeremaal.Count(g => g.Status == GjoeremaalStatus.Fullfoert)
            })
            .ToListAsync(ct);

        return runder
            .OrderBy(r => r.Rundetype == Rundetype.Ovrig)          // Øvrig sist
            .ThenByDescending(r => r.Aar ?? int.MinValue)
            .ThenBy(r => r.Rundetype)
            .Select(r => new RundeDto(
                r.Id, r.Rundetype, r.Aar, Runder.Etikett(r.Rundetype, r.Aar),
                r.OpprettetTid, r.SistSynkronisert, r.Aktive, r.Fullfoerte))
            .ToList();
    }

    public async Task<RundeDto?> HentRundeAsync(Guid rundeId, CancellationToken ct = default)
    {
        var r = await db.InternRunder.AsNoTracking()
            .Where(x => x.Id == rundeId)
            .Select(x => new
            {
                x.Id,
                x.Rundetype,
                x.Aar,
                x.OpprettetTid,
                x.SistSynkronisert,
                Aktive = x.Gjoeremaal.Count(g => g.Status == GjoeremaalStatus.Aktiv),
                Fullfoerte = x.Gjoeremaal.Count(g => g.Status == GjoeremaalStatus.Fullfoert)
            })
            .FirstOrDefaultAsync(ct);

        return r is null
            ? null
            : new RundeDto(r.Id, r.Rundetype, r.Aar, Runder.Etikett(r.Rundetype, r.Aar),
                r.OpprettetTid, r.SistSynkronisert, r.Aktive, r.Fullfoerte);
    }

    public async Task<Guid> OpprettRundeAsync(Rundetype type, int? aar, string? opprettetAv, CancellationToken ct = default)
    {
        if (Runder.HarAar(type) && aar is null)
        {
            throw new Valideringsfeil("Runden må ha et år.");
        }
        if (!Runder.HarAar(type))
        {
            aar = null;
        }

        var finnes = await db.InternRunder.AnyAsync(r => r.Rundetype == type && r.Aar == aar, ct);
        if (finnes)
        {
            throw new Valideringsfeil($"Runden «{Runder.Etikett(type, aar)}» finnes allerede.");
        }

        var runde = new InternRunde
        {
            Id = Guid.NewGuid(),
            Rundetype = type,
            Aar = aar,
            OpprettetTid = klokke.GetUtcNow(),
            OpprettetAv = opprettetAv
        };
        db.InternRunder.Add(runde);
        await db.SaveChangesAsync(ct);
        return runde.Id;
    }

    public async Task<Guid> GenererRundeAsync(Rundetype type, int aar, string? opprettetAv, CancellationToken ct = default)
    {
        if (!Runder.Genererbare.Contains(type))
        {
            throw new Valideringsfeil("Denne rundetypen kan ikke genereres.");
        }
        if (await db.InternRunder.AnyAsync(r => r.Rundetype == type && r.Aar == aar, ct))
        {
            throw new Valideringsfeil($"Runden «{Runder.Etikett(type, aar)}» finnes allerede. Bruk synkronisering.");
        }

        var runde = new InternRunde
        {
            Id = Guid.NewGuid(),
            Rundetype = type,
            Aar = aar,
            OpprettetTid = klokke.GetUtcNow(),
            OpprettetAv = opprettetAv,
            SistSynkronisert = klokke.GetUtcNow()
        };
        db.InternRunder.Add(runde);

        var regler = await HentReglerForAsync(type, ct);
        foreach (var regel in regler)
        {
            db.InterneGjoeremaal.Add(await ByggFraRegelAsync(regel, runde, ct));
        }

        await db.SaveChangesAsync(ct);
        return runde.Id;
    }

    public async Task<Guid> HentEllerOpprettOvrigAsync(string? opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.InternRunder
            .Where(r => r.Rundetype == Rundetype.Ovrig)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (eksisterende != Guid.Empty)
        {
            return eksisterende;
        }

        return await OpprettRundeAsync(Rundetype.Ovrig, null, opprettetAv, ct);
    }

    public async Task SlettRundeAsync(Guid rundeId, CancellationToken ct = default)
    {
        var runde = await db.InternRunder.FirstOrDefaultAsync(r => r.Id == rundeId, ct);
        if (runde is not null)
        {
            db.InternRunder.Remove(runde);
            await db.SaveChangesAsync(ct);
        }
    }

    // ------------------------------------------------------------- Gjøremål

    public async Task<IReadOnlyList<GjoeremaalDto>> HentGjoeremaalAsync(Guid rundeId, string? ansvarligBrukerId, CancellationToken ct = default)
    {
        var gjoeremaal = await db.InterneGjoeremaal.AsNoTracking()
            .Include(g => g.Ansvarlige)
            .Where(g => g.RundeId == rundeId)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(ansvarligBrukerId))
        {
            gjoeremaal = gjoeremaal
                .Where(g => g.Ansvarlige.Any(a => a.BrukerId == ansvarligBrukerId))
                .ToList();
        }

        return SorterOgBygg(gjoeremaal);
    }

    public async Task<Guid> HurtiglaggAsync(Guid rundeId, string tittel, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new Valideringsfeil("Tittel må fylles ut.");
        }

        var g = new InterntGjoeremaal
        {
            Id = Guid.NewGuid(),
            RundeId = rundeId,
            Tittel = tittel.Trim(),
            Tidfestingstype = Tidfestingstype.Ingen,
            Opphav = GjoeremaalOpphav.Manuell
        };
        db.InterneGjoeremaal.Add(g);
        await db.SaveChangesAsync(ct);
        return g.Id;
    }

    public async Task<GjoeremaalInndata?> HentForRedigeringAsync(Guid gjoeremaalId, CancellationToken ct = default)
    {
        var g = await db.InterneGjoeremaal.AsNoTracking()
            .Include(x => x.Ansvarlige)
            .FirstOrDefaultAsync(x => x.Id == gjoeremaalId, ct);
        if (g is null)
        {
            return null;
        }

        return new GjoeremaalInndata
        {
            Tittel = g.Tittel,
            Notat = g.Notat,
            Tidfestingstype = g.Tidfestingstype,
            Dato = g.Dato,
            Datopresisjon = g.Datopresisjon,
            Datokvalifikator = g.Datokvalifikator,
            AnkerLoep = g.AnkerLoep,
            AnkerOffsetDager = g.AnkerOffsetDager,
            Rundeposisjon = g.Rundeposisjon,
            Ansvarlige = g.Ansvarlige.Select(a => new AnsvarligDto(a.BrukerId, a.Navn)).ToList()
        };
    }

    public async Task<Guid?> HentRundeIdForGjoeremaalAsync(Guid gjoeremaalId, CancellationToken ct = default)
    {
        var rundeId = await db.InterneGjoeremaal.AsNoTracking()
            .Where(g => g.Id == gjoeremaalId)
            .Select(g => (Guid?)g.RundeId)
            .FirstOrDefaultAsync(ct);
        return rundeId;
    }

    public async Task<Guid> OpprettGjoeremaalAsync(Guid rundeId, GjoeremaalInndata inndata, CancellationToken ct = default)
    {
        var runde = await db.InternRunder.FirstOrDefaultAsync(r => r.Id == rundeId, ct)
            ?? throw new Valideringsfeil("Runden finnes ikke.");
        Valider(inndata);

        var g = new InterntGjoeremaal
        {
            Id = Guid.NewGuid(),
            RundeId = rundeId,
            Tittel = inndata.Tittel.Trim(),
            Opphav = GjoeremaalOpphav.Manuell
        };
        SettFelter(g, inndata);
        await BeregnTidfestingAsync(g, runde, ct);
        db.InterneGjoeremaal.Add(g);
        await db.SaveChangesAsync(ct);
        return g.Id;
    }

    public async Task OppdaterGjoeremaalAsync(Guid gjoeremaalId, GjoeremaalInndata inndata, CancellationToken ct = default)
    {
        var g = await db.InterneGjoeremaal
            .Include(x => x.Ansvarlige)
            .Include(x => x.Runde)
            .FirstOrDefaultAsync(x => x.Id == gjoeremaalId, ct)
            ?? throw new Valideringsfeil("Gjøremålet finnes ikke.");
        Valider(inndata);

        g.Tittel = inndata.Tittel.Trim();
        db.GjoeremaalAnsvarlige.RemoveRange(g.Ansvarlige);
        g.Ansvarlige.Clear();
        SettFelter(g, inndata);
        await BeregnTidfestingAsync(g, g.Runde!, ct);

        // Et generert gjøremål som endres manuelt beskyttes mot senere synkronisering.
        if (g.Opphav == GjoeremaalOpphav.Generert)
        {
            g.ManueltEndret = true;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task SlettGjoeremaalAsync(Guid gjoeremaalId, CancellationToken ct = default)
    {
        var g = await db.InterneGjoeremaal.FirstOrDefaultAsync(x => x.Id == gjoeremaalId, ct);
        if (g is not null)
        {
            db.InterneGjoeremaal.Remove(g);
            await db.SaveChangesAsync(ct);
        }
    }

    // --------------------------------------------------------------- Avhuking

    public async Task FullfoerAsync(Guid gjoeremaalId, string brukerId, string brukerNavn, CancellationToken ct = default)
    {
        var g = await db.InterneGjoeremaal.FirstOrDefaultAsync(x => x.Id == gjoeremaalId, ct)
            ?? throw new Valideringsfeil("Gjøremålet finnes ikke.");
        g.Status = GjoeremaalStatus.Fullfoert;
        g.FullfoertAvId = brukerId;
        g.FullfoertAvNavn = brukerNavn;
        g.FullfoertTid = klokke.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task GjenaapneAsync(Guid gjoeremaalId, CancellationToken ct = default)
    {
        var g = await db.InterneGjoeremaal.FirstOrDefaultAsync(x => x.Id == gjoeremaalId, ct)
            ?? throw new Valideringsfeil("Gjøremålet finnes ikke.");
        g.Status = GjoeremaalStatus.Aktiv;
        g.FullfoertAvId = null;
        g.FullfoertAvNavn = null;
        g.FullfoertTid = null;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ Mine oppgaver

    public async Task<IReadOnlyList<MittGjoeremaalDto>> HentMineAsync(string brukerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(brukerId))
        {
            return [];
        }

        var gjoeremaal = await db.InterneGjoeremaal.AsNoTracking()
            .Include(g => g.Ansvarlige)
            .Include(g => g.Runde)
            .Where(g => g.Status == GjoeremaalStatus.Aktiv
                        && g.Ansvarlige.Any(a => a.BrukerId == brukerId))
            .ToListAsync(ct);

        return gjoeremaal
            .OrderBy(g => g.Sorteringsdag is null)
            .ThenBy(g => g.Sorteringsdag)
            .ThenBy(g => g.Tittel)
            .Select(g => new MittGjoeremaalDto(
                Bygg(g),
                g.Runde!.Rundetype,
                g.Runde.Aar,
                Runder.Etikett(g.Runde.Rundetype, g.Runde.Aar)))
            .ToList();
    }

    // -------------------------------------------------------- Ansvarlig-liste

    public async Task<IReadOnlyList<AnsvarligDto>> HentMuligeAnsvarligeAsync(CancellationToken ct = default)
        => await db.Brukere.AsNoTracking()
            .Where(u => u.Funksjonsrolle == Funksjonsrolle.Administrator)
            .OrderBy(u => u.Navn)
            .Select(u => new AnsvarligDto(u.Id, u.Navn))
            .ToListAsync(ct);

    // -------------------------------------------------------- Synkronisering

    public async Task<IReadOnlyList<SynkForslagDto>> ForberedSynkAsync(Guid rundeId, CancellationToken ct = default)
    {
        var runde = await db.InternRunder.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rundeId, ct);
        if (runde is null || runde.Rundetype == Rundetype.Ovrig)
        {
            return [];
        }

        var regler = await HentReglerForAsync(runde.Rundetype, ct);
        var reglerById = regler.ToDictionary(r => r.Id);

        var genererte = await db.InterneGjoeremaal.AsNoTracking()
            .Include(g => g.Ansvarlige)
            .Where(g => g.RundeId == rundeId && g.Opphav == GjoeremaalOpphav.Generert)
            .ToListAsync(ct);
        var genererteByRegel = genererte
            .Where(g => g.GenerertFraRegelId is not null)
            .GroupBy(g => g.GenerertFraRegelId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var forslag = new List<SynkForslagDto>();

        // Nye eller endrede regler.
        foreach (var regel in regler)
        {
            if (!genererteByRegel.TryGetValue(regel.Id, out var eksisterende))
            {
                forslag.Add(new SynkForslagDto(SynkHandling.LeggTil, null, regel.Id, regel.Tittel, "Ny regel siden sist"));
                continue;
            }

            // Sammenlign mot det regelen ville gitt nå. Rør aldri avhukede/manuelt endrede.
            var maal = eksisterende.FirstOrDefault(g => g.Status == GjoeremaalStatus.Aktiv && !g.ManueltEndret);
            if (maal is null)
            {
                continue;
            }

            var frisk = await ByggFraRegelAsync(regel, runde, ct);
            if (!ErLik(maal, frisk))
            {
                forslag.Add(new SynkForslagDto(SynkHandling.Oppdater, maal.Id, regel.Id, regel.Tittel, "Regelen er endret"));
            }
        }

        // Genererte gjøremål hvis regel ikke lenger gjelder runden (slettet eller fjernet rundetype).
        foreach (var g in genererte)
        {
            var regelBorte = g.GenerertFraRegelId is null || !reglerById.ContainsKey(g.GenerertFraRegelId.Value);
            if (regelBorte && g.Status == GjoeremaalStatus.Aktiv && !g.ManueltEndret)
            {
                forslag.Add(new SynkForslagDto(SynkHandling.Fjern, g.Id, null, g.Tittel, "Regelen gjelder ikke lenger"));
            }
        }

        return forslag;
    }

    public async Task SynkroniserAsync(Guid rundeId, IReadOnlyList<SynkForslagDto> godkjente, CancellationToken ct = default)
    {
        var runde = await db.InternRunder.FirstOrDefaultAsync(r => r.Id == rundeId, ct)
            ?? throw new Valideringsfeil("Runden finnes ikke.");

        foreach (var f in godkjente)
        {
            switch (f.Handling)
            {
                case SynkHandling.LeggTil when f.RegelId is { } regelId:
                    var nyRegel = await HentRegelAsync(regelId, ct);
                    if (nyRegel is not null)
                    {
                        db.InterneGjoeremaal.Add(await ByggFraRegelAsync(nyRegel, runde, ct));
                    }
                    break;

                case SynkHandling.Oppdater when f.GjoeremaalId is { } gid && f.RegelId is { } rid:
                    var maal = await db.InterneGjoeremaal
                        .Include(x => x.Ansvarlige)
                        .FirstOrDefaultAsync(x => x.Id == gid, ct);
                    var regel = await HentRegelAsync(rid, ct);
                    if (maal is not null && regel is not null
                        && maal.Status == GjoeremaalStatus.Aktiv && !maal.ManueltEndret)
                    {
                        var frisk = await ByggFraRegelAsync(regel, runde, ct);
                        db.GjoeremaalAnsvarlige.RemoveRange(maal.Ansvarlige);
                        maal.Ansvarlige.Clear();
                        KopierRegelfelter(frisk, maal);
                    }
                    break;

                case SynkHandling.Fjern when f.GjoeremaalId is { } fid:
                    var slett = await db.InterneGjoeremaal.FirstOrDefaultAsync(x => x.Id == fid, ct);
                    if (slett is not null && slett.Status == GjoeremaalStatus.Aktiv && !slett.ManueltEndret)
                    {
                        db.InterneGjoeremaal.Remove(slett);
                    }
                    break;
            }
        }

        runde.SistSynkronisert = klokke.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<GjoeremaalRegel?> HentRegelAsync(Guid id, CancellationToken ct)
        => await db.GjoeremaalRegler.Include(r => r.Ansvarlige).FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>Kopierer innhold/tidfesting fra et friskt regel-generert gjøremål til et eksisterende (beholder Id/status).</summary>
    private static void KopierRegelfelter(InterntGjoeremaal frisk, InterntGjoeremaal maal)
    {
        maal.Tittel = frisk.Tittel;
        maal.Notat = frisk.Notat;
        maal.Tidfestingstype = frisk.Tidfestingstype;
        maal.Dato = frisk.Dato;
        maal.Datopresisjon = frisk.Datopresisjon;
        maal.Datokvalifikator = frisk.Datokvalifikator;
        maal.AnkerLoep = frisk.AnkerLoep;
        maal.AnkerOffsetDager = frisk.AnkerOffsetDager;
        maal.Rundeposisjon = frisk.Rundeposisjon;
        maal.Sorteringsdag = frisk.Sorteringsdag;
        maal.VenterPaaAnker = frisk.VenterPaaAnker;
        foreach (var a in frisk.Ansvarlige)
        {
            maal.Ansvarlige.Add(new GjoeremaalAnsvarlig { Id = Guid.NewGuid(), GjoeremaalId = maal.Id, BrukerId = a.BrukerId, Navn = a.Navn });
        }
    }

    /// <summary>Om et eksisterende gjøremål har samme innhold/tidfesting/ansvarlige som regelen ville gitt nå.</summary>
    private static bool ErLik(InterntGjoeremaal a, InterntGjoeremaal b)
    {
        if (a.Tittel != b.Tittel || a.Notat != b.Notat
            || a.Tidfestingstype != b.Tidfestingstype
            || a.Dato != b.Dato || a.Datokvalifikator != b.Datokvalifikator
            || a.AnkerLoep != b.AnkerLoep || a.AnkerOffsetDager != b.AnkerOffsetDager
            || a.Rundeposisjon != b.Rundeposisjon)
        {
            return false;
        }

        var aAnsv = a.Ansvarlige.Select(x => (x.BrukerId, x.Navn)).OrderBy(x => x.Navn).ToList();
        var bAnsv = b.Ansvarlige.Select(x => (x.BrukerId, x.Navn)).OrderBy(x => x.Navn).ToList();
        return aAnsv.SequenceEqual(bAnsv);
    }

    // ------------------------------------------------------------- Hjelpere

    private static void Valider(GjoeremaalInndata inndata)
    {
        if (string.IsNullOrWhiteSpace(inndata.Tittel))
        {
            throw new Valideringsfeil("Tittel må fylles ut.");
        }
        if (inndata.Tidfestingstype == Tidfestingstype.AnkerRelativ
            && string.IsNullOrWhiteSpace(inndata.AnkerLoep))
        {
            throw new Valideringsfeil("Anker-løp må velges for en anker-relativ tidfesting.");
        }
    }

    private static void SettFelter(InterntGjoeremaal g, GjoeremaalInndata inndata)
    {
        g.Notat = string.IsNullOrWhiteSpace(inndata.Notat) ? null : inndata.Notat.Trim();
        g.Tidfestingstype = inndata.Tidfestingstype;
        g.Dato = inndata.Dato;
        g.Datopresisjon = inndata.Datopresisjon;
        g.Datokvalifikator = inndata.Datokvalifikator;
        g.AnkerLoep = string.IsNullOrWhiteSpace(inndata.AnkerLoep) ? null : inndata.AnkerLoep.Trim();
        g.AnkerOffsetDager = inndata.AnkerOffsetDager;
        g.Rundeposisjon = inndata.Rundeposisjon;

        foreach (var a in inndata.Ansvarlige.Where(a => !string.IsNullOrWhiteSpace(a.Navn)))
        {
            g.Ansvarlige.Add(new GjoeremaalAnsvarlig
            {
                Id = Guid.NewGuid(),
                GjoeremaalId = g.Id,
                BrukerId = string.IsNullOrWhiteSpace(a.BrukerId) ? null : a.BrukerId,
                Navn = a.Navn.Trim()
            });
        }
    }

    /// <summary>Oppløser tidfestingen til Dato/Sorteringsdag/VenterPaaAnker for et gjøremål i en runde.</summary>
    private async Task BeregnTidfestingAsync(InterntGjoeremaal g, InternRunde runde, CancellationToken ct)
    {
        g.VenterPaaAnker = false;

        switch (g.Tidfestingstype)
        {
            case Tidfestingstype.KonkretDato:
                g.Datopresisjon = Datopresisjon.Dag;
                g.Datokvalifikator = null;
                g.Sorteringsdag = g.Dato is { } d ? Datoberegning.Sorteringsdag(d, Datopresisjon.Dag, null) : null;
                break;

            case Tidfestingstype.TentativMaaned:
                g.Datopresisjon = Datopresisjon.Maaned;
                g.Sorteringsdag = g.Dato is { } dm ? Datoberegning.Sorteringsdag(dm, Datopresisjon.Maaned, g.Datokvalifikator) : null;
                break;

            case Tidfestingstype.Rundeposisjon:
                g.Datopresisjon = Datopresisjon.Dag;
                g.Datokvalifikator = null;
                var dag = runde.Aar is { } aar
                    ? Runder.Posisjonsdag(runde.Rundetype, aar, g.Rundeposisjon ?? Domain.Rundeposisjon.Midt)
                    : null;
                g.Dato = dag;
                g.Sorteringsdag = dag;
                break;

            case Tidfestingstype.AnkerRelativ:
                g.Datopresisjon = Datopresisjon.Dag;
                g.Datokvalifikator = null;
                var anker = await FinnAnkerdatoAsync(g.AnkerLoep, runde, ct);
                if (anker is { } a)
                {
                    var res = a.AddDays(g.AnkerOffsetDager);
                    g.Dato = res;
                    g.Sorteringsdag = res;
                }
                else
                {
                    g.VenterPaaAnker = true;
                    g.Dato = null;
                    g.Sorteringsdag = null;
                }
                break;

            default: // Ingen
                g.Sorteringsdag = null;
                break;
        }
    }

    /// <summary>
    /// Finner ankerdatoen for et løp i en runde: en publisert (godkjent) frist på samme løp der
    /// budsjett-/regnskapsåret er rundens år. Returnerer null (→ «venter på ankerdato») hvis ingen finnes.
    /// </summary>
    private async Task<DateOnly?> FinnAnkerdatoAsync(string? ankerLoep, InternRunde runde, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ankerLoep) || runde.Aar is null)
        {
            return null;
        }

        var frist = await db.Frister.AsNoTracking()
            .Where(f => f.Loep == ankerLoep
                        && f.Budsjettaar == runde.Aar
                        && f.Status == FristStatus.Godkjent)
            .OrderBy(f => f.Sorteringsdag)
            .FirstOrDefaultAsync(ct);
        return frist?.Sorteringsdag;
    }

    /// <summary>Reglene som gjelder en rundetype (inkludert rundetyper + ansvarlige).</summary>
    internal async Task<List<GjoeremaalRegel>> HentReglerForAsync(Rundetype type, CancellationToken ct)
        => await db.GjoeremaalRegler
            .Include(r => r.Rundetyper)
            .Include(r => r.Ansvarlige)
            .Where(r => r.Rundetyper.Any(t => t.Rundetype == type))
            .ToListAsync(ct);

    /// <summary>
    /// Bygger et konkret gjøremål fra en generell regel for en gitt runde. Oversetter regelens
    /// årsuavhengige tidfesting til konkrete verdier for rundeåret og oppløser sorteringsdag/anker.
    /// Delt av generering (trinn 2) og synkronisering (trinn 3).
    /// </summary>
    internal async Task<InterntGjoeremaal> ByggFraRegelAsync(GjoeremaalRegel regel, InternRunde runde, CancellationToken ct)
    {
        var g = new InterntGjoeremaal
        {
            Id = Guid.NewGuid(),
            RundeId = runde.Id,
            Tittel = regel.Tittel,
            Notat = regel.Notat,
            Tidfestingstype = regel.Tidfestingstype,
            AnkerLoep = regel.AnkerLoep,
            AnkerOffsetDager = regel.AnkerOffsetDager,
            Rundeposisjon = regel.Rundeposisjon,
            Opphav = GjoeremaalOpphav.Generert,
            GenerertFraRegelId = regel.Id
        };

        var kalaar = (runde.Aar ?? 0) + Runder.Aarforskyvning(runde.Rundetype) + regel.AarforskyvningJustering;
        switch (regel.Tidfestingstype)
        {
            case Tidfestingstype.KonkretDato when regel.Maaned is { } m && regel.Dag is { } d:
                g.Dato = Datoberegning.FastDato(kalaar, m, Math.Min(d, DateTime.DaysInMonth(kalaar, m)));
                break;
            case Tidfestingstype.TentativMaaned when regel.Maaned is { } m:
                g.Dato = new DateOnly(kalaar, m, 1);
                g.Datokvalifikator = regel.Datokvalifikator;
                break;
        }

        foreach (var a in regel.Ansvarlige)
        {
            g.Ansvarlige.Add(new GjoeremaalAnsvarlig { Id = Guid.NewGuid(), GjoeremaalId = g.Id, BrukerId = a.BrukerId, Navn = a.Navn });
        }

        await BeregnTidfestingAsync(g, runde, ct);
        return g;
    }

    private static IReadOnlyList<GjoeremaalDto> SorterOgBygg(List<InterntGjoeremaal> gjoeremaal)
        => gjoeremaal
            .OrderBy(g => g.Status == GjoeremaalStatus.Fullfoert)   // aktive først
            .ThenByDescending(g => g.Status == GjoeremaalStatus.Fullfoert && g.FullfoertTid.HasValue
                ? g.FullfoertTid!.Value.UtcDateTime : DateTime.MinValue)   // ferdige: nyeste øverst
            .ThenBy(g => g.Sorteringsdag is null)                  // aktive: udaterte sist
            .ThenBy(g => g.Sorteringsdag)
            .ThenBy(g => g.Tittel)
            .Select(Bygg)
            .ToList();

    private static GjoeremaalDto Bygg(InterntGjoeremaal g) => new(
        g.Id,
        g.RundeId,
        g.Tittel,
        g.Notat,
        g.Tidfestingstype,
        g.Sorteringsdag,
        g.VenterPaaAnker,
        Tidfestingsformat.Bygg(g.Tidfestingstype, g.Dato, g.Datopresisjon, g.Datokvalifikator,
            g.AnkerLoep, g.AnkerOffsetDager, g.Rundeposisjon, g.VenterPaaAnker),
        g.Status,
        g.FullfoertAvNavn,
        g.FullfoertTid,
        g.Opphav,
        g.ManueltEndret,
        g.Ansvarlige.Select(a => new AnsvarligDto(a.BrukerId, a.Navn)).ToList());
}
