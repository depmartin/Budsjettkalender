using Aarshjul.Domain;
using Aarshjul.Kilder;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer totrinns filtrering (Fase 2, Steg D): nummerserie + tittelgjenkjenning, og det
/// konservative sikkerhetsnettet «ukjent type» (kravdok. 4.3).
/// </summary>
public class TotrinnsfilterTester
{
    [Theory]
    [InlineData(4, "Hovudbudsjettskriv for 2027", "rammefordeling", Kategori.Budsjett)]
    [InlineData(9, "Materialet til regjeringens konferanse i mars 2027", "marskonferanse", Kategori.Budsjett)]
    [InlineData(6, "Bekreftelsesbrev og innlevering av tekst til Gul bok", "gulbok", Kategori.Gulbok)]
    [InlineData(10, "Rapportering til statsrekneskapen", "rapportering", Kategori.Regnskap)]
    public void Gjenkjenner_kjent_loep_paa_tittel(int nummer, string tittel, string forventetLoep, Kategori forventetKategori)
    {
        var r = Totrinnsfilter.Klassifiser(nummer, tittel);

        Assert.Equal(Klassifiseringsutfall.Gjenkjent, r.Utfall);
        Assert.Equal(forventetLoep, r.Loep);
        Assert.Equal(forventetKategori, r.Kategori);
    }

    [Fact]
    public void Nummer_uavhengig_av_tittelmatch()
    {
        // Nummeret er kun et hint: et avvikende nummer skal ikke hindre titteltreff.
        var r = Totrinnsfilter.Klassifiser(42, "Hovedbudsjettskriv for 2028");
        Assert.Equal(Klassifiseringsutfall.Gjenkjent, r.Utfall);
        Assert.Equal("rammefordeling", r.Loep);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(199)]
    public void Varig_regelverk_ignoreres(int nummer)
    {
        var r = Totrinnsfilter.Klassifiser(nummer, "Et eller annet regelverk");
        Assert.Equal(Klassifiseringsutfall.Ignorer, r.Utfall);
    }

    [Fact]
    public void Aarlig_uten_tittelmatch_blir_ukjent_type()
    {
        var r = Totrinnsfilter.Klassifiser(5, "Et helt nytt rundskriv ingen kjenner");
        Assert.Equal(Klassifiseringsutfall.UkjentType, r.Utfall);
        Assert.Null(r.Loep);
    }

    [Fact]
    public void Ukjent_nummer_uten_match_kastes_ikke_men_blir_ukjent_type()
    {
        // Manglende nummer skal ikke føre til stille bortfiltrering (aldri miste en frist).
        var r = Totrinnsfilter.Klassifiser(null, "Uvanlig formulert tittel");
        Assert.Equal(Klassifiseringsutfall.UkjentType, r.Utfall);
    }
}
