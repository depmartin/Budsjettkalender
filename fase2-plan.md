# Fase 2 — Automatikk og samhandling: detaljert plan

Detaljert byggeplan for fase 2, lagt fram før kode skrives (jf. utviklingsplanen
Del C). Planen er forankret i den faktiske fase 1-koden (PR #1, merget til main):
.NET 10-solution `backend/Aarshjul.slnx` lagdelt i Domain/Application/Infrastructure/
Web/Tests, Blazor Web App (Interactive Server) + minimal-API, EF Core mot Azure SQL.
Se @.claude/rules/arkitektur.md for stack, miljøoppsett, kommandoer og kart over hvor
fase 1-koden bor, og @.claude/rules/beslutningslogg.md for fremdrift og åpne forhold.

Kravgrunnlag: @kravdokument-aarshjul-frister_v2.md kap. 4, 5, 8, 9.5;
@SYSTEMARKITEKTUR.md kap. 3.2, 3.4, 5, 6, 9.

---

## 1. Mål og avgrensning

Fase 2 kobler på automatisk innhenting fra regjeringen.no og åpner for brukerforslag
og endringsforslag, **uten å svekke prinsippet om at ingenting publiseres uten
godkjenning**. Fase 2 leverer ikke generering fra mal eller valgårslogikk (fase 3),
men forbereder gjentaksregler ved at relative frister i kilden mappes til regelutkast.

## 2. Utgangspunkt i koden (det fase 2 gjenbruker)

Fase 1 har allerede lagt fundamentet fase 2 bygger på:

- **Datamodellen er komplett.** `AppDbContext` har DbSets for `Forslag`,
  `UttrekksBevis`, `BehandledeDokumenter`, `Gjentaksregler` og `Varsler` (i tillegg
  til `Frister`, `FristSynlighet`, `Synlighetsgrupper`, `Brukere`, `BrukerGrupper`).
  `Forslag` har `ForslagType` (NyFrist/Endring/Generert), `Opphav`,
  `KildeEllerInnsender`, fristfeltene, `ForeslaattSynlighet`, `EndrerFristId`,
  `DokumentId` og en `UttrekksBevis`-kolleksjon. `BehandletDokument` har `Kilde`,
  `DokumentNokkel`, `InnholdHash`, `BehandletStatus` og unik indeks på
  (`Kilde`,`DokumentNokkel`). `UttrekksBevis` har `Felt`, `TolketVerdi`,
  `Kildeutdrag`, `Konfidens`.
- **Synlighet (sikkerhetskritisk):** `Aarshjul.Application/Synlighet/Synlighetsfilter.cs`
  (`FiltrerSynlige`) er det ene punktet all synlighet går gjennom. Gjenbrukes av køen,
  brukerforslag og Word-utskrift.
- **Publiseringsmønster:** `IFristskriving`/`FristskrivingTjeneste`
  (`OpprettAsync`/`OppdaterAsync`) med synlighetsvalidering (POL kun ved aktivt valg).
  Godkjenning av et forslag publiserer via samme mønster.
- **Auth:** policyene `ErAdministrator` og `KanForeslå` (`Web/Sikkerhet/Autorisasjon.cs`);
  `BrukerClaimsTransformation` stripper forfalskede claims. Brukerforslag krever
  `KanForeslå`; køhandlinger krever `ErAdministrator`.
- **Plassholdere:** `backend/kilder` (kildeabstraksjon) og `backend/jobb`
  (bakgrunnsjobb) er opprettet og venter på fase 2.

## 3. Modelltillegg fase 2 trenger (krever EF-migrasjon)

Det meste finnes, men to skjerpinger fra planleggingen krever små tillegg:

- **Forsøksteller / uttrekk-mellomtilstand på `BehandletDokument`.** Dagens
  `BehandletStatus` er `Ny`/`ForslagLaget`/`FerdigBehandlet` og har ingen teller for
  gjentatte `hent()`-feil. Legg til et forsøksteller-felt (og evt. en status-verdi
  for «henting/uttrekk feilet, venter nytt forsøk»), slik at et dokument kan prøves
  et fast antall ganger over påfølgende kjøringer uten å foreslås dobbelt eller
  forsvinne (SYSTEMARKITEKTUR 3.4). Alternativt en egen liten arbeidskø-tabell —
  avgjøres i steg C.
- **Liveness-spor.** En liten tilstand for «sist vellykkede innhenting», med
  `oppdag()` og `hent()`/uttrekk sporet hver for seg (ny liten entitet eller
  konfig-/tilstandstabell). Brukes av admin-flaten i steg B+L.

Begge tas i én EF-migrasjon tidlig i fasen
(`dotnet ef migrations add Fase2Innhenting --project backend/Aarshjul.Infrastructure
--startup-project backend/Aarshjul.Infrastructure`).

## 4. Byggesteg

Hvert steg fullføres og verifiseres (`dotnet test backend/Aarshjul.slnx` grønt) før
neste. Stegene er ordnet etter avhengighet.

### Steg A — Kildeabstraksjon (`backend/kilder`)
`Kilde`-grensesnitt som gjør resten av systemet kildeagnostisk (kravdok. 4.1):
`oppdag()` → dokumentreferanser (id, tittel, dato, url), `hent(ref)` → råtekst/PDF.
`RegjeringenKilde` er første implementasjon; `DFOeKilde` kan kobles på senere.
**Grensesnittet uttrykker utfall, ikke bare data:** `oppdag()` returnerer en
utfallstilstand — fant nye / ingen nye (kjørte greit) / klarte ikke parse — slik at en
tom liste fra en vellykket kjøring skilles fra en feilet kjøring. Legg til prosjektet
i `Aarshjul.slnx` og la `Aarshjul.Web`/jobben referere det.

### Steg B — `RegjeringenKilde.oppdag()`
Les arkivsiden `…/rundskriv/arkiv/id446220/`, parse tabellradene (Nummer/Tittel/Dato/
Status), utled PDF-URL fra `…/arlige/{aar}/r-{nr}-{aar}.pdf` med fallback til den eldre
`…/upload/fin/vedlegg/okstyring/…`-varianten (kravdok. 4.2). Parse-feil gir tilstanden
«klarte ikke parse», ikke en tom liste.

### Steg C — Deduplisering mot `BehandledeDokumenter`
Sjekk hvert dokument mot registeret før forslag lages (SYSTEMARKITEKTUR 3.4):
- `DokumentNokkel` (stabil, foreslås avledet av `r-{nr}-{aar}`, bekreftes), `InnholdHash`
  (hash av hentet PDF-tekst).
- Kjent nøkkel + uendret hash → hopp over (også når fristene tidligere ble avvist).
- Kjent nøkkel + ny hash → flagg «oppdatert versjon» til administrator, ikke dublett.
- **Bruk/utvid mellomtilstanden + forsøkstelleren (kap. 3)** for dokumenter som er
  oppdaget men ennå ikke ferdig hentet/uttrukket. Plassering (felt på
  `BehandletDokument` vs. egen arbeidskø) **avgjøres her** — begge oppfyller kravet.

### Steg D — Totrinns filtrering
Trinn 1 nummerserie (1–99 årlig → videre; 100–199 varig → ignorer). Trinn 2
tittelgjenkjenning (case-insensitivt) mot løpsmønstrene i kravdok. 4.3
(marskonferanse/rammefordeling/rnb/nysaldering/gulbok/statsregnskap/rapportering);
nummer kun som svakt hint. **Sikkerhetsnett:** årlig rundskriv uten match kastes ikke,
men legges i køen som «ukjent type» (`Forslag` uten gjenkjent `Loep`/`Kategori`).

### Steg E — `hent()` + datouttrekk
Last ned PDF, hent tekst, og bruk **språkmodell** til tolkning (kravdok. 4.4).
Anbefaling: Claude API med nyeste, kapable modell; leverandør/SDK bekreftes i fasen
(API: https://docs.claude.com/en/api/overview). Resultatet skrives som `Forslag`
(`Opphav = Robot`, `DokumentId` satt) med per-felt `UttrekksBevis` (`TolketVerdi`,
`Kildeutdrag`, `Konfidens`). Relative frister («ultimo mars») → utkast til
gjentaksregel/`Datopresisjon = Maaned` (+ `Datokvalifikator`), ikke hard dato.
**Kildeutdraget vises alltid** ved siden av tolket verdi i køen.
**Usikkerhetsflagget styres av deterministiske, verifiserbare regler** i
Application-laget (relativ→hard dato, kildespenn uten gjenkjennelig dato, brutt
fornuftsregel som dato utenfor budsjettløpets vindu). `Konfidens` er ett bidrag,
**aldri eneste utløser**.

### Steg F — Godkjenningskøen (`Aarshjul.Web`, admin)
Felles innboks (SYSTEMARKITEKTUR 6, kravdok. 5.1): enkeltkort gruppert per kilde, hver
godkjennes for seg (ingen masse-godkjenning). Filtre: opphav, kilde, ukjent type,
kategori, forslagstype. Handlinger: godkjenn / juster / avvis (+ vurder for «ukjent
type»). **Godkjenn** publiserer `Forslag` → `Frist` via `FristskrivingTjeneste`-
mønsteret (synlighetsvalidering, POL kun ved aktivt valg). **Avvis** setter
`Status = Avvist` (bevares) og oppretter `Varsel` ved brukerforslag. Admin-innsyn i
køen gjenbruker `Synlighetsfilter`.

### Steg G — Redigeringsskjema (gjenbruk fra fase 1)
`Admin/RedigerFrist` brukes for «juster» og manuell innlegging (kravdok. 5.2).
Forhåndsutfyller synlighet fra brukerforslagets `ForeslaattSynlighet`; `POL` aldri
automatisk; «Gjenta neste år» knytter til `Gjentaksregel` (tas i full bruk i fase 3).

### Steg H — Brukerforslag (bidragsyter)
Enklere skjema bak `KanForeslå` (kravdok. 5.3): tittel, dato, budsjettår, kategori,
notat, foreslått synlighet. Navn fra innlogget identitet (`KildeEllerInnsender`).
`Forslag` med `Opphav = Bruker`, `Status = Forslag`; usynlig for andre til godkjent;
`POL` foreslått av bruker krever aktiv bekreftelse fra administrator. «Mine forslag»-
oversikt viser status (venter/godkjent/avvist); avvist kan redigeres og sendes på nytt.

### Steg I — Endringsforslag
`Forslag` med `ForslagType = Endring` og `EndrerFristId` (indeksert). Original står
uendret til godkjenning; «venter endring»-merke utledes ved oppslag på åpne forslag
med matchende `EndrerFristId`. Før/etter-visning i køen; flere samtidige tillatt,
vurderes uavhengig (SYSTEMARKITEKTUR 3.2).

### Steg J — Varsel (`Varsel`)
Opprettes ved godkjenn/avvis av brukerforslag; `BrukerId`, `Tekst`, valgfri
`Begrunnelse`, `Lest`. In-app innboks/teller i web-hosten; ingen utgående kanal
(SYSTEMARKITEKTUR 3.7).

### Steg K — Utskrift til Word (`Aarshjul.Web` + Application)
Server-side generering (kravdok. 8, SYSTEMARKITEKTUR 9). Valg av gruppe + periode;
utvalget følger gruppens faktiske tilgang via `Synlighetsfilter`/`Synlighetskontekst.
ForGruppe`. Word-bibliotek for .NET velges i fasen (f.eks. Open XML SDK).
**Dokumentet bærer en synlig topptekst generert fra det faktiske utvalgskriteriet**
(gruppe + periode). Admins «alt»-utskrift merkes tydeligst som FIN-internt. Selve
utskriftshandlingen logges ikke.

### Steg L — Bakgrunnsjobb (`backend/jobb`)
Periodisk (daglig) kjøring som driver oppdagelse → dedup → filtrering → uttrekk →
forslag (kravdok. 9.5). Foretrukket form: .NET `BackgroundService`/Worker i samme
løsning (delt forretnings-/autoriseringslag), timer-utløst; alternativ er separat
timer-utløst Azure Functions/container-jobb. Form bekreftes i fasen (arkitektur.md).

### Steg B+L — Stille feil og liveness (tverrgående)
«Klarte ikke parse» fra `oppdag()` varsler administrator. Admin-flaten viser «sist
vellykkede innhenting» (liveness-sporet fra kap. 3), der `oppdag()` og `hent()`/uttrekk
spores hver for seg. Et dokument som gjentatte ganger feiler i `hent()` flagges til
administrator når forsøksgrensen er nådd — det forsvinner aldri stille.

## 5. Det avgjørende kvalitetskravet
- **Ingenting publiseres uten godkjenning** — alt uttrekk og alle bruker-/
  endringsforslag passerer køen som `Forslag`.
- **Synlighet verifiseres på selve API-/spørringssvaret**, ikke bare i UI — også for
  køen, brukerforslag (usynlige for andre) og Word-utvalget. Dekkes av xUnit-tester
  mot SQLite in-memory + `WebApplicationFactory`, slik fase 1 etablerte.

## 6. Verifikasjon før fase 2 regnes som fullført
Akseptkriteriene fra utviklingsplanen, som testbare tilfeller:
1. Et nytt årlig rundskriv gir **ett** `Forslag`; samme rundskriv gir **ikke** nytt
   forslag ved neste kjøring (dedup mot `BehandledeDokumenter`).
2. Varig rundskriv (100–199) ignoreres; årlig uten tittelmatch havner som «ukjent type».
3. Brukerforslag ligger i køen med opphavsmerke og foreslått synlighet, usynlig for
   andre til godkjent.
4. `POL` foreslått av bruker krever aktiv bekreftelse fra administrator.
5. Utskrift for `FAG` utelater FIN-interne frister; utskrift for `POL` gir nøyaktig
   `POL`-settet.
6. Usikkerhetsflagget tennes av de deterministiske reglene uavhengig av `Konfidens`
   (relativ→hard dato; kildespenn uten dato).
7. Liveness: parse-feil varsler admin; «sist vellykkede innhenting» skiller `oppdag()`
   fra `hent()`/uttrekk; dokument som feiler gjentatt i `hent()` flagges ved
   forsøksgrensen.
8. Endringsforslag gir «venter endring»-merke uten å endre fristen; godkjenning
   oppdaterer fristen via skrivetjenesten.

## 7. Til beslutningsloggen ved fasens slutt
Hvordan `DokumentNokkel`/`InnholdHash` er utledet; valgt verktøy/modell for datouttrekk
og oppnådd presisjon; hvordan kildeabstraksjonen er formet for senere kilder; valgt
form for bakgrunnsjobben; og hvor mellomtilstanden/forsøkstelleren havnet.

## 8. Åpne spørsmål (avklares i fasen)
- **FINs notatmal:** finnes en malfil (.dotx) å bygge Word-eksporten på?
- **Datouttrekk/LLM:** rundskrivene er offentlige (ikke personvernsperre); endelig
  leverandør/SDK og prompt-/skjemaoppsett bekreftes.
- **`DokumentNokkel`-format** og eldre PDF-URL-varianter (steg B/C).
- **Plassering av mellomtilstand/forsøksteller** (felt på `BehandletDokument` vs. egen
  arbeidskø) — steg C.
- **Bakgrunnsjobb-form** (`BackgroundService` vs. Azure Functions/container) — steg L.

(Stack, database, frontend og datamodell er ikke lenger åpne — de er bekreftet og
implementert i fase 1, se arkitektur.md.)
