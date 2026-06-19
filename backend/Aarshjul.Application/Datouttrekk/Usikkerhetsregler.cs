namespace Aarshjul.Application.Datouttrekk;

/// <summary>Hvorfor et felt ble flagget som usikkert.</summary>
public enum Usikkerhetsgrunn
{
    /// <summary>Kilden er relativt formulert (f.eks. «ultimo mars»), men tolket til en konkret dato.</summary>
    RelativFormuleringTolketTilDato,

    /// <summary>En dato ble tolket, men kildeutdraget inneholder ingen gjenkjennelig dato.</summary>
    KildeutdragUtenDato,

    /// <summary>Tolket dato faller utenfor det forventede vinduet for budsjettløpet.</summary>
    DatoUtenforForventetVindu
}

/// <summary>Et usikkerhetsflagg på ett felt, med en forklaring administrator kan handle på.</summary>
public sealed record Feltflagg(string Felt, Usikkerhetsgrunn Grunn, string Forklaring);

/// <summary>Resultatet av en usikkerhetsvurdering: flagg per felt og om uttrekket auto-forkastes.</summary>
public sealed record Usikkerhetsvurdering
{
    public IReadOnlyList<Feltflagg> Flagg { get; init; } = [];
    public bool AutoForkast { get; init; }
    public bool HarFlagg => Flagg.Count > 0;
}

/// <summary>
/// Deterministiske, verifiserbare regler for usikkerhetsflagg på robotuttrekk (SYSTEMARKITEKTUR 5,
/// designintervju 2026-06-19). Konfidens er ett bidrag, <b>aldri</b> eneste utløser av et flagg.
/// Auto-forkast skjer kun når uttrekket er <b>både</b> lav konfidens <b>og</b> uten gjenkjennelig
/// dato — og er aldri stille (forkastede uttrekk havner i den reverserbare forkastet-listen).
/// </summary>
public static class Usikkerhetsregler
{
    public const double LavKonfidensTerskel = 0.5;

    public static Usikkerhetsvurdering Vurder(
        Uttrekksresultat resultat, int budsjettaar, double lavKonfidensTerskel = LavKonfidensTerskel)
    {
        var flagg = new List<Feltflagg>();
        var dato = resultat.Felt(Uttrekksfelter.Dato);

        var harGjenkjenneligDato = dato is not null &&
            (Datogjenkjenning.InneholderDato(dato.TolketVerdi) || Datogjenkjenning.InneholderDato(dato.Kildeutdrag));

        if (dato is not null)
        {
            var tolketErDato = Datogjenkjenning.InneholderDato(dato.TolketVerdi);

            // Regel 1 — relativ kilde tolket til hard dato.
            if (tolketErDato && Datogjenkjenning.ErRelativFormulering(dato.Kildeutdrag))
                flagg.Add(new Feltflagg(Uttrekksfelter.Dato, Usikkerhetsgrunn.RelativFormuleringTolketTilDato,
                    "Kilden er relativt formulert, men er tolket til en konkret dato — kontroller."));

            // Regel 2 — tolket dato uten gjenkjennelig dato i kildeutdraget.
            if (tolketErDato && !Datogjenkjenning.InneholderDato(dato.Kildeutdrag))
                flagg.Add(new Feltflagg(Uttrekksfelter.Dato, Usikkerhetsgrunn.KildeutdragUtenDato,
                    "Tolket dato har ingen gjenkjennelig dato i kildeutdraget — kontroller."));

            // Regel 3 — dato utenfor forventet budsjettløpsvindu (~18 mnd: budsjettaar-2 .. budsjettaar+1).
            if (Datogjenkjenning.ProvAarstall(dato.TolketVerdi) is int aar &&
                (aar < budsjettaar - 2 || aar > budsjettaar + 1))
                flagg.Add(new Feltflagg(Uttrekksfelter.Dato, Usikkerhetsgrunn.DatoUtenforForventetVindu,
                    $"Tolket årstall {aar} er utenfor forventet vindu for budsjettår {budsjettaar}."));
        }

        // Auto-forkast: lav konfidens OG ingen gjenkjennelig dato. Aldri stille (forkastet-listen, Steg C/F).
        var datokonfidens = dato?.Konfidens ?? 0d;
        var autoForkast = datokonfidens < lavKonfidensTerskel && !harGjenkjenneligDato;

        return new Usikkerhetsvurdering { Flagg = flagg, AutoForkast = autoForkast };
    }
}
