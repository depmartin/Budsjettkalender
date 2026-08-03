using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// Seed-settet av generelle regler fra SBRs tidslinjedokument: at det er gyldig, tomt-vernet, og at
/// årsforskyvningen gir riktig kalenderår ved generering (særlig Marsrundens november-oppgaver).
/// </summary>
public class InternkalenderdataTester : IDisposable
{
    private readonly Testdatabase _t = new();

    [Fact]
    public void Seed_settet_er_gyldig_og_bruker_kun_genererbare_runder()
    {
        Assert.NotEmpty(Internkalenderdata.Regler);
        foreach (var r in Internkalenderdata.Regler)
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Tittel));
            Assert.NotEmpty(r.Rundetyper);
            Assert.All(r.Rundetyper, t => Assert.Contains(t, Runder.Genererbare));
            if (r.Type == Tidfestingstype.KonkretDato)
            {
                Assert.InRange(r.Maaned!.Value, 1, 12);
                Assert.InRange(r.Dag!.Value, 1, 31);
            }
        }
    }

    [Fact]
    public async Task SeedRegler_er_tomt_vernet()
    {
        await Internkalenderdata.SeedReglerAsync(_t.Db);
        await _t.Db.SaveChangesAsync();
        var antall = await _t.Db.GjoeremaalRegler.CountAsync();
        Assert.Equal(Internkalenderdata.Regler.Length, antall);

        // Andre kjøring skal ikke duplisere.
        await Internkalenderdata.SeedReglerAsync(_t.Db);
        await _t.Db.SaveChangesAsync();
        Assert.Equal(antall, await _t.Db.GjoeremaalRegler.CountAsync());
    }

    [Fact]
    public async Task Generering_fra_seed_plasserer_marsrundens_novemberoppgave_to_aar_for_budsjettaaret()
    {
        await Internkalenderdata.SeedReglerAsync(_t.Db);
        await _t.Db.SaveChangesAsync();

        var kal = new InternkalenderTjeneste(_t.Db, new FastKlokke(new DateOnly(2026, 1, 1)));
        var runde = await kal.GenererRundeAsync(Rundetype.Marsrunden, 2028, "Test");
        var gjoeremaal = await kal.HentGjoeremaalAsync(runde, null);

        // Novemberoppgaven («Sende ut beskjed …») skal falle i 2026 (to år før budsjettår 2028).
        var nov = gjoeremaal.First(g => g.Tittel.StartsWith("Sende ut beskjed til saksbehandlere"));
        Assert.Equal(2026, nov.Sorteringsdag!.Value.Year);
        Assert.Equal(11, nov.Sorteringsdag.Value.Month);

        // Januaroppgaven («Spesifikasjon av FAGs driftsposter») skal falle i 2027 (ett år før).
        var jan = gjoeremaal.First(g => g.Tittel.StartsWith("Spesifikasjon av FAGs driftsposter"));
        Assert.Equal(2027, jan.Sorteringsdag!.Value.Year);
        Assert.Equal(1, jan.Sorteringsdag.Value.Month);
    }

    [Fact]
    public async Task Foer_hver_runde_reglene_genereres_inn_i_alle_hovedrunder()
    {
        await Internkalenderdata.SeedReglerAsync(_t.Db);
        await _t.Db.SaveChangesAsync();
        var kal = new InternkalenderTjeneste(_t.Db, new FastKlokke(new DateOnly(2026, 1, 1)));

        var mars = await kal.HentGjoeremaalAsync(await kal.GenererRundeAsync(Rundetype.Marsrunden, 2028, "T"), null);
        var rnb = await kal.HentGjoeremaalAsync(await kal.GenererRundeAsync(Rundetype.Rnb, 2028, "T"), null);
        Assert.Contains(mars, g => g.Tittel.StartsWith("Etablere budsjettrom"));
        Assert.Contains(rnb, g => g.Tittel.StartsWith("Etablere budsjettrom"));
    }

    public void Dispose() => _t.Dispose();
}
