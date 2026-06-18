namespace Aarshjul.Domain;

/// <summary>
/// Koblingstabell bruker ↔ gruppekode (beslutning 2026-06-18). <see cref="Kilde"/> skiller
/// Entra-utledet medlemskap fra manuelt satt, slik at en manuell overstyring ikke
/// overskrives ved neste innlogging.
/// </summary>
public class BrukerGruppe
{
    public required string BrukerId { get; set; }
    public Bruker? Bruker { get; set; }

    /// <summary>Refererer <see cref="Synlighetsgruppe.Kode"/>.</summary>
    public required string GruppeKode { get; set; }
    public Synlighetsgruppe? Gruppe { get; set; }

    public GruppeMedlemskapKilde Kilde { get; set; } = GruppeMedlemskapKilde.Entra;
}
