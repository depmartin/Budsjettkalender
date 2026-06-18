namespace Aarshjul.Domain;

/// <summary>
/// Register over dokumenter systemet har sett (kravdok. 3.4), for å unngå at samme rundskriv
/// foreslås flere ganger — uavhengig av om fristene ble godkjent eller avvist. Tas i full
/// bruk i Fase 2.
/// </summary>
public class BehandletDokument
{
    public Guid Id { get; set; }

    /// <summary>Hvilken kilde, f.eks. "regjeringen".</summary>
    public required string Kilde { get; set; }

    /// <summary>Stabil identifikator, uavhengig av rundskrivnummer.</summary>
    public required string DokumentNokkel { get; set; }

    /// <summary>Hash (f.eks. SHA-256) av dokumentinnholdet; fanger republisering med endret innhold.</summary>
    public string? InnholdHash { get; set; }

    public string? Tittel { get; set; }

    public string? Url { get; set; }

    public DateTimeOffset ForstSett { get; set; } = DateTimeOffset.UtcNow;

    public BehandletStatus BehandletStatus { get; set; } = BehandletStatus.Ny;
}
