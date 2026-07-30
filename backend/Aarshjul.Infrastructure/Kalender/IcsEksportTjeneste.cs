using System.Globalization;
using System.Text;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Kalender;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;

namespace Aarshjul.Infrastructure.Kalender;

/// <summary>
/// Bygger en iCalendar-fil (.ics, RFC 5545) i ren .NET. Én <c>VCALENDAR</c> samler alle fristene
/// i utvalget som hver sin <c>VEVENT</c>, slik at administrator laster ned mange hendelser i én fil
/// og importerer dem samlet i Outlook. Hendelsene er heldags (fristene har ikke klokkeslett),
/// TRANSP:TRANSPARENT (blokkerer ikke «opptatt»-tid) og uten påminnelse.
///
/// Plasseringen bruker <see cref="FristDto.Sorteringsdag"/> — det entydige sorteringspunktet som
/// også tentative frister har (SYSTEMARKITEKTUR 7) — så en «medio august»-frist havner på en dag i
/// kalenderen samtidig som tittelen ærlig merkes «(tentativ)».
/// </summary>
public sealed class IcsEksportTjeneste(TimeProvider klokke) : IKalenderEksport
{
    private static readonly CultureInfo Nb = CultureInfo.GetCultureInfo("nb-NO");
    private const string Crlf = "\r\n";

    /// <summary>Varighet for en tidfestet frist (frister er punkthendelser; en kort blokk gjør dem synlige).</summary>
    private static readonly TimeSpan Varighet = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Norsk tidssone for å konvertere et klokkeslett til UTC i .ics-en. IANA-id først (Linux),
    /// Windows-id som reserve, slik at tidfestede hendelser får riktig tidspunkt uansett vertsplattform.
    /// </summary>
    private static readonly TimeZoneInfo OsloTid = FinnOsloTid();

    public byte[] GenererIcs(Utskriftsforesporsel foresporsel, IReadOnlyList<FristDto> frister)
    {
        var dtstamp = klokke.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var linjer = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Finansdepartementet//Aarshjul for budsjettfrister//NO",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            $"X-WR-CALNAME:{Escape(Kalendernavn(foresporsel))}"
        };

