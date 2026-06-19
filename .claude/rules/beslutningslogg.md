# Beslutningslogg

Kronologisk. Nyeste øverst. Hver oppføring: dato, beslutning, begrunnelse, konsekvens.
Dette er prosjektets hukommelse mellom økter. Les hele ved start av hver økt.

## Status nå
- Aktiv fase: **Fase 1 + Fase 2-offline + Fase 3 er på `main`** (Fase 3 merget via PR #6). Gjenstående Fase 2 (Steg B/C/E/L) er fortsatt blokkert til egress mot `www.regjeringen.no` åpnes.
- Sist fullført: **Lokal demo-modus KODET 2026-06-19** (miljø `Demo`: SQLite + dev-innlogging + demo-data) på gren `claude/demo-modus`, røyktestet kjørende (admin ser 9 frister, FAG-leser 2 — server-side synlighet holder i demo). 128 tester grønt. Se beslutningsoppføringen «Lokal demo-modus kodet» nedenfor. (Forut: Fase 3 Steg A–G, merget via PR #6.)
- Neste mulige steg (brukerens valg): (a) Fase 2-restene B/C/E/L når egress åpnes; (b) Azure-utrulling/driftsherding; (c) småting (det diskrete «venter endring»-merket, FINs ekte `.dotx`-notatmal).
- **Miljøoppsett (ferskt miljø):** .NET 10 SDK + `dotnet-ef` 10.0.9 må installeres (se arkitektur.md «Miljøoppsett»); bekreft `dotnet test backend/Aarshjul.slnx` grønt (128/128) før ny kode. Kjør demo lokalt: `ASPNETCORE_ENVIRONMENT=Demo dotnet run --project backend/Aarshjul.Web`, så `/demo` for å velge persona.
- Tidligere kodet og på `main` (PR #1–#5) — **Fase 1** (fundament) + **Fase 2 offline-deler**. Ferdige steg:
  - **Steg A** kildeabstraksjon (`backend/kilder`/`Aarshjul.Kilder`: `IKilde`, `OppdagResultat`/`Oppdagutfall`, `HentResultat`/`Hentutfall`, `Dokumentreferanse`).
  - **Modelltillegg + EF-migrasjon `Fase2Innhenting`** (`BehandletDokument.UttrekksForsoek`/`SisteForsoek`; `BehandletStatus` + `HentingFeilet`/`FeiletFlagget`; `FristStatus.Forkastet`; ny entitet `InnhentingsStatus` for liveness).
  - **Steg D** totrinns filtrering (`Loepmonstre` + `Totrinnsfilter.Klassifiser`).
  - **Punkt 1 (Steg E-forberedelse, ren logikk):** `IDatouttrekk` + `Uttrekksresultat` + `Usikkerhetsregler.Vurder` (deterministiske flagg + auto-forkast) i `Aarshjul.Application/Datouttrekk`.
  - **Steg F** godkjenningskø: `IGodkjenningsko`/`GodkjenningskoTjeneste` (godkjenn publiserer Forslag→Frist med synlighetsvalidering, POL kun ved aktiv bekreftelse håndhevet på server; endringsforslag oppdaterer berørt frist; avvis bevarer + varsler) + Blazor-flate `/admin/ko`.
  - **Steg H** brukerforslag: `IForslagsinnsending`/`ForslagsinnsendingTjeneste` (+ `Gjeldendebrukerkilde`) + flater `/forslag/ny`, `/forslag/{id}/rediger`, `/forslag/mine`.
  - **Steg I (komplett)** endringsforslag: `IFristlesing.HentEnAsync` (synlighets-sikker by-id), endre-modus i forslagsskjemaet (`/forslag/endre/{fristId}`), «Foreslå endring»-lenke på `Fristkort`. **Punkt C (korrigering av Steg F): endringsforslag rører aldri synlighet** — `GodkjennAsync` oppdaterer kun innhold for `Endring`, `synlig_for` står urørt, ingen synlighetsvalidering; køflaten viser ingen synlighetsvelger for endringsforslag.
  - **Steg J** varsel-innboks: `IVarseltjeneste`/`Varseltjeneste` + `/varsler` + `VarselTeller` i nav.
  - **Steg G** «juster» fra køen (felles RedigerFrist-skjema, valg A): `IGodkjenningsko.HentForslagForJusteringAsync` + `JusterOgGodkjennAsync` (felles `PubliserAsync` delt med `GodkjennAsync`), rute `/admin/frist/juster/{forslagId}`, «Juster»/«Vurder»-knapp i køflaten.
  - **Steg K** Word-utskrift: `FristFilter.FraDato/TilDato`, `IWordEksport`/`WordEksportTjeneste` (Open XML SDK), endepunkt `/api/eksport/word` (admin), flate `/admin/eksport`. Utvalg via `Synlighetskontekst.ForGruppe`/`SerAlt`.
  - `dotnet-ef` 10.0.9 + .NET 10 SDK installeres i ferskt miljø. Forut: Fase 1, PR #3/#4 merget, designintervju (se beslutningene over).
- **BLOKKERER Steg B/E:** `www.regjeringen.no` er ikke i miljøets network egress-allowlist (svar: «Host not in allowlist»). Egress-endring slår trolig først inn i ny sesjon/miljø. Steg B (`OppdagAsync` mot live side) og Steg E (datouttrekk) kan ikke bygges/valideres mot live kilde før verten er åpnet. Bygg parseren mot ekte markup når egress er på plass (brukerens valg), evt. mot fixture i mellomtiden.
- Gjenstående Fase 2 (egress-blokkert): detaljkrav for Steg B (`RegjeringenKilde.OppdagAsync()` mot arkivsiden, PDF-URL `…/arlige/{aar}/r-{nr}-{aar}.pdf` + eldre fallback, ekte User-Agent, parse-feil → `KlarteIkkeParse`), Steg C (dedup + **auto-versjonsmatching på funksjons-/tittelnøkkel innen `Loep`+`Budsjettaar`** + forkastet-liste + auto endringsforslag + **«foreslått fjernet»-utfall**), Steg E (`HentAsync` + live datouttrekk bak `IDatouttrekk`) og Steg L (bakgrunnsjobb `BackgroundService` + manuell «sjekk nå») står i `fase2-plan.md` og i beslutningene nedenfor. Kodes når egress mot `www.regjeringen.no` er åpnet.
- Småting uavhengig av egress: det diskrete «venter endring»-merket på leser-/adminflaten (Steg I-rest), og FINs ekte `.dotx`-notatmal til Word-eksporten (strukturell layout finnes).
- Ekte ende-til-ende-verifisering av Fase 1 (mot reell Azure SQL/Entra-tenant) er en utrullingsoppgave.
- Åpne spørsmål: De fire IT-forholdene i kravdokumentets kap. 12. Konkret Entra attributt→gruppe-mapping (mekanismen er konfigurerbar via EntraGrupper-seksjonen; verdier avklares med IT). Lokal/CI-kjøring krever .NET 10 SDK (installeres i miljøet) og en database for integrasjon mot ekte SQL.
- Driftsherding før produksjon (identifisert i gjennomgangen, utsatt til utrulling): (a) databasemigrering bør kjøres som eget deploy-steg, ikke ved app-oppstart (unngå crash-loop ved utilgjengelig/pauset DB og race ved skalering); (b) SQL-brannmurens «Allow Azure services» (0.0.0.0) er bred for FIN-interne data — vurder strammere nettverksisolering; (c) web-appens managed identity må gis DB-bruker via T-SQL (dokumentert i infra/README); (d) `HttpSynlighetskontekst` er global ISynlighetskontekst-binding — fungerer for dagens kall, men komponenter må bruke Synlighetskontekstkilde i kretsen.

## Beslutninger

### [2026-06-19] Lokal demo-modus kodet (miljø `Demo`)
- Beslutning: La til en lokal kjøremodus så løsningen kan vises/demonstreres uten Azure SQL og uten ekte Entra-tenant. Gren `claude/demo-modus`, 128 tester grønt, røyktestet kjørende. Egress-uavhengig. Implementert som et eget miljø `Demo` ved siden av produksjonsstien (rører den ikke):
  - **Database:** SQLite (`aarshjul-demo.db`, git-ignorert) i `Demo`; Azure SQL ellers. Skjema via `EnsureCreated()` (EF-migrasjonene er SqlServer-spesifikke). **Fallgruve funnet og fikset:** `ConnectionStrings:Aarshjul` er tom streng i appsettings (ikke null), så `?? "Data Source=…"`-fallbacken slo ikke inn → `UseSqlite("")` ga hver tilkobling sin egen private temp-DB («no such table» rett etter `CREATE TABLE`). Behandler nå tom streng som «ikke satt».
  - **Auth:** cookie-scheme i `Demo` (ingen Entra/OIDC). Dev-innlogging på `/demo` (minimal-API, `DemoEndepunkter`) der man velger en av fem personaer; cookie bærer kun `NameIdentifier`+`Name`, og den **eksisterende** `BrukerClaimsTransformation` beriker rolle/grupper fra DB — så autorisering og server-side synlighet kjører nøyaktig som i produksjon, kun identitetskilden er byttet. Verifisert: admin ser 9 frister, FAG-leser 2, ingen FIN-interne frister lekker til FAG.
  - **Demo-data (`Demodata`, Infrastructure):** fem personaer (admin/FA, bidragsyter/FA, bidragsyter/FIN-FAG, leser/FAG, leser/POL) med **manuelle** gruppemedlemskap (overlever brukeroppslaget, som kun synker Entra-utledede grupper); ~10 frister over budsjettår 2026/2027 med variert synlighet; robot-/bruker-/endringsforslag i køen + ett varsel. Idempotent (hopper over hvis `demo-admin` finnes).
  - **UI:** `MainLayout` er demo-bevisst (banner + innlogg/utlogg peker til `/demo` i `Demo`, til Entra ellers).
- Begrunnelse: Brukeren valgte demo-modus som neste steg for å se løsningen kjøre. Faithful gjenbruk av claims-transformasjonen gjør at demoen tester ekte autorisasjonssti, ikke en forenklet variant.
- Konsekvens: Ingen EF-migrasjon (SQLite via EnsureCreated). Ny pakke `Microsoft.EntityFrameworkCore.Sqlite` i web-prosjektet. 3 nye tester (Demodata). Produksjonsstien (Entra + Azure SQL + `Migrate()`) er urørt. Kjør: `ASPNETCORE_ENVIRONMENT=Demo dotnet run --project backend/Aarshjul.Web`.

### [2026-06-19] Fase 3 (mal og generering) kodet — Steg A–G
- Beslutning: Hele Fase 3 er kodet på gren `claude/cool-clarke-vm4uoi` (125/125 tester grønt), etter `fase3-plan.md`. Brukeren ba om å fortsette kodingen og utsette demoen; Fase 3 var det dokumenterte neste kodetrinnet og er ikke egress-blokkert.
  - **Steg A — datoberegning + typede parametre:** `Datoberegning.FastDato/NteUkedag/Virkedagjuster` (Domain) + `Valgaar.Valgtype` (Domain). Typede parameter-DTO-er (`FastDatoParametre`/`RelativUkedagParametre`/`RelativTilMilepaelParametre`) + `Regelparser` i `Aarshjul.Application/Generering` mot eksisterende JSON-felt. **Virkedagjustering:** helg → nærmeste virkedag, aldri over årsskifte (kun helg, ikke helligdager).
  - **Modelltillegg (krevde EF-migrasjon `Fase3Generering`):** `Gjentaksregel.Tittel` lagt til, fordi en malregel må kunne gi den genererte fristen en meningsfull tittel (regelen hadde ingen). Dette avviker fra planens «ingen migrasjon», men er et reelt behov; migrasjonen legger kun til én nvarchar(512)-kolonne.
  - **`aar_forskyvning` på FastDato/RelativUkedag (nytt parameterfelt, default 0):** kalenderår = budsjettår + forskyvning. Nødvendig fordi årshjulet spenner ~18 mnd (en frist for budsjettår T kan falle i T−1 eller T+1). Bæres i JSON, ingen skjemaendring.
  - **Valgår avgjøres av kalenderåret fristen faktisk faller i** (ikke et fast T−1), via `Valgaar.Type(dato.Year)`. **Kommunevalg-styrke besluttet:** mild — beholder konkret dato, men tennes valgårsflagg per frist. Stortingsvalg → tentativ (månedspresisjon), ingen gjettet dag.
  - **Steg C — `Genereringsberegning` (ren) + `GenereringsTjeneste` (Infrastructure):** anker-kjeder (`RelativTilMilepael`) løses i avhengighetsrekkefølge med sirkularitets-/manglende-anker-deteksjon → tydelig feil framfor gjettet dato. Tentativitet arves nedover kjeden (arver ankerets presisjon). To-trinns flyt: `GenererAsync` tar valgfrie `ManuelleAnkre` (løp→dato), så admin kan sette valgårssensitive ankre og beregne resten konkret. Genererte forslag (`ForslagType.Generert`) er **utelatt fra den løpende køen** (`HentKoAsync` filtrerer dem bort) og lever på egen generér-flate.
  - **Steg D — videreføring av synlighet:** kun fra fjorårsfrist (`Loep` + (målår−1)) med `Status = Godkjent`; ellers synlighetsregelens default. POL bæres med som *forslag* men krever aktiv bekreftelse ved godkjenning (håndheves allerede i `GodkjenningskoTjeneste`).
  - **Steg E — synlighetsregel (konfig-drevet, valgt framfor tabell):** `ISynlighetsregel`/`Synlighetsregel` + `SynlighetsregelOpsjoner` (seksjon `Synlighetsregel`, default `["FA","FIN-FAG"]`). POL fjernes ubetinget. Ingen EF-migrasjon for dette (konfig valgt, jf. plan).
  - **Steg F — generér-flate (`/admin/generer`):** målår-velger, to-trinns manuelle ankre for tentative frister, ny dato ved siden av fjorårets faktiske, tentativ-/valgårsmerke, godkjenn (synlighetsvalg, POL kun aktivt) / juster (gjenbruker `/admin/frist/juster/{id}`) / avvis. «Er du sikker?»-bekreftelse kreves i UI før en tentativ frist godkjennes.
  - **Steg G — malforvaltning (`/admin/mal` + `/admin/mal/{id}`):** `IMaltjeneste`/`Maltjeneste` (opprett/rediger/**slett** — ikke «deaktiver», siden `Gjentaksregel` ikke har `Aktiv`-felt og en sletting er trygg; ingen FK fra frist). Validerer at parametrene kan tolkes for regeltypen. `Startdata` seeder de syv standardløpene (kun når malen er tom).
- Begrunnelse: Følger den ferdige planen; lukker de åpne punktene (kommunevalg = mild; synlighetsregel = konfig; helligdager utenfor). `Gjentaksregel.Tittel` og `aar_forskyvning` var nødvendige tillegg planen ikke hadde forutsett, men de bryter ingen bærende prinsipp.
- Konsekvens: EF-migrasjon `Fase3Generering` lagt til (kjøres ved neste deploy/oppstart). 31 nye tester. Bærende prinsipp holdt: ingenting publiseres uten godkjenning, POL aldri automatisk, synlighet verifisert på spørringssvaret. Gjenstående åpne punkter: presisjon på de seedede standardreglenes datoer (maler admin justerer), helligdagshåndtering.

### [2026-06-19] Steg G (juster fra køen) og Steg K (Word-utskrift) kodet
- Beslutning: De to offline-delbare Fase 2-stegene er kodet (94/94 tester grønt).
  - **Steg G — valg A (felles RedigerFrist-skjema):** «Juster» fra køen åpner `RedigerFrist` i en tredje modus (`/admin/frist/juster/{forslagId}`) forhåndsutfylt fra forslaget; «Lagre» justerer innhold og publiserer i én handling (kravdok. 5.1 «juster … deretter godkjenn»). Publiseringslogikken er trukket ut i en delt privat `PubliserAsync` som både `GodkjennAsync` og `JusterOgGodkjennAsync` bruker, så POL/synlighet håndheves ett sted på server. Endringsforslag skjuler synlighet og rører kun innhold (punkt C). «Ukjent type»-kort får knappen «Vurder» (samme rute) — dekker kravdokumentets manuelle kategorisering. Brukeren valgte A framfor inline-redigering fordi det er spec-tro og gjenbruker hele feltsettet/validering uten duplisering.
  - **Steg K — Word-utskrift:** `FristFilter` utvidet med `FraDato`/`TilDato` (periodevindu på sorteringsdag). `IWordEksport`/`WordEksportTjeneste` (Open XML SDK, `DocumentFormat.OpenXml` 3.5.1 i Infrastructure) bygger .docx med synlig topptekst fra utvalgskriteriet; «alt» merkes FIN-internt. Admin-endepunkt `/api/eksport/word` bygger synlighetskontekst via `Synlighetskontekst.ForGruppe` (eller `SerAlt`) og gjenbruker samme server-side filter; flate `/admin/eksport`. FINs ekte `.dotx` kobles på senere (strukturell layout nå).
- Konsekvens: Gjenstående Fase 2 er kun de egress-blokkerte stegene (B `OppdagAsync`, C dedup/versjonsmatching/«foreslått fjernet», E live datouttrekk, L bakgrunnsjobb), som krever at `www.regjeringen.no` åpnes i egress-allowlisten. Ingen EF-migrasjon i Steg G/K.

### [2026-06-19] Endringsforslag rører aldri synlighet + auto-versjonsmatching og «foreslått fjernet»
- Beslutning (tre presiseringer fra gjennomgang av Steg I/Steg C):
  - **(C) Endringsforslag rører aldri synlighet.** Et endringsforslag (`ForslagType.Endring`) gjelder kun fristens innhold (tittel, dato, budsjettår, kategori, notat). I endre-modus settes ikke `ForeslaattSynlighet`, og synlighetsseksjonen skjules i skjemaet. Ved godkjenning oppdaterer `GodkjennAsync` kun innholdsfeltene; fristens `synlig_for` står urørt, og det kreves ingen synlighetsvalidering. Køflaten viser ingen synlighetsvelger for endringsforslag. **Dette var en korrigering av kodet adferd** — `GodkjennAsync` overskrev tidligere synligheten med admin-input for endringsforslag. Synlighet er admins domene og POL-sensitivt; det styres aldri via et endringsforslag. (Regresjonstest: «godkjent endringsforslag endrer innhold, lar `synlig_for` stå uendret».)
  - **(A) Auto-versjonsmatching (Steg C, egress-blokkert).** Re-uttrukne frister fra en republisert versjon parres på en **funksjons-/tittelavledet identitet innen `Loep` + `Budsjettaar`**, ikke `DokumentId` alene. Låste krav: matching må tåle **flere frister per dokument**; **entydig** match → auto-`Forslag(Endring)`; **tvetydig eller manglende** match → **manuell kobling av admin**, aldri et gjettet auto-endringsforslag; samlet usikker matching sender **hele** re-uttrekket til manuell kobling. Åpent for koding: hvordan funksjonsnøkkelen utledes (tittelnormalisering vs. eget funksjonstype-felt).
  - **(B) «Foreslått fjernet» som tredje køutfall (Steg C/F, egress-blokkert).** Når en publisert frist **mangler match** i et ellers trygt matchet re-uttrekk, tennes et **«foreslått fjernet»**-forslag til administrator — aldri en automatisk fjerning. Krever et modelltillegg (fjernings-/delta-variant på `Forslag`, koblet via `EndrerFristId`) og en køhandling som ved godkjenning avpubliserer fristen.
- Begrunnelse: (C) lukker et reelt avvik mot prinsippet «POL/synlighet aldri automatisk» og holder endringsforslag enkelt og trygt. (A) gjør re-uttrekk robust mot at rundskrivnummer skifter og at ett dokument gir mange frister, uten å gjette. (B) bevarer «aldri miste/feilstille en frist»: også en *fjerning* passerer køen.
- Konsekvens: (C) er kodet og testet (81/81). (A) og (B) er **dokumentert** i `fase2-plan.md` (Steg C/F) og `SYSTEMARKITEKTUR.md` (3.4/5/6), og kodes med Steg C når egress mot `www.regjeringen.no` åpnes. (B) krever EF-migrasjon ved koding.

### [2026-06-19] Designintervju: styrende rammevalg og forfininger av fase 2/3
- Beslutning: Etter et strukturert designintervju (trykktest av hele planen, gren for gren) er følgende låst og styrer videre koding:
  - **Rammevalg v1: FIN først.** Første brukbare versjon målretter FA + FIN-FAG (+ POL eksplisitt) i FINs egen tenant. FAG/øvrige departementer kobles på når IT har åpnet multi-tenant Entra (ett av de fire IT-forholdene). Dette gjør FIN-internt til et naturlig synlighetsstandard for v1.
  - **Prioritet: aldri miste en frist**, men tilgangsstyring er et **hardt gulv i praksis** fordi godkjenningskøen gater alt (robotforslag er `Forslag`, usynlig til admin godkjenner). POL er absolutt — aldri automatisk.
  - **Oppdatert versjon av kjent rundskriv (samme nøkkel, ny hash) → auto endringsforslag.** Re-uttrekk lager `Forslag(Endring)` mot de berørte fristene (koblet via `Loep`+`Budsjettaar`+`DokumentId`), til admins gjennomgang i køen. (Erstatter «flagg, ikke dublett».)
  - **Auto-forkast kun ved lav konfidens OG ingen gjenkjennelig dato — og aldri stille.** Forkastede uttrekk havner i en synlig, reverserbar «forkastet»-liste admin kan gjennomgå og slette. Sletting huskes på `(Kilde, DokumentNokkel)` så samme kilde ikke gjenoppliver den; ny kilde/neste års dokument (ny nøkkel) kan komme inn på nytt. (Forfining av den konservative køen, ikke et brudd: ingenting forsvinner uten spor.)
  - **Synlighetsregel-default (Steg E) = FIN-internt uten FAG.** Robot-/genererte forslag prefylles FA+FIN-FAG (aldri FAG/POL automatisk); admin legger til FAG aktivt. Gjelder kun auto/robot-/genererte forslag — manuell oppretting krever fortsatt aktivt synlighetsvalg.
  - **Datouttrekk abstraheres bak `IDatouttrekk`** (samme prinsipp som `IKilde`): tar PDF-tekst inn, leverer strukturert per-felt-resultat ut. Default Claude API i utvikling/test; endelig provider/lokasjon (ekstern vs Azure-vertet) er et IT-styringsspørsmål som byttes uten ombygging. **Uttrekksfeil: retry et fast antall ganger, så flagg admin** via liveness-sporet — aldri stille.
  - **Fase 3-forfininger låst:** generingsflyt ved manuelt anker = **to-trinns** (admin setter valgårssensitive ankre først, resten beregnes); tentativ-arv = **eksplisitt beregningsregel** kodet og testet fra start i Steg C.
- Begrunnelse: Intervjuet avdekket to spenninger (tilgang vs. kompletthet; auto-forkast vs. «aldri miste») som ble forsont uten å svekke bærende prinsipper — køen gater alt, og ingen forkasting er stille. De øvrige forfiningene lukker tidligere åpne punkter slik at koding kan gå sammenhengende.
- Konsekvens: `fase2-plan.md` (Steg C/E, kap. 8), `fase3-plan.md` (Steg E, C/F), `arkitektur.md` (datouttrekk-rad) og `SYSTEMARKITEKTUR.md` (3.4/5) oppdatert tilsvarende. Forkastet-listen og auto-endringsforslag krever modell-/EF-vurdering i fase 2-kodingen. Fortsatt åpent: kommunevalg-styrke, helligdager, konkret modellvalg bak `IDatouttrekk`, utrullingsherding.

### [2026-06-19] Fase 3-planlegging avsluttet — mal og generering forankret i fase 1-koden
- Beslutning: La fram detaljert fase 3-plan (`fase3-plan.md` i rot), forankret i den faktiske .NET-koden: gjentaksregler tas i bruk (typede parameter-DTO-er mot eksisterende `Gjentaksregel.Regelparametre`-JSON — ingen skjemaendring), «generér neste år» som leser malen og legger `Forslag(Generert)` på en egen gjennomgangsflate adskilt fra køen, synlighetsregler (konfig-drevet anbefalt), og valgårslogikk (`Valgtype(aar)`). Planlegges nå selv om fase 2 ikke er kodet, fordi all planlegging skal være ferdig før koding; fase 3-koding forutsetter fase 2 (godkjenningskøens publisering, Steg F, + redigeringsskjema, Steg G). Gjenbruker `Datoberegning`, `FristskrivingTjeneste`-mønsteret, `Synlighetsfilter` og `ForslagType.Generert` fra fase 1-modellen.
- Lukkede designvalg: (a) anker-kjeder (`RelativTilMilepael`) løses topologisk, og sirkulær/uoppløselig kjede feiler tydelig med varsel til administrator — manuelt satt anker er utvei; (b) tentativitet arves nedover anker-kjeden (arver ankerets presisjon, ikke bare dato), så en frist kan bli tentativ uten selv å være valgårssensitiv; (c) synlighet videreføres kun fra fjorårsfrister med `Status = Godkjent`, ellers tom synlighet, og POL videreføres aldri stilltiende (krever aktiv bekreftelse); (d) virkedagjustering flytter til nærmeste virkedag ved helg, men aldri over årsskifte.
- Gjenstående åpne punkter (besluttes ved koding): genereringsflyt ved manuelt anker (én omgang med tentative forslag vs. to-trinns); om tentativ-propagering kodes som eksplisitt regel nå eller avklares ved koding; kommunevalg-variantens styrke (mild vs. som stortingsvalg); helligdagshåndtering (kun helg i første omgang); synlighetsregel-lagring (konfig vs. tabell).
- Begrunnelse: Planleggingen skulle gjøres helt ferdig for alle faser slik at koding kan gå sammenhengende etter plan-godkjenning, uten flere avklaringsrunder.
- Konsekvens: Fase 3-koding starter (etter at fase 2 er kodet) fra stegene A–G i fase3-plan.md. Ingen EF-migrasjon nødvendig med mindre synlighetsregler velges tabelldrevet (`Fase3Generering`).

### [2026-06-18] Fase 2-planlegging avsluttet — gjenstående designvalg lukket
- Beslutning: Lukket de gjenstående åpne designvalgene for fase 2 (se fase2-plan.md kap. 8): Claude API for datouttrekk; `DokumentNokkel` = normalisert `r-{nr}-{aar}` + SHA-256 `InnholdHash`; mellomtilstand/forsøksteller som felt på `BehandletDokument` (ikke egen kø); liveness via en liten `InnhentingsStatus`-tilstand; bakgrunnsjobb som .NET `BackgroundService` i web-hosten; Word via Open XML SDK. Kun FINs notatmal-fil og de fire IT-forholdene krever ekstern avklaring og blokkerer ikke kodestart.
- Begrunnelse: Fase 2-planleggingen skulle gjøres helt ferdig slik at en ny økt kan gå rett på koding (etter plan-godkjenning) uten flere avklaringsrunder.
- Konsekvens: Fase 2-koding starter med EF-migrasjonen `Fase2Innhenting` (forsøksteller + liveness-spor), deretter stegene A–L i fase2-plan.md.

### [2026-06-18] Fase 2-planlegging forankret i fase 1-koden (etter merge av PR #1)
- Beslutning: Etter at PR #1 (full fase 1) ble merget til main, ble den detaljerte fase 2-planen skrevet om til å være forankret i den faktiske .NET-koden (`fase2-plan.md`): hvert byggesteg er kartlagt til hvor det bor (`backend/kilder`, `backend/jobb`, Application-tjenester, Web/admin), og gjenbruker `Synlighetsfilter`, `FristskrivingTjeneste`-mønsteret og auth-policyene. Designet er innarbeidet i SYSTEMARKITEKTUR.md (3.2, 3.4, 5, ny kap. 9) og BRUKERHISTORIER.md (4.1, 4.2, 4.6, ny 4.12). To modelltillegg er identifisert som nødvendige i fase 2 (krever EF-migrasjon): forsøksteller/mellomtilstand på `BehandletDokument`, og et liveness-spor for «sist vellykkede innhenting».
- Begrunnelse: Min fase 2-planlegging var opprinnelig gjort stack-agnostisk på en dokument-only main, før jeg oppdaget at PR #1 allerede hadde kodet fase 1 i .NET. Planen måtte forankres i den reelle koden for å være brukbar.
- Konsekvens: Stack/DB/frontend/datamodell er ikke lenger åpne spørsmål. Gjenstående åpne forhold er listet i fase2-plan.md kap. 8.

### [2026-06-18] Fase 2-design: kildegrensesnittet uttrykker utfall
- Beslutning: `Kilde.oppdag()` returnerer en utfallstilstand — fant nye / ingen nye / klarte ikke parse — ikke bare en liste. En tom liste fra en vellykket kjøring skilles fra en feilet kjøring.
- Begrunnelse: Stille feil (endret sidestruktur) må ikke kunne forveksles med en stille periode uten nye rundskriv.
- Konsekvens: Utfallet driver liveness og varsling. Gjelder alle kilder bak grensesnittet i `backend/kilder`.

### [2026-06-18] Fase 2-design: liveness og stille-feil-varsling
- Beslutning: «Klarte ikke parse» varsler administrator. Admin-flaten viser «sist vellykkede innhenting» med `oppdag()` og `hent()`/uttrekk sporet hver for seg. Et dokument som feiler i `hent()` prøves et fast antall ganger (forsøksteller på `BehandletDokument`) og flagges til administrator når grensen er nådd.
- Begrunnelse: Et automatisk ledd som svikter stille er verre enn ingen automatikk; administrator må se at innhentingen lever og hvor den eventuelt står.
- Konsekvens: Krever modelltillegg (forsøksteller + liveness-spor) og EF-migrasjon i fase 2.

### [2026-06-18] Fase 2-design: usikkerhetsflagg styres av deterministiske regler
- Beslutning: Per-felt usikkerhetsflagg på robotuttrekk tennes av verifiserbare regler (relativ formulering tolket til hard dato, kildespenn uten gjenkjennelig dato, brutt fornuftsregel). `UttrekksBevis.Konfidens` er ett bidrag, aldri eneste utløser. Kildeutdraget vises alltid ved siden av tolket verdi.
- Begrunnelse: Et flagg som bare speiler modellens selvtillit er ikke testbart og gir ikke administrator et pålitelig kontrollpunkt.
- Konsekvens: Flagg-logikken bygges i Application-laget over `UttrekksBevis`; gir konkrete, testbare akseptkriterier.

### [2026-06-18] Fase 2-design: Word-utskrift med utvalgs-topptekst, ingen logging
- Beslutning: Word-utskriften bærer en synlig topptekst generert fra det faktiske utvalgskriteriet (gruppe + periode). «Alt»-utskrift merkes tydeligst som FIN-internt. Selve utskriftshandlingen logges ikke.
- Begrunnelse: En utskrift må ikke kunne forveksles med en annen gruppes utvalg; «alt» er mest sensitivt. Logging av utskrift gir liten verdi mot kontrollkostnaden (jf. linjen om å unngå egen POL-logging).
- Konsekvens: Generatoren (backend) utleder toppteksten fra utvalgsparametrene; utvalget bruker `Synlighetsfilter`.

### [2026-06-18] Fase 1 avsluttet og kodegjennomgang gjennomført
- Beslutning: Avsluttet Fase 1 etter en strukturert kodegjennomgang av PR #1 (fire fokuserte review-agenter: sikkerhet/auth, datalag, Blazor, infra). Fem bekreftede feil ble rettet og dekket av nye regresjonstester (se Status nå). Driftsherding (migrering ved oppstart, SQL-brannmurens bredde, MI-DB-bruker, default ISynlighetskontekst-binding) er bevisst utsatt til utrullingsfasen og listet under «Åpne spørsmål». Stack-anbefalingene som gjensto er nå bekreftet i praksis: hosting = Azure App Service (Linux, .NET 10).
- Begrunnelse: Fasens hovedkvalitetskrav (server-side synlighet verifisert på API-svaret) er oppfylt og testdekket. Resten er drift som hører til utrulling, ikke fundamentkoden.
- Konsekvens: PR #1 er klar for review/merge. Fase 2 starter i ny økt fra punktene i «Status nå»; arkitektur.md har miljøoppsett og kart over hvor Fase 1-koden bor.

### [2026-06-18] Fase 1 fundament implementert (steg 1–8)
- Beslutning: Bygde hele Fase 1 på .NET 10. Sikkerhetskjernen: ett `Synlighetsfilter` som både Blazor-visningene og minimal-API-et bruker; rolle/grupper bæres som claims satt fra DB via en claims-transformasjon; manuell innlegging validerer at synlighet er valgt (POL kun ved aktivt valg). Tester (20) dekker filtrering på tjeneste- og HTTP-nivå, datoberegning og innleggingsvalidering. Infra som Bicep (App Service Linux + Azure SQL serverless med Entra-only auth + Key Vault RBAC + App Insights); deploy via manuell GitHub Actions-workflow med OIDC.
- Begrunnelse: Oppfyller fasens hovedkvalitetskrav — synlighet håndheves på server og er verifisert på selve API-svaret. Entra-only SQL og Key Vault unngår lagrede hemmeligheter.
- Konsekvens: Web-appens managed identity må gis DB-tilgang via T-SQL etter første deploy (dokumentert i infra/README). Hosting-form bekreftet til App Service. Klar for Fase 2.

### [2026-06-18] Fase 1: Blazor (Interactive Server) + lagdelt .NET-solution
- Beslutning: Frontend bygges i Blazor Web App med render mode Interactive Server, servert fra samme ASP.NET Core-host som API-et. Solution under backend/ deles i Domain/Application/Infrastructure/Web/Tests. All synlighetshåndheving går gjennom ett Synlighetsfilter; rolle og grupper bæres som claims (satt av en claims-transformasjon fra DB) slik at policyer og synlighetskontekst bygges fra claims. Migrasjoner er SqlServer; tester kjører mot SQLite in-memory + WebApplicationFactory.
- Begrunnelse: Interactive Server rendrer på server og sender aldri data klienten ikke har rett til — oppfyller det bærende prinsippet uten ekstra klientbeskyttelse. Ett språk/stack for hele teamet. Claims-basert tilgang gir idiomatiske ASP.NET-policyer.
- Konsekvens: arkitektur.md frontend-rad bekreftet til Blazor. Minimal-API beholdes ved siden av Blazor for å kunne verifisere filtrering på selve JSON-svaret og for framtidige klienter.

### [2026-06-18] Stack: backend .NET/ASP.NET Core, database Azure SQL
- Beslutning: Backend-API og bakgrunnsjobb bygges i .NET (LTS) / ASP.NET Core (C#) med EF Core, og databasen er Azure SQL. `frist.synlig_for` modelleres med en koblingstabell (frist ↔ gruppekode). Frontend-rammeverk, hosting-form og bakgrunnsjobb-form er fortsatt åpne.
- Begrunnelse: .NET gir tett, godt støttet integrasjon mot Entra ID og Azure og er lett å vedlikeholde internt; Azure SQL passer tett sammen med .NET/EF Core. Ett rammeverk dekker API + jobb, slik at autorisering og synlighetsfiltrering ligger ett sted.
- Konsekvens: arkitektur.md oppdatert med stack og .NET-kommandoer. Fase 1 starter med solution-oppsett og EF Core-datamodell. Server-side synlighetsfiltrering blir en indeksert join mot koblingstabellen.

### [2026-06-18] Kontekstapparat og mappestruktur etablert (igangsetting)
- Beslutning: Opprettet `CLAUDE.md` i rot og flyttet `beslutningslogg.md` og `arkitektur.md` til `.claude/rules/` slik opplegget (claude-md-opplegg.md) og utviklingsplanens Del A foreskriver. Opprettet tom mappestruktur med plassholder-README-er. Stacken er bevisst IKKE valgt nå.
- Begrunnelse: Hver Claude Code-økt starter med tomt kontekstvindu; det som overlever ligger på disk. Apparatet må stå før første kodeøkt. Stack-valget tilhører dem som skal vedlikeholde løsningen og loggføres når det tas.
- Konsekvens: Fremtidige økter laster CLAUDE.md + `.claude/rules/`-filene ved oppstart. Fase 1 starter med å bekrefte stack og fylle ut arkitektur.md.

### [2026-06-18] Kildelenke på leserflaten
- Beslutning: Frister fra offentlige rundskriv viser lenke til kildedokumentet for alle som har tilgang til fristen. Lenken arver fristens synlighet. Manuelle og genererte frister viser ingen lenke og intet opphavsmerke på leserflaten.
- Begrunnelse: Lar brukere finne tilbake til kilden. Trygt fordi måldokumentet uansett er offentlig og synlighet allerede styres på fristen.
- Konsekvens: Leserflaten bruker `frist.dokument_id` + dokumentets URL fra behandlet-dokument-registeret. Vises kun når den finnes.

### [2026-06-18] Endringsforslag på publiserte frister
- Beslutning: Bidragsytere kan foreslå endring på en allerede publisert frist. Endringsforslag refererer fristen via `endrer_frist_id` og bærer foreslåtte nye verdier. Original står uendret til admin godkjenner; admin ser et diskret «venter endring»-merke. Ligger i samme kø som filtrerbar type med før/etter-visning. Flere samtidige endringsforslag mot samme frist er tillatt og vurderes hver for seg.
- Begrunnelse: Oversikten må kunne holdes oppdatert (f.eks. datoendring) uten å gå utenom løsningen, uten å vise ubesluttede endringer til lesere.
- Konsekvens: Ny forslagstype i datamodellen. «Venter endring»-merket utledes ved oppslag på åpne forslag med matchende `endrer_frist_id`.

### [2026-06-18] Datopresisjon og tentative frister
- Beslutning: Frist får presisjonsfelt ved siden av dato: normalt `dag`, kan løsnes til `maaned` (unntaksvis primo/medio/ultimo). En avledet sorteringsdag beregnes ved lagring. Tentativ frist teller med på landingsflaten, men merkes alltid synlig som tentativ. Godkjenning uten konkret dag krever aktiv «er du sikker?»-bekreftelse.
- Begrunnelse: Valgårssensitive høstfrister har ingen meningsfull dato før regjeringsdannelsen; å gjette en falsk presis dato er verre enn å vise ærlig usikkerhet.
- Konsekvens: All visnings-, sorterings- og historikklogikk beholder ett entydig sorteringspunkt. Berører alle tre visninger og landingsflaten.

### [2026-06-18] Avvist-status — forslag slettes ikke
- Beslutning: `status` utvides med `avvist`. Avviste forslag bevares (slettes ikke). Avviste brukerforslag kan redigeres og sendes inn på nytt. Rundskriv som er behandlet foreslås derimot aldri på nytt (uendret dedup-regel).
- Begrunnelse: Bidragsyter må kunne se utfall i «mine forslag» og rette en liten feil uten å miste arbeidet. Rundskriv og brukerforslag har bevisst ulik gjenbruksregel.
- Konsekvens: Avvist-spor i datamodellen; dedup mot behandlet-dokument-registeret gjelder kun kilder, ikke brukerforslag.

### [2026-06-18] In-app-varsel som eneste push-kanal
- Beslutning: Bidragsyter varsles om godkjenning/avvisning kun inne i løsningen (innboks/teller med lest/ulest). Avvisning kan ha valgfri begrunnelse. Ingen e-post eller annen utgående kanal i denne omgang.
- Begrunnelse: Lukker tilbakemeldingssløyfen uten å dra inn utgående e-post fra Azure, som har egne IT-/driftshensyn.
- Konsekvens: Nytt varselbegrep per bruker-id i datamodellen. Eneste push mot bruker; alt annet er pull.

### [2026-06-18] Uttrekksbevis per felt på robotforslag
- Beslutning: Robotforslag bærer per felt både tolket verdi, tekstutdraget verdien kom fra, og et konfidensnivå som tenner et per-felt usikkerhetsflagg. Kortet viser tolkede felter + utdrag + kildelenke + flagg. Beviset hører til forslaget, ikke den publiserte fristen.
- Begrunnelse: PDF-uttrekk blir aldri feilfritt; admin må kunne verifisere på sekunder uten å forlate køen, ellers blir «godkjenn» en refleks.
- Konsekvens: Datouttrekket (4.4) må levere strukturert per-felt-resultat med kildespenn og usikkerhet, ikke bare en dato.

### [2026-06-18] Godkjenningskø: enkeltkort gruppert per kilde
- Beslutning: Køen er selvstendige enkeltkort gruppert per kilde, hver godkjennes for seg — ingen masse-godkjenning. Filtre på opphav, kilde, ukjent type, kategori (og forslagstype). Kilde må fremgå tydelig per kort.
- Begrunnelse: Ett rundskriv kan gi mange frister; flat blanding drukner enkeltforslag. Konservativ linje: heller én vurdering for mye enn én oversett frist.
- Konsekvens: Gruppering på kilde er strukturen i køflaten; gjenbrukes av endringsforslag og brukerforslag.

### [2026-06-18] Generering: egen gjennomgangsflate med fjorårssammenligning
- Beslutning: «Generér neste år» legges på egen flate adskilt fra køen. Hver frist viser ny beregnet dato ved siden av fjorårets faktiske. Valgårsflagg per frist (intet banner). Synlighet videreføres fra fjoråret.
- Begrunnelse: Generering er en bevisst, sesongbestemt handling med annen rytme enn løpende forslag, og krever sammenligning mot fjoråret.
- Konsekvens: Genereringen slår opp fjorårets tilsvarende frist via `loep` + forrige budsjettår.

### [2026-06-18] Ren kalender, ikke statusverktøy
- Beslutning: Frist vises t.o.m. fristdagen og flyttes til historikk dato + 1. Ingen «forsinket»-tilstand for noen rolle. `fullfoert` blir i praksis lite brukt på brukerflaten. Historikk vises ikke automatisk og filtreres med nåværende synlighetsregler.
- Begrunnelse: Verktøyet skal fortelle hva som skjer når, ikke om en frist ble overholdt. Enkelt og forutsigbart på tvers av departementer.
- Konsekvens: Livssyklusen til en frist styres av dato alene; ingen kvitteringsmekanikk på leserflaten.

### [2026-06-18] Landingsflate: union av «30 dager» og «neste 5»
- Beslutning: Landingsflaten viser alle frister innen 30 dager OG minst de 5 førstkommende (union), rullerende, filtrert på brukerens grupper. «Vis flere» henter resten framover.
- Begrunnelse: «Minst 5» garanterer at flaten aldri er tom i stille perioder; «innen 30 dager» fanger travle perioder.
- Konsekvens: Server-side spørring må kombinere de to kriteriene; gjelder alle roller.

### [2026-06-18] Administrator-innsyn i tre lag
- Beslutning: (1) Diskret gruppemerking på hvert fristkort, utvides ved klikk. (2) «Se som rolle» — opplev en gruppes flate. (3) Revisjonsliste per gruppe («alt delt med POL») med øvrige grupper synlige. Alle gjenbruker server-side filtrering.
- Begrunnelse: Erstatter behovet for egen POL-logging med aktiv kontroll admin kan gjøre når som helst.
- Konsekvens: «Se som» kjører den valgte gruppens synlighetsspørring i backend; ingen ny sikkerhetsmekanisme.

### [2026-06-18] POL settes alltid eksplisitt, uten egen logging
- Beslutning: `POL` er eget avkrysningsfelt, aldri forhåndshuket. Verken synlighetsregel eller brukerforslag kan sette POL automatisk. Ingen egen logging av POL-endringer (bevisst valg; admin-innsyn dekker kontrollbehovet).
- Begrunnelse: Konsekvensen av feil POL-deling er politisk; «aldri automatisk» oppfylles av ikke-forhåndshuket felt. Logging valgt bort til fordel for aktiv «se alt delt med POL».
- Konsekvens: Synlighetsregler produserer aldri POL; godkjenning av POL-forslag krever eksplisitt bekreftelse.

### [2026-06-18] Administrator utpekes av sittende admin; nødgjenoppretting via drift
- Beslutning: Administrator (kun FA-ansatte) utpekes alltid av en sittende administrator. Ingen selvbetjent «gjør meg til admin»-knapp. Nødvei når stolen er tom: re-seed via driftsmiljøet (Azure), ikke en funksjon i grensesnittet.
- Begrunnelse: En stående selvbetjent admin-knapp ville gjort admin-rollen reelt selvtildelt for hele FA og uthult godkjenningskøen. Drifts-reseed krever Azure-tilgang som få har.
- Konsekvens: Løste en motsetning i designvalgene (selvbetjent vs. drift) til fordel for drift som eneste nødvei.

### [2026-06-18] Gruppemedlemskap: Entra-mapping + manuell overstyring
- Beslutning: Gruppemedlemskap utledes fra Entra-attributter via konfigurerbar mapping, med mulighet for manuell overstyring i appen. Krever en enkel brukeradministrasjonsflate for admin.
- Begrunnelse: Egendefinerte grupper må kunne få medlemmer raskt uten IT-runde for hver gruppe, men Entra-mapping er standarden.
- Konsekvens: Datamodellen må skille Entra-utledet medlemskap fra manuelt satt, så manuell overstyring ikke overskrives ved neste innlogging.
