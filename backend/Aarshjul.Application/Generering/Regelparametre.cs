using System.Text.Json;
using System.Text.Json.Serialization;
using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>
/// Parametre for <see cref="Regeltype.FastDato"/>: samme måned/dag hvert år, justert til nærmeste
/// virkedag ved helg. <c>aar_forskyvning</c> uttrykker at fristen for budsjettår T faller i
/// kalenderår T + forskyvning (årshjulet spenner ~18 mnd; standard 0 = kalenderår = budsjettår).
/// </summary>
public sealed record FastDatoParametre(
    [property: JsonPropertyName("maaned")] int Maaned,
    [property: JsonPropertyName("dag")] int Dag,
    [property: JsonPropertyName("aar_forskyvning")] int AarForskyvning = 0);

/// <summary>
/// Parametre for <see cref="Regeltype.RelativUkedag"/>: n-te forekomst av en ukedag i en måned
/// (f.eks. «andre uke i mars, mandag»). <c>aar_forskyvning</c> som for <see cref="FastDatoParametre"/>.
/// </summary>
public sealed record RelativUkedagParametre(
    [property: JsonPropertyName("maaned")] int Maaned,
    [property: JsonPropertyName("uke")] int Uke,
    [property: JsonPropertyName("ukedag")] string Ukedag,
    [property: JsonPropertyName("aar_forskyvning")] int AarForskyvning = 0);

/// <summary>
/// Parametre for <see cref="Regeltype.RelativTilMilepael"/>: forskyvning i dager fra et anker-løps
/// beregnede dato. Flytter seg automatisk når ankeret flyttes.
/// </summary>
public sealed record RelativTilMilepaelParametre(
    [property: JsonPropertyName("anker_loep")] string AnkerLoep,
    [property: JsonPropertyName("offset_dager")] int OffsetDager);

/// <summary>
/// (De)serialiserer <see cref="Gjentaksregel.Regelparametre"/> (JSON) til typede parameter-DTO-er
/// uten skjemaendring. Ukedag-koder («man», «tir», …) mappes til <see cref="DayOfWeek"/>.
/// </summary>
public static class Regelparser
{
    private static readonly JsonSerializerOptions Opsjoner = new(JsonSerializerDefaults.Web);

    public static FastDatoParametre FastDato(string json)
        => Deserialiser<FastDatoParametre>(json);

    public static RelativUkedagParametre RelativUkedag(string json)
        => Deserialiser<RelativUkedagParametre>(json);

    public static RelativTilMilepaelParametre RelativTilMilepael(string json)
        => Deserialiser<RelativTilMilepaelParametre>(json);

    public static string Serialiser<T>(T parametre)
        => JsonSerializer.Serialize(parametre, Opsjoner);

    /// <summary>Mapper en norsk ukedagkode til <see cref="DayOfWeek"/>. Kaster <see cref="FormatException"/> ved ukjent kode.</summary>
    public static DayOfWeek TolkUkedag(string kode) => kode.Trim().ToLowerInvariant() switch
    {
        "man" or "mandag" => DayOfWeek.Monday,
        "tir" or "tirsdag" => DayOfWeek.Tuesday,
        "ons" or "onsdag" => DayOfWeek.Wednesday,
        "tor" or "torsdag" => DayOfWeek.Thursday,
        "fre" or "fredag" => DayOfWeek.Friday,
        "lor" or "lør" or "lørdag" => DayOfWeek.Saturday,
        "son" or "søn" or "søndag" => DayOfWeek.Sunday,
        _ => throw new FormatException($"Ukjent ukedagkode: «{kode}».")
    };

    private static T Deserialiser<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Opsjoner)
           ?? throw new FormatException($"Kunne ikke tolke regelparametre som {typeof(T).Name}: «{json}».");
}
