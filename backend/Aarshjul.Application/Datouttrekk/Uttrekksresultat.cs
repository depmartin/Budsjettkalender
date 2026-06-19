namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Ett tolket felt fra et automatisk uttrekk: den tolkede verdien, tekstutdraget den er hentet
/// fra, og modellens egenkonfidens (0..1). Kildeutdraget vises alltid ved siden av tolket verdi
/// i køen, og konfidens er kun ett bidrag til usikkerhetsvurderingen — aldri eneste utløser
/// (SYSTEMARKITEKTUR 3.2, 5).
/// </summary>
public sealed record Uttrekksfelt
{
    public required string Felt { get; init; }
    public string? TolketVerdi { get; init; }
    public string? Kildeutdrag { get; init; }
    public double Konfidens { get; init; }
}

/// <summary>Strukturert per-felt-resultat fra <see cref="IDatouttrekk"/>.</summary>
public sealed record Uttrekksresultat
{
    public IReadOnlyList<Uttrekksfelt> Felter { get; init; } = [];

    /// <summary>Henter ett felt på navn (case-insensitivt), eller null.</summary>
    public Uttrekksfelt? Felt(string navn) =>
        Felter.FirstOrDefault(f => string.Equals(f.Felt, navn, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Stabile feltnavn brukt i uttrekksresultatet.</summary>
public static class Uttrekksfelter
{
    public const string Dato = "dato";
    public const string Tittel = "tittel";
    public const string Budsjettaar = "budsjettaar";
    public const string Kategori = "kategori";
}
