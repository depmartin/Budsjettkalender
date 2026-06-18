namespace Aarshjul.Domain;

/// <summary>
/// «I dag» beregnet i norsk tid. Historikkgrensen (dato + 1) og landingsflatens vinduer skal
/// følge departementets kalenderdag, ikke UTC — ellers blir dagen feil rett etter midnatt.
/// </summary>
public static class Datohjelp
{
    private static readonly TimeZoneInfo Oslo = HentOslo();

    public static DateOnly Idag(TimeProvider klokke)
    {
        var lokal = TimeZoneInfo.ConvertTime(klokke.GetUtcNow(), Oslo);
        return DateOnly.FromDateTime(lokal.DateTime);
    }

    private static TimeZoneInfo HentOslo()
    {
        // IANA-id virker på Linux/Azure; falsk tilbake til UTC dersom tidssonedata mangler.
        foreach (var id in new[] { "Europe/Oslo", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // prøv neste
            }
        }
        return TimeZoneInfo.Utc;
    }
}
