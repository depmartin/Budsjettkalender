using Aarshjul.Domain;

namespace Aarshjul.Kilder;

/// <summary>
/// Et kjent budsjettløp og tittelmønstrene som identifiserer det (kravdok. 4.3). Frister
/// identifiseres på <b>funksjon</b>, aldri på rundskrivnummer — nummeret er kun et svakt hint
/// og kan skifte mellom år. Tittelmønstrene matches case-insensitivt som delstreng, og dekker
/// både bokmåls- og nynorskvarianter siden titlene er formelfaste men språkblandede over tid.
/// </summary>
public sealed record Loepmonster(string Loep, Kategori Kategori, string[] Tittelmonstre, int? Nummerhint);

/// <summary>De syv kjente løpene fra kravdok. 4.3.</summary>
public static class Loepmonstre
{
    public static readonly IReadOnlyList<Loepmonster> Alle =
    [
        new("marskonferanse", Kategori.Budsjett,
            ["materialet til regjeringens konferanse i mars", "materialet til regjeringa si konferanse i mars"], 9),
        new("rammefordeling", Kategori.Budsjett,
            ["hovudbudsjettskriv", "hovedbudsjettskriv"], 4),
        new("rnb", Kategori.Budsjett,
            ["tilleggsbevilgninger og omprioriteringer våren", "tilleggsløyvingar og omprioriteringar våren"], 3),
        new("nysaldering", Kategori.Budsjett,
            ["tilleggsløyvingar i haustsesjonen", "ny saldering", "nysaldering"], 7),
        new("gulbok", Kategori.Gulbok,
            ["bekreftelsesbrev og innlevering av tekst til gul bok", "stadfestingsbrev og innlevering av tekst til gul bok"], 6),
        new("statsregnskap", Kategori.Regnskap,
            ["årsavslutning og frister for innrapportering", "årsavslutning og fristar for innrapportering"], 8),
        new("rapportering", Kategori.Regnskap,
            ["rapportering til statsrekneskapen", "rapportering til statsregnskapen"], 10),
    ];
}
