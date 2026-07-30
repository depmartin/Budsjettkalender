namespace Aarshjul.Domain;

/// <summary>
/// En delbar, auto-oppdaterende kalender-abonnementslenke (Endring 1, 2026-07-30). En administrator
/// oppretter én lenke per synlighetsgruppe (POL, FA, FAG, …) eller for «alt», og deler URL-en (f.eks.
/// via en SharePoint-kalender). Outlook/kalenderklienter abonnerer på lenken og henter en fersk .ics
/// jevnlig, slik at endringer i løsningen dukker opp av seg selv — i motsetning til en engangs-import.
///
/// <see cref="Token"/> er en uknekkbar hemmelighet i URL-en og fungerer som selve autoriseringen:
/// feed-endepunktet krever ingen innlogging (kalenderklienter kan ikke logge inn interaktivt), men
/// serveren filtrerer alltid utvalget til <see cref="GruppeKode"/> med det samme server-side
/// synlighetsfilteret. Deler man POL-lenken, ser mottakerne kun POL-settet. <see cref="Aktiv"/> lar
/// administrator skru lenken av (tilbakekalle) uten å slette den.
/// </summary>
public class Kalenderabonnement
{
    public Guid Id { get; set; }

    /// <summary>Uknekkbar hemmelig nøkkel i feed-URL-en. Unik.</summary>
    public required string Token { get; set; }

    /// <summary>Synlighetsgruppen feeden gjelder, eller null for «alt» (administrators fulle innsyn).</summary>
    public string? GruppeKode { get; set; }

    /// <summary>Visningsnavn for feeden (gruppens navn eller «Alle»), til admin-oversikten.</summary>
    public required string Etikett { get; set; }

    /// <summary>Identitet/navn på administrator som opprettet lenken.</summary>
    public string? OpprettetAv { get; set; }

    public DateTime OpprettetTid { get; set; }

    /// <summary>Av/på. Er den falsk, svarer feed-endepunktet 404 (tilbakekalt) uten å slette lenken.</summary>
    public bool Aktiv { get; set; } = true;
}
