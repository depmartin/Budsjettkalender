namespace Aarshjul.Domain;

/// <summary>
/// Administrerbar synlighetsgruppe (kravdok. 3.6). Administrator kan opprette, gi nytt navn
/// og deaktivere grupper i appen. <see cref="Kode"/> er den stabile nøkkelen det refereres
/// til fra <see cref="FristSynlighet"/>, slik at en ny gruppe virker uten skjemaendring.
/// </summary>
public class Synlighetsgruppe
{
    public Guid Id { get; set; }

    /// <summary>Stabil nøkkel, f.eks. "FA", "FAG", "SKOK".</summary>
    public required string Kode { get; set; }

    /// <summary>Visningsnavn, f.eks. "Finansavdelingen".</summary>
    public required string Navn { get; set; }

    /// <summary>Om gruppen tilbys i nye valg. Deaktivering skjuler den uten å fjerne historiske referanser.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Om gruppen er en innebygd standard (FA, FIN-FAG, FAG, POL).</summary>
    public bool ErStandard { get; set; }
}