        foreach (var frist in frister.OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Tittel, StringComparer.Ordinal))
        {
            linjer.Add("BEGIN:VEVENT");
            linjer.Add($"UID:{frist.Id}@aarshjul.fin.dep.no");
            linjer.Add($"DTSTAMP:{dtstamp}");
            linjer.AddRange(TidLinjer(frist));
            linjer.Add($"SUMMARY:{Escape(Sammendrag(frist))}");
            var beskrivelse = Beskrivelse(frist);
            if (!string.IsNullOrEmpty(beskrivelse))
            {
                linjer.Add($"DESCRIPTION:{Escape(beskrivelse)}");
            }
            linjer.Add($"CATEGORIES:{Escape(KategoriNavn(frist.Kategori))}");
            linjer.Add("TRANSP:TRANSPARENT");
            linjer.Add("END:VEVENT");
        }

        linjer.Add("END:VCALENDAR");

        var innhold = new StringBuilder();
        foreach (var linje in linjer)
        {
            innhold.Append(Brett(linje)).Append(Crlf);
        }

        return Encoding.UTF8.GetBytes(innhold.ToString());
    }

    /// <summary>
    /// DTSTART/DTEND for hendelsen. Uten klokkeslett: en heldagshendelse på sorteringsdagen
    /// (DTEND eksklusiv = dagen etter). Med klokkeslett: en tidfestet blokk, konvertert fra
    /// norsk tid til UTC (Z-form), slik at Outlook viser riktig tidspunkt uansett lesertidssone.
    /// </summary>
    private static IEnumerable<string> TidLinjer(FristDto frist)
    {
        if (frist.Klokkeslett is not { } klokkeslett)
        {
            var start = frist.Sorteringsdag;
            var slutt = frist.Sorteringsdag.AddDays(1); // DTEND er eksklusiv for heldagshendelser.
            return
            [
                $"DTSTART;VALUE=DATE:{start:yyyyMMdd}",
                $"DTEND;VALUE=DATE:{slutt:yyyyMMdd}"
            ];
        }

        var lokalStart = frist.Dato.ToDateTime(klokkeslett);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(lokalStart, DateTimeKind.Unspecified), OsloTid);
        var utcSlutt = utcStart.Add(Varighet);
        return
        [
            $"DTSTART:{utcStart:yyyyMMdd'T'HHmmss'Z'}",
            $"DTEND:{utcSlutt:yyyyMMdd'T'HHmmss'Z'}"
        ];
    }

    private static TimeZoneInfo FinnOsloTid()
    {
        foreach (var id in new[] { "Europe/Oslo", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc; // Siste utvei: behandle klokkeslettet som UTC framfor å feile eksporten.
    }

    private static string Kalendernavn(Utskriftsforesporsel f)
        => f.ErAlt ? "Budsjettfrister – alle (fullt innsyn)" : $"Budsjettfrister – {f.GruppeEtikett}";

    private static string Sammendrag(FristDto frist)
        => frist.ErTentativ ? $"{frist.Tittel} (tentativ)" : frist.Tittel;

    private static string Beskrivelse(FristDto frist)
    {
        var deler = new List<string>
        {
            $"Kategori: {KategoriNavn(frist.Kategori)}",
            $"Budsjettår: {frist.Budsjettaar}"
        };

        if (frist.ErTentativ)
        {
            deler.Add($"Tentativ dato: {TentativTekst(frist)}");
        }

        if (!string.IsNullOrWhiteSpace(frist.Kilde))
        {
            deler.Add($"Kilde: {frist.Kilde}");
        }

        if (!string.IsNullOrWhiteSpace(frist.Notat))
        {
            deler.Add(frist.Notat!.Trim());
        }

        return string.Join("\n", deler);
    }

    private static string TentativTekst(FristDto frist)
    {
        var maaned = frist.Dato.ToString("MMMM yyyy", Nb);
        var kvalifikator = frist.Datokvalifikator is { } kv ? $"{kv.ToString().ToLower(Nb)} " : "";
        return $"{kvalifikator}{maaned}";
    }

    private static string KategoriNavn(Kategori kategori) => kategori switch
    {
        Kategori.Budsjett => "Budsjett",
        Kategori.Gulbok => "Gul bok",
        Kategori.Regnskap => "Regnskap",
        _ => kategori.ToString()
    };

    /// <summary>
    /// RFC 5545 5.4.1: escape av tekstverdier. Rekkefølgen på backslash først er viktig, ellers
    /// dobbelt-escapes de øvrige. Kolon escapes ikke i verdier.
    /// </summary>
    private static string Escape(string tekst) => tekst
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    /// <summary>
    /// RFC 5545 3.1: innholdslinjer brettes til maks 75 oktetter ved å sette inn CRLF + ett
    /// mellomrom. Vi teller UTF-8-oktetter (norske tegn er flerbyte) og deler aldri midt i et tegn.
    /// </summary>
    private static string Brett(string linje)
    {
        const int maks = 75;
        if (Encoding.UTF8.GetByteCount(linje) <= maks)
        {
            return linje;
        }

        var resultat = new StringBuilder();
        var gjeldende = 0;
        var erFortsettelse = false;
        foreach (var tegn in linje)
        {
            var bytes = Encoding.UTF8.GetByteCount(tegn.ToString());
            // Fortsettelseslinjer starter med ett mellomrom, som teller mot grensen.
            var grense = erFortsettelse ? maks - 1 : maks;
            if (gjeldende + bytes > grense)
            {
                resultat.Append(Crlf).Append(' ');
                gjeldende = 0;
                erFortsettelse = true;
            }

            resultat.Append(tegn);
            gjeldende += bytes;
        }

        return resultat.ToString();
    }
}
