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

        var uttrekk = await _datouttrekk.TrekkUtAsync(tekst, budsjettaar, ct);
        var vurdering = Usikkerhetsregler.Vurder(uttrekk, budsjettaar);

        var tittel = hint.Tittel
            ?? uttrekk.Felt(Uttrekksfelter.Tittel)?.TolketVerdi
            ?? $"Rundskriv {nokkel}";

        // Trinn 2 (tittelgjenkjenning) — gjenkjent løp / ukjent type (varig er allerede silt bort).
        var klassifisering = Totrinnsfilter.Klassifiser(hint.Nummer, tittel);
        var erUkjentType = klassifisering.Utfall == Klassifiseringsutfall.UkjentType;

        var dato = ParseDato(uttrekk.Felt(Uttrekksfelter.Dato)?.TolketVerdi);
        var kilderef = hint.Nummer is int nr ? $"R-{nr}/{budsjettaar} (opplastet)" : $"{nokkel} (opplastet)";

        if (eksisterende is null)
        {
            var dok = new BehandletDokument
            {
                Id = Guid.NewGuid(),
                Kilde = KildeKode,
                DokumentNokkel = nokkel,
                InnholdHash = hash,
                Tittel = tittel,
                ForstSett = _klokke.GetUtcNow(),
                BehandletStatus = BehandletStatus.ForslagLaget,
                SisteForsoek = _klokke.GetUtcNow()
            };
            _db.BehandledeDokumenter.Add(dok);

            var forslag = LagForslag(
                ForslagType.NyFrist, dok.Id, kilderef, tittel, dato, budsjettaar, klassifisering, uttrekk, vurdering);
            _db.Forslag.Add(forslag);
            await _db.SaveChangesAsync(ct);

            return new Opplastingsresultat
            {
                Utfall = Opplastingsutfall.ForslagOpprettet,
                ForslagId = forslag.Id,
                Loep = klassifisering.Loep,
                ErUkjentType = erUkjentType,
                HarUsikkerhetsflagg = vurdering.HarFlagg,
                Melding = erUkjentType
                    ? "Årlig rundskriv uten gjenkjent tittel — lagt i køen som «ukjent type» til manuell vurdering."
                    : $"Forslag lagt i godkjenningskøen{(klassifisering.Loep is null ? "" : $" (løp: {klassifisering.Loep})")}."
            };
        }

        // Kjent nøkkel + endret innhold → endringsforslag mot den berørte, publiserte fristen.
        eksisterende.InnholdHash = hash;
        eksisterende.SisteForsoek = _klokke.GetUtcNow();

        var beroert = await _db.Frister
            .Where(f => f.DokumentId == eksisterende.Id && f.Status == FristStatus.Godkjent)
            .Select(f => f.Id)
            .ToListAsync(ct);

        // Entydig berørt frist → endringsforslag. Ellers faller vi tilbake på et nytt-frist-forslag
        // (ingenting går tapt); presis versjonsmatching kommer med full Steg C.
        var erEndring = beroert.Count == 1;
        var endringsforslag = LagForslag(
            erEndring ? ForslagType.Endring : ForslagType.NyFrist,
            eksisterende.Id, kilderef, tittel, dato, budsjettaar, klassifisering, uttrekk, vurdering);
        if (erEndring)
            endringsforslag.EndrerFristId = beroert[0];
        _db.Forslag.Add(endringsforslag);
        await _db.SaveChangesAsync(ct);

        return new Opplastingsresultat
        {
            Utfall = Opplastingsutfall.EndringsforslagOpprettet,
            ForslagId = endringsforslag.Id,
            Loep = klassifisering.Loep,
            ErUkjentType = erUkjentType,
            HarUsikkerhetsflagg = vurdering.HarFlagg,
            Melding = erEndring
                ? "Endret versjon oppdaget — endringsforslag lagt i køen mot den berørte fristen."
                : "Endret versjon oppdaget, men ingen entydig berørt frist — nytt forslag lagt i køen til manuell vurdering."
        };
    }

    private Forslag LagForslag(
        ForslagType type, Guid dokumentId, string kilderef, string tittel, DateOnly? dato,
        int budsjettaar, Klassifiseringsresultat klassifisering, Uttrekksresultat uttrekk,
        Usikkerhetsvurdering vurdering)
    {
        // Endringsforslag rører aldri synlighet (punkt C); kun nytt-frist-forslag prefylles.
        var synlighet = type == ForslagType.Endring
            ? "[]"
            : JsonSerializer.Serialize(_synlighetsregel.StandardForslagssynlighet());

        var harDato = dato is not null;
        return new Forslag
        {
            Id = Guid.NewGuid(),
            ForslagType = type,
            Opphav = Opphav.Robot,
            KildeEllerInnsender = kilderef,
            Tittel = tittel,
            Dato = dato,
            Datopresisjon = harDato ? Datopresisjon.Dag : Datopresisjon.Maaned,
            Budsjettaar = budsjettaar,
            Kategori = klassifisering.Kategori ?? Kategori.Budsjett,
            Loep = klassifisering.Loep,
            Notat = Notat(vurdering),
            ForeslaattSynlighet = synlighet,
            Status = FristStatus.Forslag,
            DokumentId = dokumentId,
            UttrekksBevis = uttrekk.TilBevis().ToList()
        };
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
