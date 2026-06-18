namespace Aarshjul.Domain;

/// <summary>Kategori som styrer filtrering i visningen (kravdok. 3.2).</summary>
public enum Kategori
{
    Budsjett,
    Gulbok,
    Regnskap
}

/// <summary>Livssyklusstatus for en frist/forslag. `Avvist` er en designutvidelse (SYSTEMARKITEKTUR 3.1).</summary>
public enum FristStatus
{
    Forslag,
    Godkjent,
    Fullfoert,
    Avvist
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

/// <summary>Behandlingsstatus for et oppdaget kildedokument (kravdok. 3.4).</summary>
public enum BehandletStatus
{
    Ny,
    ForslagLaget,
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
