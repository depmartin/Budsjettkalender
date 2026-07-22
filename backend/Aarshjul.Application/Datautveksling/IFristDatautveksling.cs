using Aarshjul.Domain;

namespace Aarshjul.Application.Datautveksling;

/// <summary>
/// Én frist slik den lagres i eksportfila. Bærer alle feltene som trengs for å gjenopprette
/// fristen ved import — inkludert synlighetsgruppene (<see cref="SynligFor"/>), budsjettår,
/// kategori, løp og beskrivende tekst. Sorteringsdagen er avledet og beregnes på nytt ved import,
/// så den lagres ikke.
/// </summary>
public sealed record FristEksport
{
    public Guid Id { get; init; }
    public required string Tittel { get; init; }
    public DateOnly Dato { get; init; }
    public Datopresisjon Datopresisjon { get; init; }
    public Datokvalifikator? Datokvalifikator { get; init; }
    public int Budsjettaar { get; init; }
    public Kategori Kategori { get; init; }
    public string? Loep { get; init; }
    public string? Kilde { get; init; }
    public Guid? DokumentId { get; init; }
    public FristStatus Status { get; init; }
    public Opphav Opphav { get; init; }
    public string? ForeslaattAv { get; init; }
    public string? Notat { get; init; }
    public Guid? GjentaRegelId { get; init; }

    /// <summary>Synlighetsgruppenes koder (f.eks. <c>["FA","FAG"]</c>).</summary>
    public IReadOnlyList<string> SynligFor { get; init; } = [];
}

/// <summary>
/// Hele «databasen» over frister slik den lastes ned og lastes opp igjen. Versjonsfeltet gjør at
/// formatet kan utvikles uten å knekke gamle filer.
/// </summary>
public sealed record FristDatabase
{
    public int Versjon { get; init; } = 1;
    public DateTimeOffset EksportertTid { get; init; }
    public IReadOnlyList<FristEksport> Frister { get; init; } = [];
}

/// <summary>Utfallet av en import.</summary>
public sealed record ImportResultat(int AntallImportert, int AntallErstattet, IReadOnlyList<string> Advarsler);

/// <summary>
/// Eksport og import av alle frister som en gjenopprettbar JSON-«database» (endring #2). Kun
/// administrator (håndheves i web-laget). Import bruker <b>erstatt-alt</b>-semantikk: eksisterende
/// frister fjernes og erstattes fullstendig av innholdet i fila.
/// </summary>
public interface IFristDatautveksling
{
    /// <summary>Alle frister med felter og synlighet, klar til serialisering.</summary>
    Task<FristDatabase> EksporterAsync(CancellationToken ct = default);

    /// <summary>Erstatter alle frister med innholdet i <paramref name="database"/>. Synlighetskoder
    /// som ikke finnes som grupper, hoppes over (med advarsel) for å unngå brutt referanse.</summary>
    Task<ImportResultat> ImporterAsync(FristDatabase database, CancellationToken ct = default);
}
