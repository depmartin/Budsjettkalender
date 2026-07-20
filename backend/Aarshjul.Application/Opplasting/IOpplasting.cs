namespace Aarshjul.Application.Opplasting;

/// <summary>
/// Hint administrator kan gi ved manuell opplasting. Nummer og budsjettår styrer dedup-nøkkel
/// (<c>r-{nr}-{aar}</c>) og fornuftssjekk av datoer; tittel kan overstyre den uttrukne.
/// </summary>
public sealed record Opplastingshint
{
    public int? Nummer { get; init; }
    public int? Budsjettaar { get; init; }
    public string? Tittel { get; init; }
}

/// <summary>Hva den manuelle opplastingen førte til.</summary>
public enum Opplastingsutfall
{
    /// <summary>Nytt dokument → ett eller flere robotforslag (ett per frist) lagt i køen.</summary>
    ForslagOpprettet,

    /// <summary>Kjent dokument med endret innhold → re-uttrekk, nye forslag til gjennomgang.</summary>
    EndretVersjon,

    /// <summary>Kjent dokument med uendret innhold → hoppet over (dedup).</summary>
    Duplikat,

    /// <summary>Varig regelverk (nummer 100–199) → ignorert, ingen frister.</summary>
    VarigIgnorert,

    /// <summary>PDF-en inneholdt ingen lesbar tekst.</summary>
    KunneIkkeLeseTekst
}

/// <summary>Resultatet av en opplasting, med nok informasjon til å vise administrator hva som skjedde.</summary>
public sealed record Opplastingsresultat
{
    public required Opplastingsutfall Utfall { get; init; }

    /// <summary>Antall forslag som ble lagt i køen (ett per frist funnet i dokumentet).</summary>
    public int AntallForslag { get; init; }

    /// <summary>Id-en til det første forslaget (for lenke til køen), hvis noe ble laget.</summary>
    public Guid? ForslagId { get; init; }

    public string? Loep { get; init; }
    public bool ErUkjentType { get; init; }
    public bool HarUsikkerhetsflagg { get; init; }
    public string Melding { get; init; } = "";
}

/// <summary>
/// Manuell opplasting av en rundskriv-PDF gjennom samme innhentingspipeline som den automatiske
/// jobben senere skal bruke (dedup → totrinnsfilter → datouttrekk → forslag i godkjenningskøen).
/// Lar hele pipelinen og køen testes uten live tilgang til regjeringen.no (Cloudflare-blokkert),
/// og fungerer som en varig reserveløsning når en kilde er utilgjengelig. Ingenting publiseres —
/// resultatet er alltid et <c>Forslag</c> som passerer administrators gjennomgang.
/// </summary>
public interface IOpplasting
{
    Task<Opplastingsresultat> LastOppAsync(byte[] pdf, Opplastingshint hint, CancellationToken ct = default);
}
