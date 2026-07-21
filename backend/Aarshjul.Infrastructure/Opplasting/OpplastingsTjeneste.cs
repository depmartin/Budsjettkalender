using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aarshjul.Application.Datouttrekk;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Opplasting;
using Aarshjul.Domain;
using Aarshjul.Kilder;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Opplasting;

/// <summary>
/// Leser gjennom et opplastet dokument og lager forslag i godkjenningskøen — samme uttrekk
/// (<see cref="IDatouttrekk"/>), klassifisering (<see cref="Totrinnsfilter"/>) og per-felt-bevis
/// som den automatiske innhentingen. Ingenting publiseres uten godkjenning. Administratorhandling
/// (policy håndheves i web-laget). Dokumentet registreres i <see cref="BehandletDokument"/> med
/// kilde «opplastet» for deduplisering på filnavn + innholdshash.
/// </summary>
public sealed class OpplastingsTjeneste(
    AppDbContext db,
    IPdftekst pdftekst,
    IDatouttrekk datouttrekk,
    ISynlighetsregel synlighetsregel,
    TimeProvider klokke) : IDokumentopplasting
{
    public const string Kilde = "opplastet";
    private const int MaksKandidater = 40;

    public async Task<OpplastingsResultat> LesOgLagForslagAsync(
        OpplastetDokument dokument, int budsjettaar, CancellationToken ct = default)
    {
        var tekst = pdftekst.HentTekst(dokument.Innhold);
        var nokkel = Normaliser(dokument.Filnavn);
        var hash = Innholdshash(tekst);

        var eksisterende = await db.BehandledeDokumenter
            .FirstOrDefaultAsync(d => d.Kilde == Kilde && d.DokumentNokkel == nokkel, ct);
        if (eksisterende is not null && eksisterende.InnholdHash == hash)
        {
            return new OpplastingsResultat
            {
                AlleredeBehandlet = true,
                Melding = "Dette dokumentet er allerede lest inn tidligere — ingen nye forslag laget."
            };
        }

        // Kandidatavsnitt: linjer som inneholder en gjenkjennelig dato (ikke bare et årstall).
        var kandidater = tekst
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length >= 8 && Datogjenkjenning.InneholderDato(l))
            .Take(MaksKandidater)
            .ToList();

        if (kandidater.Count == 0)
        {
            return new OpplastingsResultat
            {
                AntallForslag = 0,
                Melding = "Fant ingen datoer i dokumentet. Du kan eventuelt legge inn fristen manuelt."
            };
        }

        var dok = eksisterende ?? new BehandletDokument
        {
            Id = Guid.NewGuid(),
            Kilde = Kilde,
            DokumentNokkel = nokkel,
            ForstSett = klokke.GetUtcNow()
        };
        dok.InnholdHash = hash;
        dok.Tittel = dokument.Filnavn;
        dok.BehandletStatus = BehandletStatus.ForslagLaget;
        dok.SisteForsoek = klokke.GetUtcNow();
        if (eksisterende is null)
        {
            db.BehandledeDokumenter.Add(dok);
        }

        var standardSynlighet = JsonSerializer.Serialize(synlighetsregel.StandardForslagssynlighet());
        var antall = 0;

        foreach (var avsnitt in kandidater)
        {
            var resultat = await datouttrekk.TrekkUtAsync(avsnitt, budsjettaar, ct);

            // Krever en tolket, konkret dato for å lage et forslag; ellers hoppes avsnittet over.
            if (resultat.Felt(Uttrekksfelter.Dato)?.TolketVerdi is not { } iso
                || !DateOnly.TryParse(iso, out var dato))
            {
                continue;
            }

            var tittelfelt = resultat.Felt(Uttrekksfelter.Tittel);
            var tittel = string.IsNullOrWhiteSpace(tittelfelt?.TolketVerdi)
                ? dokument.Filnavn
                : tittelfelt!.TolketVerdi!;

            // Samme klassifisering som automatisk: kjent løp → løp+kategori; ellers «ukjent type».
            var klass = Totrinnsfilter.Klassifiser(null, avsnitt);
            var loep = klass.Utfall == Klassifiseringsutfall.Gjenkjent ? klass.Loep : null;
            var kategori = klass.Kategori ?? Kategori.Budsjett;

            var forslag = new Forslag
            {
                Id = Guid.NewGuid(),
                ForslagType = ForslagType.NyFrist,
                Opphav = Opphav.Robot,
                KildeEllerInnsender = $"Opplastet: {dokument.Filnavn}",
                Tittel = Trunker(tittel, 512),
                Dato = dato,
                Budsjettaar = budsjettaar,
                Kategori = kategori,
                Loep = loep,
                DokumentId = dok.Id,
                Status = FristStatus.Forslag,
                ForeslaattSynlighet = standardSynlighet
            };
            foreach (var bevis in resultat.TilBevis())
            {
                forslag.UttrekksBevis.Add(bevis);
            }
            db.Forslag.Add(forslag);
            antall++;
        }

        await db.SaveChangesAsync(ct);

        return new OpplastingsResultat
        {
            AntallForslag = antall,
            Melding = antall == 0
                ? "Fant datoer, men klarte ikke tolke dem til frister. Du kan legge inn manuelt."
                : null
        };
    }

    private static string Normaliser(string filnavn)
        => filnavn.Trim().ToLowerInvariant().Replace(" ", "-");

    private static string Innholdshash(string tekst)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tekst)));

    private static string Trunker(string s, int maks)
        => s.Length > maks ? s[..maks] : s;
}
