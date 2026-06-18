namespace Aarshjul.Domain;

/// <summary>
/// Per-felt uttrekksbevis på et robotforslag (SYSTEMARKITEKTUR 3.2). Bærer tolket verdi,
/// tekstutdraget verdien er hentet fra, og et konfidensnivå som tenner et usikkerhetsflagg
/// i grensesnittet. Hører til forslaget, ikke den publiserte fristen.
/// </summary>
public class UttrekksBevis
{
    public Guid Id { get; set; }

    public Guid ForslagId { get; set; }
    public Forslag? Forslag { get; set; }

    /// <summary>Hvilket fristfelt beviset gjelder, f.eks. "dato" eller "tittel".</summary>
    public required string Felt { get; set; }

    /// <summary>Den tolkede verdien for feltet (som tekst).</summary>
    public string? TolketVerdi { get; set; }

    /// <summary>Tekstutdraget verdien er hentet fra (kildespenn).</summary>
    public string? Kildeutdrag { get; set; }

    /// <summary>Konfidens 0–1; lav verdi tenner et per-felt usikkerhetsflagg.</summary>
    public double Konfidens { get; set; }
}
