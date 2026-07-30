using System.Security.Cryptography;
using Aarshjul.Application;
using Aarshjul.Application.Kalender;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Kalender;

/// <summary>
/// Forvalter kalender-abonnementslenker. Tokenet er 32 tilfeldige byte (256 bit) i url-trygg
/// Base64 — praktisk umulig å gjette, og feed-endepunktet svarer 404 på ukjente/avskrudde tokens
/// slik at lenker ikke kan enumeres. Gruppen valideres mot aktive synlighetsgrupper ved opprettelse.
/// </summary>
public class KalenderabonnementTjeneste(AppDbContext db, TimeProvider klokke) : IKalenderabonnement
{
    public async Task<KalenderabonnementDto> OpprettAsync(string? gruppeKode, string opprettetAv, CancellationToken ct = default)
    {
        string etikett;
        if (string.IsNullOrWhiteSpace(gruppeKode))
        {
            gruppeKode = null;
            etikett = "Alle (fullt innsyn)";
        }
        else
        {
            var gruppe = await db.Synlighetsgrupper
                .FirstOrDefaultAsync(g => g.Aktiv && g.Kode == gruppeKode, ct)
                ?? throw new Valideringsfeil($"Ukjent eller inaktiv gruppe: {gruppeKode}.");
            etikett = gruppe.Navn;
        }

        var abonnement = new Kalenderabonnement
        {
            Id = Guid.NewGuid(),
            Token = LagToken(),
            GruppeKode = gruppeKode,
            Etikett = etikett,
            OpprettetAv = opprettetAv,
            OpprettetTid = klokke.GetUtcNow().UtcDateTime,
            Aktiv = true
        };

        db.Kalenderabonnementer.Add(abonnement);
        await db.SaveChangesAsync(ct);

        return TilDto(abonnement);
    }

    public async Task<IReadOnlyList<KalenderabonnementDto>> HentAlleAsync(CancellationToken ct = default)
    {
        var abonnementer = await db.Kalenderabonnementer
            .AsNoTracking()
            .OrderByDescending(a => a.OpprettetTid)
            .ToListAsync(ct);
        return abonnementer.Select(TilDto).ToList();
    }

    public async Task SettAktivAsync(Guid id, bool aktiv, CancellationToken ct = default)
    {
        var abonnement = await db.Kalenderabonnementer.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new Valideringsfeil("Abonnementslenken finnes ikke.");
        abonnement.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
    }

    public async Task SlettAsync(Guid id, CancellationToken ct = default)
    {
        var abonnement = await db.Kalenderabonnementer.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (abonnement is null)
        {
            return;
        }
        db.Kalenderabonnementer.Remove(abonnement);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Feedutvalg?> HentAktivtUtvalgAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var abonnement = await db.Kalenderabonnementer
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Token == token && a.Aktiv, ct);

        return abonnement is null ? null : new Feedutvalg(abonnement.GruppeKode, abonnement.Etikett);
    }

    private static string LagToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        // Url-trygg Base64 uten utfylling, så tokenet kan stå rått i en URL.
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static KalenderabonnementDto TilDto(Kalenderabonnement a)
        => new(a.Id, a.Token, a.GruppeKode, a.Etikett, a.Aktiv, a.OpprettetTid);
}
