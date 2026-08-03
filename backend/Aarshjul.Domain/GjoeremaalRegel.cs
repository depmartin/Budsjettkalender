namespace Aarshjul.Domain;

/// <summary>
/// En generell regel (mal) for internkalenderen: beskriver et gjøremål som skal genereres inn i én
/// eller flere rundetypers konkrete planer. Tidfestingen er årsuavhengig (måned/dag, tentativ måned,
/// anker-løp, eller rundeposisjon); ved generering beregnes en konkret dato for det aktuelle rundeåret.
/// Reglene er SBR-interne og forvaltes kun av administrator.
/// </summary>
public class GjoeremaalRegel
{
    public Guid Id { get; set; }

    public required string Tittel { get; set; }

    public string? Notat { get; set; }

    /// <summary>Rundetypene regelen genereres inn i (én eller flere; «hver runde» = alle genererbare).</summary>
    public ICollection<RegelRundetype> Rundetyper { get; set; } = new List<RegelRundetype>();

    // --- Årsuavhengig tidfesting ---

    public Tidfestingstype Tidfestingstype { get; set; } = Tidfestingstype.Ingen;

    /// <summary>Måned (1–12) ved KonkretDato/TentativMaaned.</summary>
    public int? Maaned { get; set; }

    /// <summary>Dag i måneden ved KonkretDato.</summary>
    public int? Dag { get; set; }

    /// <summary>
    /// Ekstra kalenderår-justering på toppen av rundetypens standardforskyvning (default 0).
    /// Brukes f.eks. når en regnskapsoppgave lander i året etter regnskapsåret.
    /// </summary>
    public int AarforskyvningJustering { get; set; }

    public Datokvalifikator? Datokvalifikator { get; set; }

    public string? AnkerLoep { get; set; }

    public int AnkerOffsetDager { get; set; }

    public Rundeposisjon? Rundeposisjon { get; set; }

    /// <summary>Standard ansvarlige som kopieres til hvert generert gjøremål.</summary>
    public ICollection<RegelAnsvarlig> Ansvarlige { get; set; } = new List<RegelAnsvarlig>();
}

/// <summary>Kobling regel ↔ rundetype (hvilke runder en regel genereres inn i).</summary>
public class RegelRundetype
{
    public Guid RegelId { get; set; }
    public GjoeremaalRegel? Regel { get; set; }

    public Rundetype Rundetype { get; set; }
}

/// <summary>En standard ansvarlig på en regel (kopieres til genererte gjøremål). Samme form som <see cref="GjoeremaalAnsvarlig"/>.</summary>
public class RegelAnsvarlig
{
    public Guid Id { get; set; }

    public Guid RegelId { get; set; }
    public GjoeremaalRegel? Regel { get; set; }

    public string? BrukerId { get; set; }

    public required string Navn { get; set; }
}
