using Aarshjul.Domain;

namespace Aarshjul.Application.Datautveksling;

/// <summary>
/// Én gjentaksregel (årsmal) slik den lagres i eksportfila. Bærer alle feltene som trengs for å
/// gjenopprette regelen — løp, tittel, kategori, regeltype, parametre (JSON) og valgårssensitivitet.
/// </summary>
public sealed record MalRegelEksport
{
    public Guid Id { get; init; }
    public required string Loep { get; init; }
    public string Tittel { get; init; } = "";
    public Kategori Kategori { get; init; }
    public Regeltype Regeltype { get; init; }
    public string Regelparametre { get; init; } = "{}";
    public bool Valgaarssensitiv { get; init; }
}

/// <summary>
/// Hele «databasen» over årsmalen (gjentaksreglene) slik den lastes ned og opp igjen. Versjonsfeltet
/// gjør at formatet kan utvikles uten å knekke gamle filer.
/// </summary>
public sealed record MalDatabase
{
    public int Versjon { get; init; } = 1;
    public DateTimeOffset EksportertTid { get; init; }
    public IReadOnlyList<MalRegelEksport> Regler { get; init; } = [];
}

/// <summary>
/// Eksport og import av alle gjentaksregler (årsmalen) som en gjenopprettbar JSON-«database» —
/// tilsvarende <see cref="IFristDatautveksling"/> for frister. Kun administrator (håndheves i
/// web-laget). Import bruker <b>erstatt-alt</b>: eksisterende regler fjernes og erstattes av fila.
/// </summary>
public interface IMalDatautveksling
{
    Task<MalDatabase> EksporterAsync(CancellationToken ct = default);
    Task<ImportResultat> ImporterAsync(MalDatabase database, CancellationToken ct = default);
}
