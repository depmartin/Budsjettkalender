namespace Aarshjul.Application.Frister;

/// <summary>
/// Skriveport for frister (administratorhandlinger). Validerer at synlighet er valgt aktivt
/// før noe lagres. Manuelt opprettede frister publiseres direkte (status godkjent).
/// </summary>
public interface IFristskriving
{
    /// <summary>Oppretter en publisert frist med valgt synlighet. Kaster <see cref="Valideringsfeil"/> ved tom synlighet.</summary>
    Task<Guid> OpprettAsync(FristInndata inndata, CancellationToken ct = default);

    /// <summary>Oppdaterer en eksisterende frist. Kaster <see cref="Valideringsfeil"/> ved tom synlighet.</summary>
    Task OppdaterAsync(Guid id, FristInndata inndata, CancellationToken ct = default);

    /// <summary>Henter en frist som inndata for redigering, eller null om den ikke finnes.</summary>
    Task<FristInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default);
}
