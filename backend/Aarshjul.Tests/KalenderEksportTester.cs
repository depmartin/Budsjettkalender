using System.Text;
using System.Text.RegularExpressions;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Kalender;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer iCalendar-generatoren (.ics): mange frister samles i én VCALENDAR som VEVENT-er,
/// heldags som standard, tidfestet når fristen har klokkeslett, med korrekt RFC 5545-escaping.
/// </summary>
public class KalenderEksportTester
{
    private static readonly Utskriftsforesporsel AltForesporsel = new(null, "alle", null, null, ErAlt: true);

    private static FristDto Frist(
        string tittel, DateOnly dato, int budsjettaar = 2028,
        Kategori kategori = Kategori.Budsjett, TimeOnly? klokkeslett = null,
        Datopresisjon presisjon = Datopresisjon.Dag, Datokvalifikator? kvalifikator = null,
        string? notat = null)
    {
        var sorteringsdag = Datoberegning.Sorteringsdag(dato, presisjon, kvalifikator);
        return new FristDto(Guid.NewGuid(), tittel, dato, presisjon, kvalifikator, sorteringsdag,
            budsjettaar, kategori, null, null, null, notat, FristStatus.Godkjent, [], klokkeslett);
    }

    private static string GenererTekst(params FristDto[] frister)
    {
        var tjeneste = new IcsEksportTjeneste(TimeProvider.System);
        var bytes = tjeneste.GenererIcs(AltForesporsel, frister);
        return Encoding.UTF8.GetString(bytes);
    }

    [Fact]
    public void Samler_mange_frister_i_en_gyldig_vcalendar()
    {
        var ics = GenererTekst(
            Frist("Hovedbudsjettskriv", new DateOnly(2027, 3, 15)),
            Frist("Gul bok-innlevering", new DateOnly(2027, 9, 1), kategori: Kategori.Gulbok));

        Assert.StartsWith("BEGIN:VCALENDAR", ics);
        Assert.Contains("VERSION:2.0", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        // To hendelser i én fil — poenget med samlet nedlasting.
        Assert.Equal(2, Regex.Matches(ics, "BEGIN:VEVENT").Count);
        Assert.Equal(2, Regex.Matches(ics, "END:VEVENT").Count);
        Assert.Contains("SUMMARY:Hovedbudsjettskriv", ics);
        Assert.Contains("SUMMARY:Gul bok-innlevering", ics);
        // CRLF er RFC-kravet.
        Assert.Contains("\r\n", ics);
    }

    [Fact]
    public void Uten_klokkeslett_blir_heldagshendelse()
    {
        var ics = GenererTekst(Frist("Marskonferanse", new DateOnly(2027, 3, 15)));

        // Heldags: DTSTART/DTEND som DATE (DTEND eksklusiv = dagen etter), ingen klokkeslett.
        Assert.Contains("DTSTART;VALUE=DATE:20270315", ics);
        Assert.Contains("DTEND;VALUE=DATE:20270316", ics);
        Assert.Contains("TRANSP:TRANSPARENT", ics);
        Assert.DoesNotContain("VALARM", ics); // Ingen påminnelse.
    }

    [Fact]
    public void Med_klokkeslett_blir_tidfestet_hendelse_i_utc()
    {
        var ics = GenererTekst(Frist("Møte i budsjettgruppen", new DateOnly(2027, 3, 15), klokkeslett: new TimeOnly(9, 0)));

        // Tidfestet: UTC-form (…T…Z), ikke VALUE=DATE.
        Assert.Matches(@"DTSTART:\d{8}T\d{6}Z", ics);
        Assert.Matches(@"DTEND:\d{8}T\d{6}Z", ics);
        Assert.DoesNotContain("DTSTART;VALUE=DATE", ics);
    }

    [Theory]
    // Norsk klokkeslett konverteres til UTC: sommer (CEST, UTC+2) og vinter (CET, UTC+1).
    [InlineData(2027, 7, 1, "20270701T070000Z")]  // 09:00 Oslo sommer → 07:00 UTC
    [InlineData(2027, 1, 15, "20270115T080000Z")] // 09:00 Oslo vinter → 08:00 UTC
    public void Klokkeslett_konverteres_fra_norsk_tid_til_utc(int aar, int maaned, int dag, string forventetStart)
    {
        var ics = GenererTekst(Frist("Møte", new DateOnly(aar, maaned, dag), klokkeslett: new TimeOnly(9, 0)));

        Assert.Contains($"DTSTART:{forventetStart}", ics);
    }

    [Fact]
    public void Tentativ_frist_merkes_og_er_heldags()
    {
        var ics = GenererTekst(Frist("Regjeringskonferanse", new DateOnly(2027, 8, 15),
            presisjon: Datopresisjon.Maaned, kvalifikator: Datokvalifikator.Medio));

        Assert.Contains("SUMMARY:Regjeringskonferanse (tentativ)", ics);
        Assert.Contains("DTSTART;VALUE=DATE:", ics);
        Assert.DoesNotContain("DTSTART:", ics); // heldags bruker VALUE=DATE, aldri en tidfestet DTSTART:
    }

    [Fact]
    public void Escaper_komma_og_semikolon_i_tekst()
    {
        var ics = GenererTekst(Frist("Frist for satsingsforslag, del 1; se notat", new DateOnly(2027, 1, 23)));

        Assert.Contains(@"SUMMARY:Frist for satsingsforslag\, del 1\; se notat", ics);
    }

    [Fact]
    public void Tomt_utvalg_gir_gyldig_kalender_uten_hendelser()
    {
        var ics = GenererTekst();

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
    }
}
