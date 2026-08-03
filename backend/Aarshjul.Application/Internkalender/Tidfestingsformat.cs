using Aarshjul.Domain;

namespace Aarshjul.Application.Internkalender;

/// <summary>
/// Formaterer en lesbar tidsangivelse for et internt gjøremål (norsk). Holdt samlet slik at både
/// tjenesten (DTO-bygging) og eventuell gjenbruk gir samme tekst.
/// </summary>
public static class Tidfestingsformat
{
    private static readonly string[] Maaneder =
    [
        "januar", "februar", "mars", "april", "mai", "juni",
        "juli", "august", "september", "oktober", "november", "desember"
    ];

    public static string Maanednavn(int maaned) => maaned is >= 1 and <= 12 ? Maaneder[maaned - 1] : maaned.ToString();

    public static string Kvalifikatornavn(Datokvalifikator k) => k switch
    {
        Datokvalifikator.Primo => "primo",
        Datokvalifikator.Medio => "medio",
        Datokvalifikator.Ultimo => "ultimo",
        _ => ""
    };

    public static string Posisjonsnavn(Rundeposisjon p) => p switch
    {
        Rundeposisjon.Start => "start av runden",
        Rundeposisjon.Tidlig => "tidlig i runden",
        Rundeposisjon.Midt => "midt i runden",
        Rundeposisjon.Sent => "sent i runden",
        Rundeposisjon.Slutt => "slutten av runden",
        _ => p.ToString()
    };

    /// <summary>
    /// Bygger tidsangivelsen ut fra tidfestingstype og de oppløste verdiene. <paramref name="dato"/>
    /// er den oppløste referansedatoen (null når udatert/venter på anker).
    /// </summary>
    public static string Bygg(
        Tidfestingstype type,
        DateOnly? dato,
        Datopresisjon presisjon,
        Datokvalifikator? kvalifikator,
        string? ankerLoep,
        int ankerOffset,
        Rundeposisjon? rundeposisjon,
        bool venterPaaAnker)
    {
        switch (type)
        {
            case Tidfestingstype.KonkretDato:
                return dato is { } d ? d.ToString("dd.MM.yyyy") : "Uten dato";

            case Tidfestingstype.TentativMaaned:
                if (dato is not { } m)
                {
                    return "Uten dato";
                }
                var kval = kvalifikator is { } k ? Kvalifikatornavn(k) + " " : "";
                return $"{kval}{Maanednavn(m.Month)} {m.Year} (tentativ)";

            case Tidfestingstype.AnkerRelativ:
                var relasjon = AnkerRelasjon(ankerOffset, ankerLoep);
                return venterPaaAnker
                    ? $"Venter på ankerdato ({relasjon})"
                    : dato is { } a ? $"{a:dd.MM.yyyy} ({relasjon})" : relasjon;

            case Tidfestingstype.Rundeposisjon:
                var pos = rundeposisjon is { } rp ? Posisjonsnavn(rp) : "i runden";
                return dato is { } r ? $"{char.ToUpper(pos[0])}{pos[1..]} ({r:dd.MM.yyyy})" : $"{char.ToUpper(pos[0])}{pos[1..]}";

            default:
                return "Uten dato";
        }
    }

    /// <summary>Årsuavhengig tidsangivelse for en generell regel (uten konkret år).</summary>
    public static string ByggRegel(
        Tidfestingstype type,
        int? maaned,
        int? dag,
        Datokvalifikator? kvalifikator,
        string? ankerLoep,
        int ankerOffset,
        Rundeposisjon? rundeposisjon)
    {
        switch (type)
        {
            case Tidfestingstype.KonkretDato:
                return maaned is { } m && dag is { } d ? $"{d}. {Maanednavn(m)}" : "Uten dato";

            case Tidfestingstype.TentativMaaned:
                if (maaned is not { } tm)
                {
                    return "Uten dato";
                }
                var kval = kvalifikator is { } k ? Kvalifikatornavn(k) + " " : "";
                return $"{kval}{Maanednavn(tm)} (tentativ)";

            case Tidfestingstype.AnkerRelativ:
                return AnkerRelasjon(ankerOffset, ankerLoep);

            case Tidfestingstype.Rundeposisjon:
                return rundeposisjon is { } rp ? char.ToUpper(Posisjonsnavn(rp)[0]) + Posisjonsnavn(rp)[1..] : "I runden";

            default:
                return "Uten dato";
        }
    }

    private static string AnkerRelasjon(int offset, string? ankerLoep)
    {
        var loep = string.IsNullOrWhiteSpace(ankerLoep) ? "ankeret" : ankerLoep;
        if (offset == 0)
        {
            return $"samme dag som {loep}";
        }
        var dager = Math.Abs(offset);
        var enhet = dager == 1 ? "dag" : "dager";
        return offset < 0 ? $"{dager} {enhet} før {loep}" : $"{dager} {enhet} etter {loep}";
    }
}
