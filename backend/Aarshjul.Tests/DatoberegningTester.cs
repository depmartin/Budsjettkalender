using Aarshjul.Domain;

namespace Aarshjul.Tests;

/// <summary>Verifiserer avledet sorteringsdag for tentative frister (SYSTEMARKITEKTUR 3.1, 7).</summary>
public class DatoberegningTester
{
    [Fact]
    public void Dagspresisjon_gir_selve_datoen()
    {
        var dato = new DateOnly(2026, 3, 23);
        var resultat = Datoberegning.Sorteringsdag(dato, Datopresisjon.Dag, null);
        Assert.Equal(dato, resultat);
    }

    [Fact]
    public void Primo_gir_foerste_i_maaneden()
        => Assert.Equal(new DateOnly(2026, 3, 1),
            Datoberegning.Sorteringsdag(new DateOnly(2026, 3, 20), Datopresisjon.Maaned, Datokvalifikator.Primo));

    [Fact]
    public void Medio_gir_den_15()
        => Assert.Equal(new DateOnly(2026, 3, 15),
            Datoberegning.Sorteringsdag(new DateOnly(2026, 3, 2), Datopresisjon.Maaned, Datokvalifikator.Medio));

    [Fact]
    public void Ultimo_gir_siste_dag_i_maaneden()
        => Assert.Equal(new DateOnly(2026, 2, 28),
            Datoberegning.Sorteringsdag(new DateOnly(2026, 2, 10), Datopresisjon.Maaned, Datokvalifikator.Ultimo));

    [Fact]
    public void Ren_maaned_uten_kvalifikator_gir_foerste()
        => Assert.Equal(new DateOnly(2026, 8, 1),
            Datoberegning.Sorteringsdag(new DateOnly(2026, 8, 25), Datopresisjon.Maaned, null));

    [Fact]
    public void Kontekst_beregnes_automatisk_ved_lagring()
    {
        var frist = new Frist
        {
            Tittel = "Medio august",
            Dato = new DateOnly(2026, 8, 25),
            Datopresisjon = Datopresisjon.Maaned,
            Datokvalifikator = Datokvalifikator.Medio,
            Budsjettaar = 2027
        };
        frist.OppdaterSorteringsdag();
        Assert.Equal(new DateOnly(2026, 8, 15), frist.Sorteringsdag);
    }
}
