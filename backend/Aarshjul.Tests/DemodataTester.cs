using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Demo-seeding (lokal kjøremodus): personaer, frister og forslag, idempotent.</summary>
public class DemodataTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public DemodataTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        _t.Db.SaveChanges();
    }

    [Fact]
    public async Task Seeder_personaer_med_manuelle_grupper()
    {
        await Demodata.SeedAsync(_t.Db);

        var admin = await _t.Db.Brukere.Include(u => u.Grupper).SingleAsync(u => u.Id == "demo-admin");
        Assert.Equal(Funksjonsrolle.Administrator, admin.Funksjonsrolle);
        Assert.True(admin.ErFin);
        Assert.All(admin.Grupper, g => Assert.Equal(GruppeMedlemskapKilde.Manuell, g.Kilde));
        Assert.Equal(Demodata.Personaer.Length, await _t.Db.Brukere.CountAsync());
    }

    [Fact]
    public async Task Seeder_frister_og_forslag_i_koeen()
    {
        await Demodata.SeedAsync(_t.Db);

        Assert.True(await _t.Db.Frister.CountAsync() >= 5);
        // Minst ett robot-, ett bruker- og ett endringsforslag til godkjenningskøen.
        Assert.Contains(await _t.Db.Forslag.ToListAsync(), f => f.Opphav == Opphav.Robot);
        Assert.Contains(await _t.Db.Forslag.ToListAsync(), f => f.ForslagType == ForslagType.Endring && f.EndrerFristId != null);
        Assert.True(await _t.Db.Varsler.AnyAsync());
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await Demodata.SeedAsync(_t.Db);
        var antallFrister = await _t.Db.Frister.CountAsync();

        await Demodata.SeedAsync(_t.Db);

        Assert.Equal(antallFrister, await _t.Db.Frister.CountAsync());
        Assert.Equal(Demodata.Personaer.Length, await _t.Db.Brukere.CountAsync());
    }

    public void Dispose() => _t.Dispose();
}
