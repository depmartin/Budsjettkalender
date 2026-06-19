using Aarshjul.Domain;
using Aarshjul.Infrastructure.Varsler;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>Verifiserer varsel-innboksen (Fase 2, Steg J): bruker ser kun egne varsler, og kan kvittere ut.</summary>
public class VarseltjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();

    private Varseltjeneste Tjeneste() => new(_t.Db);

    private Guid LeggVarsel(string brukerId, bool lest = false)
    {
        var v = new Varsel { Id = Guid.NewGuid(), BrukerId = brukerId, Tekst = "Forslaget ditt er behandlet.", Lest = lest };
        _t.Db.Varsler.Add(v);
        _t.Db.SaveChanges();
        return v.Id;
    }

    [Fact]
    public async Task Henter_kun_egne_varsler_nyeste_forst()
    {
        LeggVarsel("bruker-1");
        LeggVarsel("bruker-1");
        LeggVarsel("annen");

        var mine = await Tjeneste().HentForBrukerAsync("bruker-1");
        Assert.Equal(2, mine.Count);
    }

    [Fact]
    public async Task Teller_uleste()
    {
        LeggVarsel("bruker-1");
        LeggVarsel("bruker-1", lest: true);

        Assert.Equal(1, await Tjeneste().AntallUlesteAsync("bruker-1"));
    }

    [Fact]
    public async Task Markerer_ett_som_lest_kun_for_eier()
    {
        var id = LeggVarsel("bruker-1");

        // Annen bruker kan ikke kvittere ut mitt varsel.
        await Tjeneste().MarkerLestAsync(id, "inntrenger");
        Assert.False((await _t.Db.Varsler.SingleAsync(v => v.Id == id)).Lest);

        await Tjeneste().MarkerLestAsync(id, "bruker-1");
        Assert.True((await _t.Db.Varsler.SingleAsync(v => v.Id == id)).Lest);
    }

    [Fact]
    public async Task Markerer_alle_lest()
    {
        LeggVarsel("bruker-1");
        LeggVarsel("bruker-1");

        await Tjeneste().MarkerAlleLestAsync("bruker-1");
        Assert.Equal(0, await Tjeneste().AntallUlesteAsync("bruker-1"));
    }

    public void Dispose() => _t.Dispose();
}
