using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>En gjentaksregel slik den vises i mal-oversikten.</summary>
public sealed record MalRegelDto(
    Guid Id,
    string Loep,
    string Tittel,
    Kategori Kategori,
    Regeltype Regeltype,
    string Regelparametre,
    bool Valgaarssensitiv);

/// <summary>Inndata fra mal-skjemaet (opprett/rediger en gjentaksregel).</summary>
public sealed class MalRegelInndata
{
    public string Loep { get; set; } = "";
    public string Tittel { get; set; } = "";
    public Kategori Kategori { get; set; } = Kategori.Budsjett;
    public Regeltype Regeltype { get; set; } = Regeltype.FastDato;

    /// <summary>Regelparametre som JSON, avhengig av <see cref="Regeltype"/> (kravdok. 3.3).</summary>
    public string Regelparametre { get; set; } = "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":0}";

    public bool Valgaarssensitiv { get; set; }
}

/// <summary>
/// Forvalter årsmalen (kravdok. 3.3, 5.4): gjentaksreglene «generér neste år» bygger på. Alle
/// handlinger er administratorhandlinger (policy håndheves i web-laget). Validerer at parametrene
/// kan tolkes for den valgte regeltypen før lagring.
/// </summary>
public interface IMaltjeneste
{
    Task<IReadOnlyList<MalRegelDto>> HentAlleAsync(CancellationToken ct = default);
    Task<MalRegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default);
    Task<Guid> OpprettAsync(MalRegelInndata inndata, CancellationToken ct = default);
    Task OppdaterAsync(Guid id, MalRegelInndata inndata, CancellationToken ct = default);
    Task SlettAsync(Guid id, CancellationToken ct = default);
}
