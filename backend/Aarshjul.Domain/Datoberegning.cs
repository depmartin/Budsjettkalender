namespace Aarshjul.Domain;

/// <summary>
/// Beregner en entydig sorteringsdag for en frist (SYSTEMARKITEKTUR 3.1, 7).
/// Selv tentative frister (månedspresisjon) får ett kronologisk punkt, slik at
/// landingsflate, historikkgrense og alle tre visninger fungerer uendret.
/// </summary>
public static class Datoberegning
{
    /// <summary>
    /// Avledet sorteringsdag ut fra referansedato, presisjon og eventuell kvalifikator:
    /// dag → selve datoen; primo → den 1.; medio → den 15.; ultimo → siste dag i måneden;
    /// ren måned (ingen kvalifikator) → den 1.
    /// </summary>
    public static DateOnly Sorteringsdag(DateOnly dato, Datopresisjon presisjon, Datokvalifikator? kvalifikator)
    {
        if (presisjon == Datopresisjon.Dag)
        {
            return dato;
        }

        var aar = dato.Year;
        var maaned = dato.Month;
        var dag = kvalifikator switch
        {
            Datokvalifikator.Medio => 15,
            Datokvalifikator.Ultimo => DateTime.DaysInMonth(aar, maaned),
            // Primo og ren månedsangivelse:
            _ => 1
        };

        return new DateOnly(aar, maaned, dag);
    }
}
