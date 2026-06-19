using System.Text.RegularExpressions;

namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Deterministisk gjenkjenning av datoer og relative tidsformuleringer i tekst. Brukes av
/// usikkerhetsreglene (SYSTEMARKITEKTUR 5) slik at flagging er etterprøvbar og uavhengig av
/// språkmodellens egenvurdering.
/// </summary>
public static class Datogjenkjenning
{
    // «23. januar 2026», «23 januar», «23.01.2026», «23/1-2026», «2026-01-23».
    private static readonly Regex Datoer = new(
        @"\b(\d{1,2}\.?\s+(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)(\s+\d{4})?" +
        @"|\d{1,2}[./-]\d{1,2}[./-]\d{2,4}" +
        @"|\d{4}-\d{1,2}-\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Aarstall = new(@"\b(19|20)\d{2}\b", RegexOptions.CultureInvariant);

    // Markører som signaliserer en relativ/upresis tidsangivelse (kravdok. 4.4: «ultimo mars»,
    // «innen seks dager etter Stortingets åpning»). Bevisst snevert for å unngå falske positiver.
    private static readonly string[] RelativeMarkorer =
    [
        "ultimo", "primo", "medio", "innen", "i løpet av", "før utgangen",
        "omkring", "omtrent", "cirka", "ca.", "rundt månedsskiftet"
    ];

    public static bool InneholderDato(string? tekst) =>
        !string.IsNullOrWhiteSpace(tekst) && Datoer.IsMatch(tekst);

    public static bool ErRelativFormulering(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
            return false;
        var t = tekst.ToLowerInvariant();
        return RelativeMarkorer.Any(m => t.Contains(m));
    }

    /// <summary>Plukker ut første firesifrede årstall (19xx/20xx) i teksten, ellers null.</summary>
    public static int? ProvAarstall(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
            return null;
        var m = Aarstall.Match(tekst);
        return m.Success && int.TryParse(m.Value, out var aar) ? aar : null;
    }
}
