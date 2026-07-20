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
    public async Task Endret_innhold_gir_endringsforslag_mot_beroert_frist()
    {
        using var t = new Testdatabase();
        var hint = new Opplastingshint { Nummer = 4, Budsjettaar = 2027 };

        await Tjeneste(t.Db, Rammefordelingstekst).LastOppAsync(EnPdf, hint);
        var dok = await t.Db.BehandledeDokumenter.SingleAsync();

        // Simuler at forslaget er godkjent og publisert som en frist koblet til dokumentet.
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = "Hovudbudsjettskriv for 2027",
            Dato = new DateOnly(2026, 1, 23),
            Budsjettaar = 2027,
            Kategori = Kategori.Budsjett,
            Loep = "rammefordeling",
            DokumentId = dok.Id,
            Status = FristStatus.Godkjent
        };
        t.Db.Frister.Add(frist);
        await t.Db.SaveChangesAsync();

        // Ny versjon av samme rundskriv (endret tekst → ny hash).
        var endret = Rammefordelingstekst + "\nOppdatert: fristen er flyttet til 30. januar 2026.";
        var res = await Tjeneste(t.Db, endret).LastOppAsync(EnPdf, hint);

        Assert.Equal(Opplastingsutfall.EndringsforslagOpprettet, res.Utfall);
        var endringsforslag = await t.Db.Forslag.SingleAsync(f => f.ForslagType == ForslagType.Endring);
        Assert.Equal(frist.Id, endringsforslag.EndrerFristId);
        // Endringsforslag rører aldri synlighet.
        Assert.Equal("[]", endringsforslag.ForeslaattSynlighet);
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
