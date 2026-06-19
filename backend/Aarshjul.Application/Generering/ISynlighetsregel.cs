namespace Aarshjul.Application.Generering;

/// <summary>
/// Forhåndsutfyller foreslått synlighet for auto-/robot-/genererte forslag (kravdok. 2.4).
/// Default er FIN-internt (FA + FIN-FAG) — <b>aldri</b> FAG og <b>aldri</b> POL automatisk
/// (designintervju 2026-06-19; POL settes kun ved administrators aktive valg). Gjelder ikke
/// manuell opprettelse, som alltid krever aktivt synlighetsvalg.
/// </summary>
public interface ISynlighetsregel
{
    /// <summary>Standard foreslått synlighet (gruppekoder) for et nytt auto-/generert forslag. Inneholder aldri POL.</summary>
    IReadOnlyList<string> StandardForslagssynlighet();
}
