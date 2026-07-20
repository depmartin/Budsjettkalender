using Aarshjul.Application.Datouttrekk;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Opplasting;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datouttrekk;
using Aarshjul.Infrastructure.Opplasting;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer den manuelle opplastingspipelinen (Fase 2): dedup → totrinnsfilter → datouttrekk →
/// <c>Forslag</c> i godkjenningskøen, uten live tilgang til regjeringen.no. Bruker en fast
/// PDF-tekst-double og det deterministiske <see cref="FakeDatouttrekk"/>.
/// </summary>
public class OpplastingTester
{
    private static readonly DateOnly Idag = new(2026, 1, 15);

    private sealed class FastPdf(string tekst) : IPdfTekst
    {
        public string HentTekst(byte[] pdf) => tekst;
    }

    private sealed class FastSynlighetsregel : ISynlighetsregel
    {
        public IReadOnlyList<string> StandardForslagssynlighet() => ["FA", "FIN-FAG"];
    }

    private static OpplastingTjeneste Tjeneste(AppDbContext db, string pdfTekst) =>
        new(db, new FastPdf(pdfTekst), new FakeDatouttrekk(), new FastSynlighetsregel(), new FastKlokke(Idag));

    private static readonly byte[] EnPdf = [0x25, 0x50, 0x44, 0x46]; // «%PDF» — innholdet ignoreres av FastPdf.

    private const string Rammefordelingstekst =
        "Hovudbudsjettskriv for 2027\nRetningslinjer for arbeidet med statsbudsjettet.\n" +
        "Frist for innsending av satsingsforslag er 23. januar 2026.";

    // Ett rundskriv med tre frister på ulike datoer (typisk tidsplan-seksjon).
    private const string Flerfristtekst =
        "Hovudbudsjettskriv for 2027\n" +
        "Frist for innsending av satsingsforslag er 23. januar 2026.\n" +
        "Rammefordelingsmøtene holdes 15. mars 2026.\n" +
        "Endelig rammefordeling foreligger 30. april 2026.";

    [Fact]
    public async Task Ny_pdf_gir_robotforslag_i_koen_med_bevis_og_synlighet()
    {
        using var t = new Testdatabase();
        var res = await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf,
            new Opplastingshint { Nummer = 4, Budsjettaar = 2027 });

        Assert.Equal(Opplastingsutfall.ForslagOpprettet, res.Utfall);
        Assert.Equal("rammefordeling", res.Loep);

        var forslag = await t.Db.Forslag.Include(f => f.UttrekksBevis).SingleAsync();
        Assert.Equal(Opphav.Robot, forslag.Opphav);
        Assert.Equal(ForslagType.NyFrist, forslag.ForslagType);
        Assert.Equal(FristStatus.Forslag, forslag.Status);
        Assert.Equal("rammefordeling", forslag.Loep);
        Assert.Equal(Kategori.Budsjett, forslag.Kategori);
        Assert.NotEmpty(forslag.UttrekksBevis);
        Assert.NotNull(forslag.DokumentId);
        // Synlighet prefylt FIN-internt, aldri POL/FAG automatisk.
        Assert.Contains("FA", forslag.ForeslaattSynlighet);
        Assert.Contains("FIN-FAG", forslag.ForeslaattSynlighet);
        Assert.DoesNotContain("POL", forslag.ForeslaattSynlighet);

