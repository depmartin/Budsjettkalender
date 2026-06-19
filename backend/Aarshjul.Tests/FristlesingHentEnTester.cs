using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer at <see cref="FristTjeneste.HentEnAsync"/> håndhever synlighet på server: en
/// enkelt frist hentes kun når konteksten har rett til å se den og fristen er godkjent.
/// Brukes når en bidragsyter skal foreslå endring på en publisert frist (Steg I).
/// </summary>
public class FristlesingHentEnTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2026, 1, 1);

    private readonly Guid _godkjentFa;
    private readonly Guid _forslag;

    public FristlesingHentEnTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();

        _godkjentFa = LeggTilFrist("Godkjent FA", FristStatus.Godkjent, "FA");
        _forslag = LeggTilFrist("Forslag FA", FristStatus.Forslag, "FA");
        _t.Db.SaveChanges();
    }

    private Guid LeggTilFrist(string tittel, FristStatus status, params string[] grupper)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = tittel,
            Dato = new DateOnly(2026, 6, 1),
            Budsjettaar = 2027,
            Kategori = Kategori.Budsjett,
            Status = status
        };
        foreach (var g in grupper)
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        _t.Db.Frister.Add(frist);
        return frist.Id;
    }

    private FristTjeneste Tjeneste() => new(_t.Db, new FastKlokke(Idag));

    [Fact]
    public async Task Henter_godkjent_frist_naar_konteksten_ser_den()
    {
        var resultat = await Tjeneste().HentEnAsync(new Synlighetskontekst(false, ["FA"]), _godkjentFa);

        Assert.NotNull(resultat);
        Assert.Equal("Godkjent FA", resultat!.Tittel);
    }

    [Fact]
    public async Task Returnerer_null_naar_konteksten_ikke_har_matchende_gruppe()
    {
        var resultat = await Tjeneste().HentEnAsync(new Synlighetskontekst(false, ["FAG"]), _godkjentFa);

        Assert.Null(resultat);
    }

    [Fact]
    public async Task Returnerer_null_for_frist_som_ikke_er_godkjent()
    {
        // Selv en administrator (ser alt) får ikke et forslag via leseporten — kun godkjente frister.
        var resultat = await Tjeneste().HentEnAsync(new Synlighetskontekst(true, []), _forslag);

        Assert.Null(resultat);
    }

    [Fact]
    public async Task Returnerer_null_naar_id_ikke_finnes()
    {
        var resultat = await Tjeneste().HentEnAsync(new Synlighetskontekst(true, []), Guid.NewGuid());

        Assert.Null(resultat);
    }

    public void Dispose() => _t.Dispose();
}
