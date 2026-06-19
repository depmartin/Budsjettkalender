namespace Aarshjul.Kilder;

/// <summary>
/// Utfall av en <see cref="IKilde.OppdagAsync"/>-kjøring. Grensesnittet uttrykker utfall,
/// ikke bare data, slik at en tom liste fra en vellykket kjøring (<see cref="IngenDokumenter"/>)
/// aldri forveksles med en feilet kjøring (<see cref="KlarteIkkeParse"/>) — en stille feil må
/// ikke se ut som en stille periode uten nye rundskriv (beslutning 2026-06-18, SYSTEMARKITEKTUR 5).
/// </summary>
/// <remarks>
/// Utfallet beskriver hva kilden faktisk kan observere: om oversikten lot seg lese og om den
/// inneholdt dokumentrader. Hvilke av dokumentene som er <em>nye</em> avgjøres nedstrøms ved
/// dedup mot behandlet-dokument-registeret (Steg C), ikke av kilden.
/// </remarks>
public enum Oppdagutfall
{
    /// <summary>Oversikten ble lest, og minst ett dokument ble funnet.</summary>
    FantDokumenter,

    /// <summary>Oversikten ble lest greit, men inneholdt ingen dokumentrader.</summary>
    IngenDokumenter,

    /// <summary>Oversikten lot seg ikke tolke (f.eks. endret sidestruktur). Varsles til administrator.</summary>
    KlarteIkkeParse
}

/// <summary>
/// Resultatet av <see cref="IKilde.OppdagAsync"/>: utfallet og de oppdagede referansene.
/// </summary>
public sealed record OppdagResultat
{
    public required Oppdagutfall Utfall { get; init; }

    /// <summary>Oppdagede dokumentreferanser (ufiltrert; dedup skjer i Steg C). Tom ved feil.</summary>
    public IReadOnlyList<Dokumentreferanse> Dokumenter { get; init; } = [];

    /// <summary>Beskrivelse når <see cref="Utfall"/> er <see cref="Oppdagutfall.KlarteIkkeParse"/>.</summary>
    public string? Feilmelding { get; init; }

    public static OppdagResultat Fant(IReadOnlyList<Dokumentreferanse> dokumenter) =>
        dokumenter.Count == 0
            ? new OppdagResultat { Utfall = Oppdagutfall.IngenDokumenter }
            : new OppdagResultat { Utfall = Oppdagutfall.FantDokumenter, Dokumenter = dokumenter };

    public static OppdagResultat ParseFeil(string feilmelding) =>
        new() { Utfall = Oppdagutfall.KlarteIkkeParse, Feilmelding = feilmelding };
}
