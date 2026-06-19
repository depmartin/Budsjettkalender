namespace Aarshjul.Domain;

/// <summary>Resultat av <see cref="Valgaar.Type"/> (kravdok. 6).</summary>
public enum Valgtype
{
    /// <summary>Verken stortings- eller kommunevalg dette året.</summary>
    Ingen,

    /// <summary>Stortingsvalg (hvert fjerde år: 2025, 2029, 2033 …). Påvirker høstløpet sterkt.</summary>
    Stortingsvalg,

    /// <summary>Kommune-/fylkestingsvalg (midt imellom: 2027, 2031, 2035 …). Svakere påvirkning.</summary>
    Kommunevalg
}

/// <summary>
/// Norsk valgsyklus er fast og kan beregnes uten oppslag (kravdok. 6). Stortingsvalg holdes
/// hvert fjerde år fra 2025 (september); kommune-/fylkestingsvalg midt imellom fra 2027.
/// Funksjonen er ren og fullt testbar.
/// </summary>
public static class Valgaar
{
    public static Valgtype Type(int aar)
    {
        // 2025, 2029, 2033 … gir rest 1 ved divisjon med 4; 2027, 2031 … gir rest 3.
        var rest = ((aar % 4) + 4) % 4;
        return rest switch
        {
            1 => Valgtype.Stortingsvalg,
            3 => Valgtype.Kommunevalg,
            _ => Valgtype.Ingen
        };
    }
}
