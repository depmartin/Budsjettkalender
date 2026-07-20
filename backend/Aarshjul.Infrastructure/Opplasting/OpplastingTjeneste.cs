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
/// Kjører en manuelt opplastet rundskriv-PDF gjennom innhentingspipelinen (Fase 2): tekstuttrekk
/// → dedup mot behandlet-dokument-registeret → totrinns filtrering → datouttrekk → et
/// <see cref="Forslag"/> i godkjenningskøen. Deler forretnings-/dedup-logikken den automatiske
/// bakgrunnsjobben (Steg L) senere skal bruke, og bruker <c>Kilde = "regjeringen"</c> slik at et
/// manuelt opplastet dokument og et framtidig live-oppdaget dokument havner i samme dedup-rom.
/// </summary>
/// <remarks>
/// Minimal dedup i denne runden (designintervju/økt 2026-07-20): kjent nøkkel + uendret hash →
/// hopp over; kjent nøkkel + ny hash → endringsforslag mot den berørte fristen. Auto-
/// versjonsmatching på funksjonsnøkkel og «foreslått fjernet» (som krever EF-migrasjon) tas med
/// full Steg C når hele innhentingen kjøres live.
/// </remarks>
public sealed class OpplastingTjeneste : IOpplasting
{
    private const string KildeKode = "regjeringen";

    private readonly AppDbContext _db;
    private readonly IPdfTekst _pdf;
    private readonly IDatouttrekk _datouttrekk;
    private readonly ISynlighetsregel _synlighetsregel;
    private readonly TimeProvider _klokke;

    public OpplastingTjeneste(
        AppDbContext db,
        IPdfTekst pdf,
        IDatouttrekk datouttrekk,
        ISynlighetsregel synlighetsregel,
        TimeProvider klokke)
    {
        _db = db;
        _pdf = pdf;
        _datouttrekk = datouttrekk;
        _synlighetsregel = synlighetsregel;
        _klokke = klokke;
    }

    public async Task<Opplastingsresultat> LastOppAsync(
        byte[] pdf, Opplastingshint hint, CancellationToken ct = default)
    {
        var tekst = _pdf.HentTekst(pdf);
        if (string.IsNullOrWhiteSpace(tekst))
            return new Opplastingsresultat
            {
                Utfall = Opplastingsutfall.KunneIkkeLeseTekst,
                Melding = "PDF-en inneholdt ingen lesbar tekst."
            };

        var budsjettaar = hint.Budsjettaar ?? Datohjelp.Idag(_klokke).Year;

        // Trinn 1 (nummerserie) tidlig: varig regelverk (100–199) gir ingen frister — hopp uttrekk.
        if (hint.Nummer is >= 100 and <= 199)
            return new Opplastingsresultat
            {
                Utfall = Opplastingsutfall.VarigIgnorert,
                Melding = $"Nummer {hint.Nummer} er varig regelverk (100–199) og gir ingen frister."
            };

        var nokkel = LagNokkel(hint, tekst);
        var hash = Sha256(tekst);

        var eksisterende = await _db.BehandledeDokumenter
            .FirstOrDefaultAsync(d => d.Kilde == KildeKode && d.DokumentNokkel == nokkel, ct);

        // Dedup: kjent nøkkel + uendret innhold → hopp over (også når fristene tidligere ble avvist).
        if (eksisterende is not null && eksisterende.InnholdHash == hash)
            return new Opplastingsresultat
            {
                Utfall = Opplastingsutfall.Duplikat,
                Melding = $"Dokumentet «{nokkel}» er allerede behandlet med uendret innhold — hoppet over."
            };

        // Dokumentnivå: tittel + løp/kategori bestemmes én gang for hele rundskrivet (frister
        // identifiseres på funksjon via dokumentets tittel/nummer, kravdok. 4.3). Hver enkelt
        // uttrukne frist arver løp/kategori, men får sin egen dato og oppgavebeskrivelse.
        var dokumentTittel = hint.Tittel ?? FoersteLinje(tekst) ?? $"Rundskriv {nokkel}";
        var klassifisering = Totrinnsfilter.Klassifiser(hint.Nummer, dokumentTittel);
        var erUkjentType = klassifisering.Utfall == Klassifiseringsutfall.UkjentType;
        var kilderef = hint.Nummer is int nr ? $"R-{nr}/{budsjettaar} (opplastet)" : $"{nokkel} (opplastet)";

        // Trekk ut ALLE fristene i dokumentet — én Uttrekksresultat per frist.
        var frister = await _datouttrekk.TrekkUtAsync(tekst, budsjettaar, ct);

        var erEndretVersjon = eksisterende is not null;
        Guid dokumentId;
        if (eksisterende is null)
        {
            var dok = new BehandletDokument
            {
                Id = Guid.NewGuid(),
                Kilde = KildeKode,
                DokumentNokkel = nokkel,
                InnholdHash = hash,
                Tittel = dokumentTittel,
                ForstSett = _klokke.GetUtcNow(),
                BehandletStatus = BehandletStatus.ForslagLaget,
                SisteForsoek = _klokke.GetUtcNow()
            };
            _db.BehandledeDokumenter.Add(dok);
            dokumentId = dok.Id;
        }
        else
        {
            // Endret versjon (hash avviker — uendret ble allerede silt bort over). Re-uttrekk til
            // gjennomgang. Presis matching av hver frist mot en eksisterende publisert frist
            // (endringsforslag / «foreslått fjernet») er full Steg C og tas når innhentingen går live.
            eksisterende.InnholdHash = hash;
            eksisterende.SisteForsoek = _klokke.GetUtcNow();
            dokumentId = eksisterende.Id;
        }

        var opprettede = new List<Forslag>();
        var harFlagg = false;

        if (frister.Count == 0)
        {
            // Ingen dato gjenkjent — mist ikke dokumentet: ett tentativt forslag til manuell vurdering.
            opprettede.Add(LagForslag(dokumentId, kilderef, dokumentTittel, null, budsjettaar,
                klassifisering, []));
        }
        else
        {
            foreach (var frist in frister)
            {
                var vurdering = Usikkerhetsregler.Vurder(frist, budsjettaar);
                harFlagg |= vurdering.HarFlagg;
                var tittel = frist.Felt(Uttrekksfelter.Tittel)?.TolketVerdi ?? dokumentTittel;
                var dato = ParseDato(frist.Felt(Uttrekksfelter.Dato)?.TolketVerdi);
                opprettede.Add(LagForslag(dokumentId, kilderef, tittel, dato, budsjettaar,
                    klassifisering, frist.TilBevis(), Notat(vurdering)));
            }
        }

        _db.Forslag.AddRange(opprettede);
        await _db.SaveChangesAsync(ct);

        var antall = opprettede.Count;
        return new Opplastingsresultat
        {
            Utfall = erEndretVersjon ? Opplastingsutfall.EndretVersjon : Opplastingsutfall.ForslagOpprettet,
            AntallForslag = antall,
            ForslagId = opprettede.FirstOrDefault()?.Id,
            Loep = klassifisering.Loep,
            ErUkjentType = erUkjentType,
            HarUsikkerhetsflagg = harFlagg,
            Melding = Melding(erEndretVersjon, erUkjentType, antall, klassifisering.Loep)
        };
    }

