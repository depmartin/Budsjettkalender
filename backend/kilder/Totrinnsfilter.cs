using Aarshjul.Domain;

namespace Aarshjul.Kilder;

/// <summary>Hva totrinns-filteret konkluderte for ett dokument.</summary>
public enum Klassifiseringsutfall
{
    /// <summary>Varig regelverk (nummer 100–199) — gir ikke frister, ignoreres.</summary>
    Ignorer,

    /// <summary>Matchet et kjent løpsmønster (kravdok. 4.3).</summary>
    Gjenkjent,

    /// <summary>Årlig rundskriv uten titteltreff — kastes ikke, men legges i køen som «ukjent type».</summary>
    UkjentType
}

/// <summary>Resultatet av <see cref="Totrinnsfilter.Klassifiser"/>.</summary>
public sealed record Klassifiseringsresultat
{
    public required Klassifiseringsutfall Utfall { get; init; }
    public string? Loep { get; init; }
    public Kategori? Kategori { get; init; }
    public string? Begrunnelse { get; init; }

    public static Klassifiseringsresultat Ignorer(string begrunnelse) =>
        new() { Utfall = Klassifiseringsutfall.Ignorer, Begrunnelse = begrunnelse };

    public static Klassifiseringsresultat Gjenkjent(Loepmonster monster) =>
        new() { Utfall = Klassifiseringsutfall.Gjenkjent, Loep = monster.Loep, Kategori = monster.Kategori };

    public static Klassifiseringsresultat Ukjent(string begrunnelse) =>
        new() { Utfall = Klassifiseringsutfall.UkjentType, Begrunnelse = begrunnelse };
}

/// <summary>
/// Totrinns filtrering av et oppdaget rundskriv (kravdok. 4.3): trinn 1 skiller årlig fra varig
/// på nummerserien, trinn 2 gjenkjenner løp på tittelmønster. Konservativ linje («aldri miste en
/// frist», designintervju 2026-06-19): kun den klart varige nummerbåndet (100–199) ignoreres;
/// alt annet uten titteltreff havner som «ukjent type» framfor å slippes stille. Nummeret er kun
/// et svakt hint, aldri nøkkel.
/// </summary>
public static class Totrinnsfilter
{
    public static Klassifiseringsresultat Klassifiser(int? nummer, string? tittel)
    {
        // Trinn 1 — nummerserie: 100–199 = varig regelverk → ignorer.
        if (nummer is >= 100 and <= 199)
            return Klassifiseringsresultat.Ignorer($"Nummer {nummer} er varig regelverk (100–199).");

        // Trinn 2 — tittelgjenkjenning (case-insensitivt delstreng, bokmål/nynorsk).
        var normalisert = (tittel ?? string.Empty).ToLowerInvariant();
        foreach (var monster in Loepmonstre.Alle)
        {
            if (monster.Tittelmonstre.Any(m => normalisert.Contains(m.ToLowerInvariant())))
                return Klassifiseringsresultat.Gjenkjent(monster);
        }

        // Sikkerhetsnett: årlig/ukjent rundskriv uten match kastes ikke.
        return Klassifiseringsresultat.Ukjent("Rundskriv uten gjenkjent tittelmønster — til manuell vurdering.");
    }
}
