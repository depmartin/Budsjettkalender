using Aarshjul.Application.Frister;
using Aarshjul.Application.Utskrift;

namespace Aarshjul.Application.Kalender;

/// <summary>
/// Genererer en iCalendar-fil (.ics, RFC 5545) fra et ferdig filtrert sett frister, slik at
/// administrator kan laste ned mange kalenderhendelser i én fil og importere dem i Outlook
/// (eller en annen kalender). Utvalget er det samme som Word-utskriften: en gruppes faktiske
/// tilgang (eller «alt» = fullt innsyn) innenfor en periode — synlighetsfiltreringen skjer i
/// leseporten før dette kalles, her bygges kun filen.
///
/// Hendelsene er heldagshendelser på fristdagen (fristene har ikke klokkeslett), markert
/// TRANSP:TRANSPARENT så de ikke blokkerer «opptatt»-tid, og uten påminnelse. Gjenbruker
/// <see cref="Utskriftsforesporsel"/> som utvalgskriterium (gruppe + periode).
/// </summary>
public interface IKalenderEksport
{
    /// <summary>Bygger .ics-innholdet i minnet og returnerer det som UTF-8-bytes.</summary>
    byte[] GenererIcs(Utskriftsforesporsel foresporsel, IReadOnlyList<FristDto> frister);
}
