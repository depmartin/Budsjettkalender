using Aarshjul.Application.Datouttrekk;

namespace Aarshjul.Tests;

/// <summary>
/// Verifiserer de deterministiske usikkerhetsreglene og auto-forkast-regelen (Fase 2, punkt 1 /
/// Steg E-forberedelse). Reglene skal være etterprøvbare og uavhengige av modellens egenkonfidens
/// (designintervju 2026-06-19).
/// </summary>
public class UsikkerhetsreglerTester
{
    private static Uttrekksresultat MedDatofelt(string? tolket, string? kilde, double konfidens) =>
        new() { Felter = [new Uttrekksfelt { Felt = Uttrekksfelter.Dato, TolketVerdi = tolket, Kildeutdrag = kilde, Konfidens = konfidens }] };

    [Theory]
    [InlineData("23. januar 2026")]
    [InlineData("23.01.2026")]
    [InlineData("23/1-2026")]
    [InlineData("2026-01-23")]
    public void Datogjenkjenning_kjenner_igjen_datoformater(string tekst)
        => Assert.True(Datogjenkjenning.InneholderDato(tekst));

    [Theory]
    [InlineData("se vedlegg 2")]
    [InlineData("ultimo mars")]
    [InlineData("")]
    [InlineData(null)]
    public void Datogjenkjenning_ikke_dato(string? tekst)
        => Assert.False(Datogjenkjenning.InneholderDato(tekst));

    [Fact]
    public void Regel1_relativ_kilde_tolket_til_hard_dato_flagges()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("31. mars 2026", "innleveres ultimo mars", 0.9), 2026);
        Assert.Contains(v.Flagg, f => f.Grunn == Usikkerhetsgrunn.RelativFormuleringTolketTilDato);
        Assert.False(v.AutoForkast); // gjenkjennelig dato finnes
    }

    [Fact]
    public void Regel2_tolket_dato_uten_dato_i_kildeutdrag_flagges()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("23. januar 2026", "Frist fremgår av vedlegg", 0.9), 2026);
        Assert.Contains(v.Flagg, f => f.Grunn == Usikkerhetsgrunn.KildeutdragUtenDato);
        Assert.DoesNotContain(v.Flagg, f => f.Grunn == Usikkerhetsgrunn.RelativFormuleringTolketTilDato);
    }

    [Fact]
    public void Regel3_dato_utenfor_vindu_flagges()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("23. januar 2030", "23. januar 2030", 0.9), 2026);
        Assert.Contains(v.Flagg, f => f.Grunn == Usikkerhetsgrunn.DatoUtenforForventetVindu);
    }

    [Fact]
    public void Dato_i_vindu_gir_ingen_vindusflagg()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("23. januar 2026", "23. januar 2026", 0.9), 2026);
        Assert.DoesNotContain(v.Flagg, f => f.Grunn == Usikkerhetsgrunn.DatoUtenforForventetVindu);
    }

    [Fact]
    public void Lav_konfidens_alene_flagger_ikke_og_forkaster_ikke()
    {
        // Gyldig dato i både tolket og kilde, men lav konfidens: ingen flagg, ingen auto-forkast.
        var v = Usikkerhetsregler.Vurder(MedDatofelt("23. januar 2026", "23. januar 2026", 0.10), 2026);
        Assert.False(v.HarFlagg);
        Assert.False(v.AutoForkast);
    }

    [Fact]
    public void Auto_forkast_kun_ved_lav_konfidens_OG_ingen_dato()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt(tolket: "uklart", kilde: "ingen tydelig dato her", konfidens: 0.2), 2026);
        Assert.True(v.AutoForkast);
    }

    [Fact]
    public void Ingen_auto_forkast_naar_dato_finnes_selv_ved_lav_konfidens()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("uklart", "kanskje 23. januar 2026", 0.2), 2026);
        Assert.False(v.AutoForkast);
    }

    [Fact]
    public void Ingen_auto_forkast_ved_hoy_konfidens_uten_dato()
    {
        var v = Usikkerhetsregler.Vurder(MedDatofelt("uklart", "ingen dato", 0.95), 2026);
        Assert.False(v.AutoForkast);
    }

    [Fact]
    public void Bevis_mapping_bevarer_felter()
    {
        var res = MedDatofelt("23. januar 2026", "kildeutdrag", 0.8);
        var bevis = res.TilBevis();
        Assert.Single(bevis);
        Assert.Equal(Uttrekksfelter.Dato, bevis[0].Felt);
        Assert.Equal("23. januar 2026", bevis[0].TolketVerdi);
        Assert.Equal("kildeutdrag", bevis[0].Kildeutdrag);
        Assert.Equal(0.8, bevis[0].Konfidens);
        Assert.NotEqual(Guid.Empty, bevis[0].Id);
    }
}
