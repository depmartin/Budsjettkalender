namespace Aarshjul.Domain;

/// <summary>
/// Domenelogikk for internkalenderens budsjettrunder (SBR-intern arbeidsliste). En runde er
/// (rundetype, år); hver rundetype har et fast månedsspenn og en kalenderår-forskyvning relativt
/// til budsjettåret, siden årshjulet spenner ~18 måneder. Brukes til å avlede sorteringsdager for
/// rundeposisjoner («tidlig i runden») og konkrete datoer fra generelle regler.
/// </summary>
public static class Runder
{
    /// <summary>Rundetyper som kan generere en konkret plan fra generelle regler (alle unntatt Øvrig).</summary>
    public static readonly Rundetype[] Genererbare =
    [
        Rundetype.Marsrunden,
        Rundetype.Augustrunden,
        Rundetype.Rnb,
        Rundetype.Nysaldering,
        Rundetype.Regnskap
    ];

    /// <summary>Visningsnavn for en rundetype.</summary>
    public static string Navn(Rundetype type) => type switch
    {
        Rundetype.Marsrunden => "Marsrunden",
        Rundetype.Augustrunden => "Augustrunden",
        Rundetype.Rnb => "RNB (revidert nasjonalbudsjett)",
        Rundetype.Nysaldering => "Nysalderingen",
        Rundetype.Regnskap => "Regnskap",
        Rundetype.Ovrig => "Øvrig",
        _ => type.ToString()
    };

    /// <summary>Etikett for en konkret runde, f.eks. «Augustrunden 2027» eller «Øvrig».</summary>
    public static string Etikett(Rundetype type, int? aar)
        => type == Rundetype.Ovrig || aar is null ? Navn(type) : $"{Navn(type)} {aar}";

    /// <summary>Om rundetypen instansieres per år (alle unntatt Øvrig, som er en stående bøtte).</summary>
    public static bool HarAar(Rundetype type) => type != Rundetype.Ovrig;

    /// <summary>
    /// Standard kalenderår-forskyvning relativt til budsjettåret: Mars/August-arbeidet skjer i
    /// år t-1, RNB/Nysaldering i år t. Regnskap og Øvrig bruker året direkte (0). En regel kan
    /// justere ytterligere (f.eks. rapportering som lander i t+1) via egen forskyvning.
    /// </summary>
    public static int Aarforskyvning(Rundetype type) => type switch
    {
        Rundetype.Marsrunden => -1,
        Rundetype.Augustrunden => -1,
        _ => 0
    };

    /// <summary>
    /// Kalendervinduet (fra, til) en rundes arbeid faller i for et gitt rundeår. Brukes for
    /// rundeposisjon og som ramme for anker-oppslag. Øvrig har intet spenn (null).
    /// </summary>
    public static (DateOnly Fra, DateOnly Til)? Spenn(Rundetype type, int aar)
    {
        var kal = aar + Aarforskyvning(type);
        return type switch
        {
            Rundetype.Marsrunden => (new DateOnly(kal, 1, 1), new DateOnly(kal, 4, 30)),
            Rundetype.Augustrunden => (new DateOnly(kal, 7, 1), new DateOnly(kal, 10, 31)),
            Rundetype.Rnb => (new DateOnly(kal, 4, 1), new DateOnly(kal, 5, 31)),
            Rundetype.Nysaldering => (new DateOnly(kal, 10, 1), new DateOnly(kal, 12, 31)),
            Rundetype.Regnskap => (new DateOnly(kal, 1, 1), new DateOnly(kal, 12, 31)),
            _ => null
        };
    }

    /// <summary>
    /// Avleder en sorteringsdag fra en rundeposisjon innenfor rundens spenn. Start = første dag,
    /// Slutt = siste dag, øvrige fordelt jevnt. Returnerer null for runder uten spenn (Øvrig).
    /// </summary>
    public static DateOnly? Posisjonsdag(Rundetype type, int aar, Rundeposisjon posisjon)
    {
        if (Spenn(type, aar) is not { } s)
        {
            return null;
        }

        var lengde = s.Til.DayNumber - s.Fra.DayNumber;
        var andel = posisjon switch
        {
            Rundeposisjon.Start => 0.0,
            Rundeposisjon.Tidlig => 0.25,
            Rundeposisjon.Midt => 0.5,
            Rundeposisjon.Sent => 0.75,
            Rundeposisjon.Slutt => 1.0,
            _ => 0.0
        };
        return s.Fra.AddDays((int)Math.Round(lengde * andel));
    }
}
