using Aarshjul.Application.Frister;
using Aarshjul.Domain;

namespace Aarshjul.Application.Godkjenningsko;

/// <summary>Per-felt uttrekksbevis slik det vises i køen (tolket verdi + kildeutdrag + konfidens).</summary>
public sealed record KoBevis(string Felt, string? TolketVerdi, string? Kildeutdrag, double Konfidens);

/// <summary>Gjeldende verdier på den publiserte fristen et endringsforslag peker på (før/etter-visning).</summary>
public sealed record FristGjeldende(
    string Tittel, DateOnly Dato, int Budsjettaar, Kategori Kategori, IReadOnlyList<string> Synlighet);

/// <summary>Ett kort i godkjenningskøen — et forslag med alt administrator trenger for å avgjøre.</summary>
public sealed record Koelement
{
    public Guid Id { get; init; }
    public ForslagType ForslagType { get; init; }
    public Opphav Opphav { get; init; }

    /// <summary>Kilde (rundskriv-id) for robot, innsenders identitet for brukerforslag.</summary>
    public string? KildeEllerInnsender { get; init; }

    public string Tittel { get; init; } = "";
    public DateOnly? Dato { get; init; }
    public Datopresisjon Datopresisjon { get; init; }
    public Datokvalifikator? Datokvalifikator { get; init; }
    public int Budsjettaar { get; init; }
    public Kategori Kategori { get; init; }
    public string? Loep { get; init; }
    public string? Notat { get; init; }

    /// <summary>Foreslått synlighet (gruppekoder). Administrator beslutter endelig ved godkjenning.</summary>
    public IReadOnlyList<string> ForeslaattSynlighet { get; init; } = [];

    public Guid? EndrerFristId { get; init; }
    public Guid? DokumentId { get; init; }
    public IReadOnlyList<KoBevis> Bevis { get; init; } = [];

    /// <summary>Gjeldende verdier på fristen som foreslås endret (kun for endringsforslag).</summary>
    public FristGjeldende? Gjeldende { get; init; }

    /// <summary>Robotforslag uten gjenkjent løp — «ukjent type» til manuell vurdering (kravdok. 4.3).</summary>
    public bool ErUkjentType => Opphav == Opphav.Robot && string.IsNullOrWhiteSpace(Loep);
}

/// <summary>Filtre for køen (kravdok. 5.1): opphav, kilde, ukjent type, kategori, forslagstype.</summary>
public sealed record Kofilter
{
    public Opphav? Opphav { get; init; }
    public string? Kilde { get; init; }
    public bool? UkjentType { get; init; }
    public Kategori? Kategori { get; init; }
    public ForslagType? ForslagType { get; init; }
}

/// <summary>
/// Et forslag forberedt for «juster» i redigeringsskjemaet: forslagets felter som
/// <see cref="FristInndata"/> (synlighet minus POL, slik at POL aldri forhåndshukes), pluss et
/// flagg om det er et endringsforslag (da skjules synlighetsvalget — endringsforslag rører aldri
/// synlighet).
/// </summary>
public sealed record ForslagForJustering(FristInndata Felter, bool ErEndring);

/// <summary>Inndata ved godkjenning: hvilket forslag, endelig synlighet, og eksplisitt POL-bekreftelse.</summary>
public sealed record GodkjennInndata
{
    public required Guid ForslagId { get; init; }
    public required IReadOnlyList<string> Synlighetskoder { get; init; }

    /// <summary>Må være true dersom synligheten inkluderer POL — POL settes aldri uten aktivt valg.</summary>
    public bool PolBekreftet { get; init; }
}

/// <summary>
/// Godkjenningskøen (SYSTEMARKITEKTUR 6, kravdok. 5.1): felles innboks for alle forslag, hvert
/// avgjort for seg. Godkjenning publiserer via samme synlighetsvaliderte mønster som
/// <c>FristskrivingTjeneste</c>; avvisning bevarer forslaget og varsler innsender. Alle handlinger
/// er administratorhandlinger (håndheves med policy i web-laget) — selve håndhevingen av synlighet
/// og POL skjer her på serveren.
/// </summary>
public interface IGodkjenningsko
{
    Task<IReadOnlyList<Koelement>> HentKoAsync(Kofilter? filter = null, CancellationToken ct = default);

    /// <summary>Publiserer forslaget som frist. Returnerer fristens id. Kaster <see cref="Valideringsfeil"/>
    /// ved tom synlighet, uautorisert POL eller manglende dato.</summary>
    Task<Guid> GodkjennAsync(GodkjennInndata inndata, CancellationToken ct = default);

    /// <summary>Avviser forslaget (bevares med status avvist) og varsler innsender ved brukerforslag.</summary>
    Task AvvisAsync(Guid forslagId, string? begrunnelse = null, CancellationToken ct = default);

    /// <summary>Henter et åpent forslag forberedt for «juster» i redigeringsskjemaet (kravdok. 5.2).
    /// Returnerer <c>null</c> om forslaget ikke finnes eller allerede er behandlet.</summary>
    Task<ForslagForJustering?> HentForslagForJusteringAsync(Guid forslagId, CancellationToken ct = default);

    /// <summary>Justerer forslagets felter og publiserer det i én handling (kravdok. 5.1 «juster …
    /// deretter godkjenn»). For endringsforslag oppdateres kun innholdet; <c>synlig_for</c> står
    /// urørt. Returnerer fristens id. Kaster <see cref="Valideringsfeil"/> som <see cref="GodkjennAsync"/>.</summary>
    Task<Guid> JusterOgGodkjennAsync(Guid forslagId, FristInndata felter, CancellationToken ct = default);
}
