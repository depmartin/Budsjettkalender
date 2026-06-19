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

    /// <summary>
    /// Fast dato (kravdok. 3.3 <c>fast_dato</c>): gitt måned/dag i målåret, justert til nærmeste
    /// virkedag ved helg (se <see cref="Virkedagjuster"/>).
    /// </summary>
    public static DateOnly FastDato(int aar, int maaned, int dag)
        => Virkedagjuster(new DateOnly(aar, maaned, dag));

    /// <summary>
    /// N-te forekomst av en ukedag i en måned (kravdok. 3.3 <c>relativ_ukedag</c>, f.eks.
    /// «andre uke i mars, mandag» = 2. mandag). <paramref name="uke"/> teller forekomster fra 1.
    /// Dersom forekomsten ville falle utenfor måneden (f.eks. en 5. mandag som ikke finnes),
    /// klampes den til den siste forekomsten i måneden. Resultatet justeres til nærmeste virkedag.
    /// </summary>
    public static DateOnly NteUkedag(int aar, int maaned, int uke, DayOfWeek ukedag)
    {
        if (uke < 1)
        {
            uke = 1;
        }

        var foerste = new DateOnly(aar, maaned, 1);
        var diffTilUkedag = ((int)ukedag - (int)foerste.DayOfWeek + 7) % 7;
        var resultat = foerste.AddDays(diffTilUkedag + 7 * (uke - 1));

        // Klamp til siste forekomst i måneden om den valgte uken ikke finnes.
        while (resultat.Month != maaned)
        {
            resultat = resultat.AddDays(-7);
        }

        return Virkedagjuster(resultat);
    }

    /// <summary>
    /// Justerer en dato som lander på lørdag/søndag til nærmeste virkedag, men <b>aldri over et
    /// årsskifte</b> (beslutning: justeringen holdes innenfor samme kalenderår). Helligdager
    /// holdes utenfor i første omgang — kun helg. Virkedager returneres uendret.
    /// </summary>
    public static DateOnly Virkedagjuster(DateOnly dato)
    {
        if (dato.DayOfWeek == DayOfWeek.Saturday)
        {
            var fredag = dato.AddDays(-1);
            // Foretrekk fredag; krysser den årsskiftet, gå framover til mandag i stedet.
            return fredag.Year == dato.Year ? fredag : dato.AddDays(2);
        }

        if (dato.DayOfWeek == DayOfWeek.Sunday)
        {
            var mandag = dato.AddDays(1);
            // Foretrekk mandag; krysser den årsskiftet, gå bakover til fredag i stedet.
            return mandag.Year == dato.Year ? mandag : dato.AddDays(-2);
        }

        return dato;
    }
}
