namespace Aarshjul.Domain;

/// <summary>Kategori som styrer filtrering i visningen (kravdok. 3.2).</summary>
public enum Kategori
{
    Budsjett,
    Gulbok,
    Regnskap
}

/// <summary>Livssyklusstatus for en frist/forslag. `Avvist` er en designutvidelse (SYSTEMARKITEKTUR 3.1);
/// `Forkastet` er auto-forkastede robotforslag i den reverserbare forkastet-listen (designintervju 2026-06-19).</summary>
public enum FristStatus
{
    Forslag,
    Godkjent,
    Fullfoert,
    Avvist,

    /// <summary>Robotforslag auto-forkastet (lav konfidens OG ingen gjenkjennelig dato). Ligger i
    /// forkastet-listen, reverserbart: admin kan hente det tilbake til køen eller slette det. Aldri stille.</summary>
    Forkastet
}

/// <summary>Hvor en frist eller et forslag stammer fra.</summary>
public enum Opphav
{
    Robot,
    Bruker,
    Admin
}

/// <summary>Hva en bruker kan gjøre (uavhengig av synlighetsgrupper).</summary>
public enum Funksjonsrolle
{
    Administrator,
    Bidragsyter,
    Leser
}

/// <summary>Presisjonsnivå for en frists dato (SYSTEMARKITEKTUR 3.1, 7).</summary>
public enum Datopresisjon
{
    Dag,
    Maaned
}

/// <summary>Kvalifikator for månedspresisjon. Kun meningsfull når presisjon er <see cref="Datopresisjon.Maaned"/>.</summary>
public enum Datokvalifikator
{
    Primo,
    Medio,
    Ultimo
}

/// <summary>Regeltype i årsmalen (kravdok. 3.3).</summary>
public enum Regeltype
{
    FastDato,
    RelativUkedag,
    RelativTilMilepael
}

/// <summary>Behandlingsstatus for et oppdaget kildedokument (kravdok. 3.4). Mellomtilstandene
/// `HentingFeilet`/`FeiletFlagget` støtter retry-med-forsøksteller og liveness (designintervju 2026-06-19).</summary>
public enum BehandletStatus
{
    /// <summary>Oppdaget, men ennå ikke hentet/uttrukket.</summary>
    Ny,

    /// <summary>Hent()/uttrekk eller forrige forsøk feilet; venter nytt forsøk (forsøksteller ikke nådd grensen).</summary>
    HentingFeilet,

    /// <summary>Forsøksgrensen er nådd; dokumentet er flagget til administrator og forsvinner ikke stille.</summary>
    FeiletFlagget,

    /// <summary>Forslag laget fra dokumentet (ligger i køen eller forkastet-listen).</summary>
    ForslagLaget,

    /// <summary>Ferdig behandlet — foreslås aldri på nytt (godkjent/avvist/slettet fra forkastet-kø).</summary>
    FerdigBehandlet
}

/// <summary>Hvilken slags forslag dette er (SYSTEMARKITEKTUR 3.2).</summary>
public enum ForslagType
{
    NyFrist,
    Endring,
    Generert
}

/// <summary>Om et gruppemedlemskap er utledet fra Entra eller satt manuelt (beslutning 2026-06-18).</summary>
public enum GruppeMedlemskapKilde
{
    Entra,
    Manuell
}

// ---------------------------------------------------------------------------
// Internkalender for SBR (SBR-intern arbeidsliste, atskilt fra publiserte frister).
// ---------------------------------------------------------------------------

/// <summary>
/// Budsjettrunde en intern oppgaveplan hører til. De fire faste rundene instansieres per
/// budsjett-/regnskapsår; <see cref="Ovrig"/> er en stående bøtte uten år for oppgaver som
/// ikke hører til en runde.
/// </summary>
public enum Rundetype
{
    /// <summary>Marsrunden: januar–april i år t-1 (for budsjettår t).</summary>
    Marsrunden,

    /// <summary>Augustrunden (inkl. gul bok/Prop. 1 S): juli–oktober i år t-1 (for budsjettår t).</summary>
    Augustrunden,

    /// <summary>Revidert nasjonalbudsjett: april–mai i år t.</summary>
    Rnb,

    /// <summary>Nysalderingen: oktober–desember i år t.</summary>
    Nysaldering,

    /// <summary>Regnskap: én runde per regnskapsår, arbeidet med gjennom hele året.</summary>
    Regnskap,

    /// <summary>Øvrig: stående bøtte uten år, kun manuelle oppgaver.</summary>
    Ovrig
}

/// <summary>Status for et internt gjøremål. Fullførte flyttes til «ferdig»-visningen.</summary>
public enum GjoeremaalStatus
{
    Aktiv,
    Fullfoert
}

/// <summary>
/// Hvordan et internt gjøremål (eller en regel) er tidfestet. Verdien styrer hvilke
/// tidfestingsfelter som er meningsfulle.
/// </summary>
public enum Tidfestingstype
{
    /// <summary>Ingen dato satt ennå (mangelfullt gjøremål — sorteres sist).</summary>
    Ingen,

    /// <summary>Konkret dag. Regel: fast dag i måned (med årsforskyvning).</summary>
    KonkretDato,

    /// <summary>Tentativ måned (primo/medio/ultimo).</summary>
    TentativMaaned,

    /// <summary>Relativt til et anker-løp (offset i dager fra en publisert frist).</summary>
    AnkerRelativ,

    /// <summary>Posisjon innenfor rundens månedsspenn (tidlig/midt/sent).</summary>
    Rundeposisjon
}

/// <summary>Grov posisjon innenfor en rundes månedsspenn (avledes til en sorteringsdag).</summary>
public enum Rundeposisjon
{
    Start,
    Tidlig,
    Midt,
    Sent,
    Slutt
}

/// <summary>Om et internt gjøremål er lagt inn manuelt eller generert fra en regel.</summary>
public enum GjoeremaalOpphav
{
    Manuell,
    Generert
}

/// <summary>Hva et synkroniseringsforslag foreslår for en konkret runde (trinn 3).</summary>
public enum SynkHandling
{
    /// <summary>Regelen er ny siden sist — legg til et gjøremål.</summary>
    LeggTil,

    /// <summary>Regelen er endret — oppdater det tilhørende gjøremålet.</summary>
    Oppdater,

    /// <summary>Regelen er slettet — fjern det tilhørende gjøremålet.</summary>
    Fjern
}
