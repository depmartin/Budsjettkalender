using Aarshjul.Application.Datouttrekk;
using Aarshjul.Application.Opplasting;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Datouttrekk;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Opplasting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// Opplastingspipelinen (Fase 2, manuell inntaksvei): samme uttrekk/klassifisering/kø som
/// automatisk innhenting. Bruker en stub-PDF-leser så testene slipper ekte PDF-filer.
/// </summary>
public class OpplastingsTjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();

    private sealed class StubPdftekst(string tekst) : IPdftekst
    {
        public string HentTekst(byte[] innhold) => tekst;
    }

    private OpplastingsTjeneste Tjeneste(string dokumenttekst)
    {
        var synlighetsregel = new Synlighetsregel(Options.Create(new SynlighetsregelOpsjoner()));
        return new OpplastingsTjeneste(
            _t.Db, new StubPdftekst(dokumenttekst), new HeuristiskDatouttrekk(),
            synlighetsregel, new FastKlokke(new DateOnly(2026, 6, 1)));
    }

    private static OpplastetDokument Fil(string navn = "rundskriv.pdf")
        => new(navn, [1, 2, 3]);

    private const string Tekst =
        "Tidsplan for budsjettarbeidet\n" +
        "Frist for innsending av satsingsforslag er 23. januar 2027.\n" +
        "Hovedbudsjettskriv sendes ut 15.03.2027.\n" +
        "Dette avsnittet har ingen dato.\n" +
        "Rapportering til statsregnskapen skjer innen 2027-02-15.\n";

    [Fact]
    public async Task Lager_forslag_av_datolinjer_med_uttrekksbevis()
    {
        var r = await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.Equal(3, r.AntallForslag);
        var forslag = await _t.Db.Forslag.Include(f => f.UttrekksBevis).ToListAsync();
        Assert.Equal(3, forslag.Count);
        Assert.All(forslag, f =>
        {
            Assert.Equal(Opphav.Robot, f.Opphav);
            Assert.StartsWith("Opplastet:", f.KildeEllerInnsender);
            Assert.Equal(FristStatus.Forslag, f.Status);
            Assert.NotNull(f.DokumentId);
            Assert.NotEmpty(f.UttrekksBevis);
        });
    }

    [Fact]
    public async Task Klassifiserer_kjente_loep_som_automatisk()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);
        var forslag = await _t.Db.Forslag.ToListAsync();

        Assert.Contains(forslag, f => f.Loep == "rammefordeling"); // «hovedbudsjettskriv»
        Assert.Contains(forslag, f => f.Loep == "rapportering");   // «rapportering til statsregnskapen»
        Assert.Contains(forslag, f => f.Loep is null);             // satsingsforslag = ukjent type
    }

    [Fact]
    public async Task Registrerer_behandlet_dokument_og_publiserer_ingenting()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.True(await _t.Db.BehandledeDokumenter.AnyAsync(d => d.Kilde == OpplastingsTjeneste.Kilde));
        // Ingenting publiseres uten godkjenning.
        Assert.Empty(await _t.Db.Frister.ToListAsync());
    }

    [Fact]
    public async Task Samme_dokument_gir_ikke_dubletter()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);
        var r2 = await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.True(r2.AlleredeBehandlet);
        Assert.Equal(3, await _t.Db.Forslag.CountAsync());
    }

    [Fact]
    public async Task Uten_datoer_lages_ingen_forslag()
    {
        var r = await Tjeneste("Et notat helt uten datoer i teksten.").LesOgLagForslagAsync(Fil(), 2027);

        Assert.Equal(0, r.AntallForslag);
        Assert.Contains("ingen datoer", r.Melding, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _t.Db.Forslag.ToListAsync());
    }

    public void Dispose() => _t.Dispose();
}