        // Dedup-dokument registrert.
        var dok = await t.Db.BehandledeDokumenter.SingleAsync();
        Assert.Equal("r-4-2027", dok.DokumentNokkel);
        Assert.Equal(BehandletStatus.ForslagLaget, dok.BehandletStatus);
    }

    [Fact]
    public async Task Datoen_trekkes_ut_fra_teksten()
    {
        using var t = new Testdatabase();
        await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf,
            new Opplastingshint { Nummer = 4, Budsjettaar = 2027 });

        var forslag = await t.Db.Forslag.SingleAsync();
        Assert.Equal(new DateOnly(2026, 1, 23), forslag.Dato);
        Assert.Equal(Datopresisjon.Dag, forslag.Datopresisjon);
    }

    [Fact]
    public async Task Samme_dokument_paa_nytt_hoppes_over()
    {
        using var t = new Testdatabase();
        var hint = new Opplastingshint { Nummer = 4, Budsjettaar = 2027 };

        var foerste = await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf, hint);
        var andre = await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf, hint);

        Assert.Equal(Opplastingsutfall.ForslagOpprettet, foerste.Utfall);
        Assert.Equal(Opplastingsutfall.Duplikat, andre.Utfall);
        Assert.Equal(1, await t.Db.Forslag.CountAsync());
    }

    [Fact]
    public async Task Flere_frister_i_ett_dokument_gir_ett_forslag_per_frist()
    {
        using var t = new Testdatabase();
        var res = await Tjeneste(t.Db, Flerfristtekst).LastOppAsync(EnPdf,
            new Opplastingshint { Nummer = 4, Budsjettaar = 2027 });

        Assert.Equal(Opplastingsutfall.ForslagOpprettet, res.Utfall);
        Assert.Equal(3, res.AntallForslag);

        var forslag = await t.Db.Forslag.ToListAsync();
        Assert.Equal(3, forslag.Count);
        // Hver frist har sin egen dato ...
        var datoer = forslag.Select(f => f.Dato).OrderBy(d => d).ToArray();
        Assert.Equal(
            new DateOnly?[] { new(2026, 1, 23), new(2026, 3, 15), new(2026, 4, 30) },
            datoer);
        // ... men deler kildedokument og løp (arvet fra dokumentets tittel/nummer).
        Assert.Single(forslag.Select(f => f.DokumentId).Distinct());
        Assert.All(forslag, f => Assert.Equal("rammefordeling", f.Loep));
        Assert.Single(await t.Db.BehandledeDokumenter.ToListAsync());
    }

    [Fact]
    public async Task Endret_versjon_gir_nye_forslag_til_gjennomgang()
    {
        using var t = new Testdatabase();
        var hint = new Opplastingshint { Nummer = 4, Budsjettaar = 2027 };

        await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf, hint);

        // Ny versjon av samme rundskriv (endret tekst → ny hash) med en ekstra frist.
        var endret = Rammefordelingstekst + "\nRammefordelingsmøtene holdes 15. mars 2026.";
        var res = await Tjeneste(t.Db, endret).LastOppAsync(EnPdf, hint);

        Assert.Equal(Opplastingsutfall.EndretVersjon, res.Utfall);
        Assert.Equal(2, res.AntallForslag);
        // Fortsatt bare ett behandlet dokument (samme nøkkel), men flere forslag totalt.
        Assert.Single(await t.Db.BehandledeDokumenter.ToListAsync());
        Assert.Equal(3, await t.Db.Forslag.CountAsync()); // 1 fra første + 2 fra re-uttrekket
    }

    [Fact]
    public async Task Varig_regelverk_ignoreres_uten_forslag()
    {
        using var t = new Testdatabase();
        var res = await Tjeneste(t.Db, "Fullmakter i henhold til bevilgningsreglementet.")
            .LastOppAsync(EnPdf, new Opplastingshint { Nummer = 110, Budsjettaar = 2027 });

        Assert.Equal(Opplastingsutfall.VarigIgnorert, res.Utfall);
        Assert.Equal(0, await t.Db.Forslag.CountAsync());
        Assert.Equal(0, await t.Db.BehandledeDokumenter.CountAsync());
    }

    [Fact]
    public async Task Aarlig_uten_titteltreff_blir_ukjent_type()
    {
        using var t = new Testdatabase();
        var tekst = "Et særskilt rundskriv uten kjent mønster\nInnhold uten gjenkjennelig løp.";
        var res = await Tjeneste(t.Db, tekst).LastOppAsync(EnPdf,
            new Opplastingshint { Nummer = 42, Budsjettaar = 2027 });

        Assert.Equal(Opplastingsutfall.ForslagOpprettet, res.Utfall);
        Assert.True(res.ErUkjentType);

        var forslag = await t.Db.Forslag.SingleAsync();
        Assert.Null(forslag.Loep);
        Assert.True(forslag.ErUkjentTypeForslag());
    }

    [Fact]
    public async Task Pdf_uten_tekst_gir_kunne_ikke_lese()
    {
        using var t = new Testdatabase();
        var res = await Tjeneste(t.Db, "").LastOppAsync(EnPdf,
            new Opplastingshint { Nummer = 4, Budsjettaar = 2027 });

        Assert.Equal(Opplastingsutfall.KunneIkkeLeseTekst, res.Utfall);
        Assert.Equal(0, await t.Db.Forslag.CountAsync());
    }
}

/// <summary>Testhjelper: speiler «ukjent type»-regelen (robotforslag uten gjenkjent løp).</summary>
internal static class ForslagTestutvidelser
{
    public static bool ErUkjentTypeForslag(this Forslag f) =>
        f.Opphav == Opphav.Robot && string.IsNullOrWhiteSpace(f.Loep);
}
