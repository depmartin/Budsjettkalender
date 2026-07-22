using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Datautveksling;

namespace Aarshjul.Tests;

/// <summary>
/// Eksport/import av årsmaler (gjentaksregler) som JSON-«database» (#2): round-trip bevarer
/// feltene, import erstatter alt, og regler med ugyldige parametre hoppes over med advarsel.
/// </summary>
public class MalDatautvekslingTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2026, 1, 1);

    private MalDatautveksling Tjeneste() => new(_t.Db, new FastKlokke(Idag));

    private void LeggTilRegel(string loep, Regeltype type, string parametre, string tittel = "")
        => _t.Db.Gjentaksregler.Add(new Gjentaksregel
        {
            Id = Guid.NewGuid(), Loep = loep, Tittel = tittel, Kategori = Kategori.Budsjett,
            Regeltype = type, Regelparametre = parametre
        });

    [Fact]
    public async Task Eksport_import_round_trip_bevarer_regler()
    {
        LeggTilRegel("rammefordeling", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}", "Hovedbudsjettskriv");
        LeggTilRegel("fag-innspill", Regeltype.RelativUkedag, "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}", "FAG-innspill");
        await _t.Db.SaveChangesAsync();

        var db = await Tjeneste().EksporterAsync();
        Assert.Equal(2, db.Regler.Count);

        // Tøm og importer tilbake.
        _t.Db.Gjentaksregler.RemoveRange(_t.Db.Gjentaksregler);
        await _t.Db.SaveChangesAsync();

        var r = await Tjeneste().ImporterAsync(db);
        Assert.Equal(2, r.AntallImportert);

        var etter = await Tjeneste().EksporterAsync();
        var fag = Assert.Single(etter.Regler, x => x.Loep == "fag-innspill");
        Assert.Equal(Regeltype.RelativUkedag, fag.Regeltype);
        Assert.Contains("fra_dag", fag.Regelparametre);
    }

    [Fact]
    public async Task Import_erstatter_alle_eksisterende_regler()
    {
        LeggTilRegel("gammel", Regeltype.FastDato, "{\"maaned\":1,\"dag\":1}");
        await _t.Db.SaveChangesAsync();

        var database = new MalDatabase
        {
            EksportertTid = new FastKlokke(Idag).GetUtcNow(),
            Regler = [new MalRegelEksport { Id = Guid.NewGuid(), Loep = "ny", Regeltype = Regeltype.FastDato, Regelparametre = "{\"maaned\":5,\"dag\":2}" }]
        };

        var r = await Tjeneste().ImporterAsync(database);

        Assert.Equal(1, r.AntallImportert);
        Assert.Equal(1, r.AntallErstattet);
        var etter = await Tjeneste().EksporterAsync();
        Assert.Single(etter.Regler);
        Assert.Equal("ny", etter.Regler[0].Loep);
    }

    [Fact]
    public async Task Import_hopper_over_regel_med_ugyldige_parametre()
    {
        var database = new MalDatabase
        {
            Regler =
            [
                new MalRegelEksport { Id = Guid.NewGuid(), Loep = "ok", Regeltype = Regeltype.FastDato, Regelparametre = "{\"maaned\":5,\"dag\":2}" },
                new MalRegelEksport { Id = Guid.NewGuid(), Loep = "raatten", Regeltype = Regeltype.FastDato, Regelparametre = "ikke gyldig json" }
            ]
        };

        var r = await Tjeneste().ImporterAsync(database);

        Assert.Equal(1, r.AntallImportert);
        Assert.Single(r.Advarsler);
        Assert.Single((await Tjeneste().EksporterAsync()).Regler);
    }

    public void Dispose() => _t.Dispose();
}
