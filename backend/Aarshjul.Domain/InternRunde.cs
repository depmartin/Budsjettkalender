namespace Aarshjul.Domain;

/// <summary>
/// En konkret oppgaveplan for én budsjettrunde i internkalenderen (SBR-intern). Opprettes som et
/// snapshot av de generelle reglene (trinn 2/3), og eier deretter sine egne gjøremål — inkludert
/// engangsoppgaver lagt til for akkurat denne runden. Kun SBR (administrator) ser internkalenderen.
/// </summary>
public class InternRunde
{
    public Guid Id { get; set; }

    public Rundetype Rundetype { get; set; }

    /// <summary>Budsjettår (Mars/August/RNB/Nysaldering) eller regnskapsår (Regnskap). Null for Øvrig.</summary>
    public int? Aar { get; set; }

    public DateTimeOffset OpprettetTid { get; set; }

    public string? OpprettetAv { get; set; }

    /// <summary>Sist gang runden ble synkronisert mot de generelle reglene (trinn 3).</summary>
    public DateTimeOffset? SistSynkronisert { get; set; }

    public ICollection<InterntGjoeremaal> Gjoeremaal { get; set; } = new List<InterntGjoeremaal>();
}
