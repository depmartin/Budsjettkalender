using Aarshjul.Infrastructure.Sidetekster;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Redigerbare flate-tekster: lagre/hent og at tom tekst nullstiller (fall tilbake til standard).</summary>
public class SidetekstTester : IDisposable
{
    private readonly Testdatabase _t = new();

    [Fact]
    public async Task Ukjent_nokkel_gir_null_saa_flaten_bruker_standardteksten()
    {
        var tj = new SidetekstTjeneste(_t.Db);
        Assert.Null(await tj.HentAsync("aarshjul.ingress"));
    }

    [Fact]
    public async Task Lagre_og_hent_roundtrip()
    {
        var tj = new SidetekstTjeneste(_t.Db);
        await tj.LagreAsync("aarshjul.ingress", "  Min egen tekst  ");
        Assert.Equal("Min egen tekst", await tj.HentAsync("aarshjul.ingress"));

        // Oppdatering overskriver.
        await tj.LagreAsync("aarshjul.ingress", "Endret");
        Assert.Equal("Endret", await tj.HentAsync("aarshjul.ingress"));
    }

    [Fact]
    public async Task Tom_tekst_fjerner_overstyringen()
    {
        var tj = new SidetekstTjeneste(_t.Db);
        await tj.LagreAsync("k", "noe");
        await tj.LagreAsync("k", "   ");
        Assert.Null(await tj.HentAsync("k"));
    }

    public void Dispose() => _t.Dispose();
}
