using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aarshjul.Infrastructure.Brukere;

/// <summary>
/// Oppretter/oppdaterer brukeren fra token-claims og synkroniserer Entra-utledede
/// gruppemedlemskap, uten å røre manuelt satte medlemskap (beslutning 2026-06-18).
/// Setter aldri administratorrolle automatisk og mapper aldri POL automatisk.
/// </summary>
public class BrukeroppslagTjeneste(
    AppDbContext db,
    IOptions<EntraGruppeOpsjoner> opsjoner,
    IOptions<AdministratortilgangOpsjoner> adminOpsjoner) : IBrukeroppslag
{
    private const string PolKode = "POL";
    private static readonly string[] FinGrupper = ["FA", "FIN-FAG"];

    private readonly EntraGruppeOpsjoner _opsjoner = opsjoner.Value;
    private readonly AdministratortilgangOpsjoner _adminOpsjoner = adminOpsjoner.Value;

    public async Task<GjeldendeBruker> HentEllerOpprettAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var id = HentId(principal) ?? throw new InvalidOperationException("Mangler stabil bruker-id (oid/sub) i token.");
        var navn = HentNavn(principal) ?? id;

        var entraKoder = await UtledEntraGrupperAsync(principal, ct);
        var erAdminGruppe = ErIAdminGruppe(principal);
        // SBR er i Finansavdelingen; admin-gruppemedlemskap innebærer FIN-tilhørighet.
        var erFin = erAdminGruppe || entraKoder.Any(k => FinGrupper.Contains(k));

        var bruker = await db.Brukere
            .Include(u => u.Grupper)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (bruker is null)
        {
            bruker = new Bruker
            {
                Id = id,
                Navn = navn,
                ErFin = erFin,
                // Standard: bidragsyter for FIN, leser for øvrige. Admin gis kun av admin-gruppen.
                Funksjonsrolle = erFin ? Funksjonsrolle.Bidragsyter : Funksjonsrolle.Leser
            };
            db.Brukere.Add(bruker);
        }
        else
        {
            bruker.Navn = navn;
            // Hev til FIN hvis Entra nå tilsier det; nedgrader aldri en eksisterende administrator her.
            if (erFin)
            {
                bruker.ErFin = true;
            }
        }

        // Medlemskapsstyrt administratortilgang (beslutning 2026-07-30): tilhører brukeren en
        // konfigurert admin-gruppe (SBR) blir vedkommende administrator. Forlater brukeren
        // gruppen, mister en *automatisk* tildelt administrator rollen igjen — men en seedet/
        // manuelt satt administrator (AdminViaEntra == false) røres aldri.
        if (erAdminGruppe)
        {
            bruker.Funksjonsrolle = Funksjonsrolle.Administrator;
            bruker.AdminViaEntra = true;
        }
        else if (bruker.AdminViaEntra)
        {
            bruker.Funksjonsrolle = bruker.ErFin ? Funksjonsrolle.Bidragsyter : Funksjonsrolle.Leser;
            bruker.AdminViaEntra = false;
        }

        SynkroniserEntraGrupper(bruker, entraKoder);
        await db.SaveChangesAsync(ct);

        var alleKoder = bruker.Grupper.Select(g => g.GruppeKode).Distinct().ToList();
        return new GjeldendeBruker(bruker.Id, bruker.Navn, bruker.Funksjonsrolle, alleKoder);
    }

    /// <summary>Sant når principalen bærer en claim som matcher en konfigurert admin-gruppe (SBR).</summary>
    private bool ErIAdminGruppe(ClaimsPrincipal principal)
    {
        if (_adminOpsjoner.Gruppeider.Count == 0)
        {
            return false;
        }

        var admingrupper = new HashSet<string>(_adminOpsjoner.Gruppeider, StringComparer.OrdinalIgnoreCase);
        return principal.FindAll(_adminOpsjoner.KildeClaimType).Any(c => admingrupper.Contains(c.Value));
    }

    /// <summary>Bytter ut Entra-utledede medlemskap, men beholder manuelt satte.</summary>
    private static void SynkroniserEntraGrupper(Bruker bruker, IReadOnlyCollection<string> entraKoder)
    {
        var entraNaa = bruker.Grupper.Where(g => g.Kilde == GruppeMedlemskapKilde.Entra).ToList();

        foreach (var g in entraNaa.Where(g => !entraKoder.Contains(g.GruppeKode)))
        {
            bruker.Grupper.Remove(g);
        }

        foreach (var kode in entraKoder)
        {
            if (!bruker.Grupper.Any(g => g.GruppeKode == kode))
            {
                bruker.Grupper.Add(new BrukerGruppe
                {
                    BrukerId = bruker.Id,
                    GruppeKode = kode,
                    Kilde = GruppeMedlemskapKilde.Entra
                });
            }
        }
    }

    private async Task<IReadOnlyList<string>> UtledEntraGrupperAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var koder = principal.FindAll(_opsjoner.KildeClaimType)
            .Select(c => c.Value)
            .Where(_opsjoner.AttributtTilGruppe.ContainsKey)
            .Select(v => _opsjoner.AttributtTilGruppe[v])
            .Where(k => !string.Equals(k, PolKode, StringComparison.OrdinalIgnoreCase)) // POL settes aldri automatisk
            .Distinct()
            .ToList();

        if (koder.Count == 0)
        {
            return koder;
        }

        // Bare aktive, eksisterende grupper kan tildeles (unngår FK-brudd og døde koder).
        var gyldige = await db.Synlighetsgrupper
            .Where(g => g.Aktiv && koder.Contains(g.Kode))
            .Select(g => g.Kode)
            .ToListAsync(ct);

        return gyldige;
    }

    private static string? HentId(ClaimsPrincipal p)
        => p.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
           ?? p.FindFirst("oid")?.Value
           ?? p.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? p.FindFirst("sub")?.Value;

    private static string? HentNavn(ClaimsPrincipal p)
        => p.FindFirst("name")?.Value
           ?? p.FindFirst(ClaimTypes.Name)?.Value
           ?? p.FindFirst("preferred_username")?.Value;
}
