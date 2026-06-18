namespace Aarshjul.Application.Grupper;

/// <summary>Leser synlighetsgrupper for synlighetsvalg og administrasjon (kravdok. 2.3, 3.6).</summary>
public interface IGruppetjeneste
{
    /// <summary>Aktive grupper som kan velges i synlighetsvalg, sortert med standardgrupper først.</summary>
    Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default);
}

public record GruppeDto(string Kode, string Navn, bool ErStandard);
