namespace Aarshjul.Domain;

/// <summary>
/// En ansvarlig for et internt gjøremål. Kan enten peke på en bruker (valgt fra listen over
/// SBR-brukere via <see cref="BrukerId"/>) eller være ren fritekst (<see cref="BrukerId"/> = null).
/// «Kun mine oppgaver»-filteret matcher på <see cref="BrukerId"/>; fritekst vises, men filtreres ikke.
/// Et gjøremål kan ha flere ansvarlige.
/// </summary>
public class GjoeremaalAnsvarlig
{
    public Guid Id { get; set; }

    public Guid GjoeremaalId { get; set; }
    public InterntGjoeremaal? Gjoeremaal { get; set; }

    /// <summary>Bruker-id (Entra oid/sub) når ansvarlig er valgt fra brukerlisten. Null for fritekst.</summary>
    public string? BrukerId { get; set; }

    /// <summary>Visningsnavn (fra brukeren eller fritekst).</summary>
    public required string Navn { get; set; }
}
