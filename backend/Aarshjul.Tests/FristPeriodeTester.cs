using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer periodevinduet på <see cref="FristFilter"/> (Steg K-utskrift): kun frister med
/// sorteringsdag i [FraDato, TilDato] returneres, og historikk innenfor vinduet tas med.
/// </summary>
public class FristPeriodeTester : IDisposable
{
    private readonly Testdatabase _t = new();
    // «I dag» er sent i perioden, slik at de tidligste fristene er historikk.
    private static readonly DateOnly Idag = new(2027, 12, 1);

    public FristPeriodeTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();

        LeggTil("Før vinduet", new DateOnly(2026, 12, 31));
        LeggTil("Tidlig i vinduet (historikk)", new DateOnly(2027, 3, 15));
        LeggTil("Sent i vinduet (framover)", new DateOnly(2027, 12, 20));
        LeggTil("Etter vinduet", new DateOnly(2028, 2, 1));
        _t.Db.SaveChanges();
    }

    private void LeggTil(string tittel, DateOnly dato)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = tittel, Dato = dato, Sorteringsdag = dato,
            Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent
        };
        frist.Synlighet.Add(new FristSynlighet { GruppeKode = "FA" });
        _t.Db.Frister.Add(frist);
    }

    [Fact]
    public async Task Periodevindu_returnerer_kun_frister_i_intervallet_inkludert_historikk()
    {
        var tjeneste = new FristTjeneste(_t.Db, new FastKlokke(Idag));
        var filter = new FristFilter
        {
            FraDato = new DateOnly(2027, 1, 1),
            TilDato = new DateOnly(2027, 12, 31),
            InkluderHistorikk = true
        };

        var resultat = await tjeneste.HentSynligeAsync(new Synlighetskontekst(false, ["FA"]), filter);

        var titler = resultat.Select(f => f.Tittel).OrderBy(t => t).ToList();
        Assert.Equal(["Sent i vinduet (framover)", "Tidlig i vinduet (historikk)"], titler);
    }

    public void Dispose() => _t.Dispose();
}