    private static string Melding(bool endret, bool ukjent, int antall, string? loep)
    {
        if (ukjent)
            return "Årlig rundskriv uten gjenkjent tittel — lagt i køen som «ukjent type» til manuell vurdering.";
        var løpstekst = loep is null ? "" : $" (løp: {loep})";
        var frasetekst = antall == 1 ? "1 forslag" : $"{antall} forslag";
        return endret
            ? $"Endret versjon oppdaget — {frasetekst} lagt i køen til gjennomgang{løpstekst}."
            : $"{frasetekst} lagt i godkjenningskøen{løpstekst}.";
    }

    private Forslag LagForslag(
        Guid dokumentId, string kilderef, string tittel, DateOnly? dato, int budsjettaar,
        Klassifiseringsresultat klassifisering, IReadOnlyList<UttrekksBevis> bevis, string? usikkerhetsnotat = null)
    {
        var harDato = dato is not null;
        return new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = ForslagType.NyFrist,
            Opphav = Opphav.Robot,
            KildeEllerInnsender = kilderef,
            Tittel = tittel,
            Dato = dato,
            Datopresisjon = harDato ? Datopresisjon.Dag : Datopresisjon.Maaned,
            Budsjettaar = budsjettaar,
            Kategori = klassifisering.Kategori ?? Kategori.Budsjett,
            Loep = klassifisering.Loep,
            Notat = usikkerhetsnotat,
            // Auto/robot-forslag prefylles FIN-internt (FA+FIN-FAG), aldri POL/FAG (synlighetsregel).
            ForeslaattSynlighet = JsonSerializer.Serialize(_synlighetsregel.StandardForslagssynlighet()),
            Status = FristStatus.Forslag,
            DokumentId = dokumentId,
            UttrekksBevis = bevis.ToList()
        };
    }

    private static string? FoersteLinje(string tekst)
    {
        foreach (var linje in tekst.Split('\n'))
        {
            var t = linje.Trim();
            if (t.Length >= 8)
                return t.Length > 200 ? t[..200] : t;
        }
        return null;
    }

    private static string? Notat(Usikkerhetsvurdering vurdering) =>
        vurdering.HarFlagg
            ? "Kontrollpunkter fra uttrekk: " + string.Join("; ", vurdering.Flagg.Select(f => f.Forklaring))
            : null;

    /// <summary>Kanonisk dedup-nøkkel <c>r-{nr}-{aar}</c> når nummer er kjent; ellers innholdsavledet.</summary>
    private static string LagNokkel(Opplastingshint hint, string tekst)
    {
        if (hint.Nummer is int nr)
            return $"r-{nr}-{hint.Budsjettaar ?? Datogjenkjenning.ProvAarstall(tekst) ?? 0}";
        return "opplast-" + Sha256(tekst)[..12];
    }

    private static DateOnly? ParseDato(string? iso) =>
        DateOnly.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;

    private static string Sha256(string tekst)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tekst));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
