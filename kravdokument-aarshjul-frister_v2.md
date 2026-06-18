# Kravdokument: Årshjul for budsjettfrister

Teknisk spesifikasjon for en delt fristoversikt knyttet til arbeidet med statsbudsjettet i Finansdepartementet (Seksjon for statsbudsjett og regnskap). Dokumentet er skrevet for en utvikler og kan brukes operativt som utgangspunkt for implementasjon.

---

## 1. Formål og kontekst

Løsningen skal gi en koordinator oversikt over alle frister i arbeidet med statsbudsjettet, og dele denne oversikten med 20–30 saksbehandlere. Fristene hentes primært fra Finansdepartementets offentlige rundskriv på regjeringen.no, suppleres manuelt, og presenteres som et flerårig årshjul der tidligere år kan studeres og kommende år kan genereres fra mal.

Løsningen er tilgjengelig for alle ansatte i departementene (FIN og øvrige departementer) samt politisk ledelse, med ulik tilgang avhengig av tilhørighet. Innlogging skjer med departementenes SSO (Entra ID). Løsningen driftes på Azure og infrastrukturen beskrives som kode (Bicep).

Kjerneprinsipper som styrer hele designet:

- Frister identifiseres på **funksjon**, ikke på rundskrivnummer (nummeret kan skifte mellom år).
- Automatisk uttrekk går aldri rett til publisering — alt passerer en **godkjenningskø** styrt av administrator.
- **Tilgang og synlighet håndheves på server** (Azure), aldri kun i nettleseren. Enkelte frister er kun for FIN-interne øyne, og data en bruker ikke har rett til å se, skal aldri sendes til nettleseren.
- Rollene danner en delvis **rangstige** (administrator ser alt, FA mye, FAG mindre), men synlighet styres av **grupper** som kan overlappe fritt, ikke av et rent nivåtall.
- Et rundskriv som er **behandlet før** (godkjent eller avvist), skal ikke foreslås på nytt.
- **Kilde** er et utbyttbart ledd, slik at flere kilder (f.eks. DFØ) kan kobles på senere uten ombygging.

---

## 2. Roller, grupper og tilgang

### 2.1 Hvem kan logge inn

Alle ansatte i departementene kan logge inn via Entra ID (departementenes SSO). Tilgangsnivået avgjøres av hvilken gruppe brukeren tilhører. Brukere utenfor organisasjonen har ingen tilgang.

### 2.2 Funksjonsrettigheter (hva en bruker kan gjøre)

| Rettighet | Administrator | Bidragsyter (FIN) | Leser |
|---|---|---|---|
| Se frister (innenfor sine grupper) | ja | ja | ja |
| Sende inn forslag | ja | ja | nei |
| Godkjenne, endre, generere, slette | ja | nei | nei |
| Utpeke andre administratorer | ja | nei | nei |

Funksjonsrettighet er uavhengig av synlighetsgruppe (2.3). En bruker har én funksjonsrolle og én eller flere synlighetsgrupper.

Regler for funksjonsrolle:
- **Administrator** kan kun være en FIN-ansatt. Administrator utpekes av en annen administrator inne i appen (ikke via Entra-grupper). Den første administratoren settes opp ved idriftsetting (seed).
- **Bidragsyter** (kan lese og foreslå) er standard for FIN-ansatte.
- **Leser** (kun lese, ingen forslag) er standard for ansatte i øvrige departementer og for politisk ledelse.
- En ansatt i øvrig departement kan **ikke** bli administrator og ikke sende forslag.

### 2.3 Synlighetsgrupper (hvilke frister en bruker ser)

Hver bruker tilhører én eller flere synlighetsgrupper. Hver frist merkes med hvilke grupper den er synlig for. En bruker ser en frist hvis minst én av brukerens grupper er blant fristens synlighetsgrupper. Administrator ser alt uavhengig av merking.

Synlighetsgrupper er **data administrator forvalter i appen**, ikke en fast liste i koden. Administrator kan opprette, gi nytt navn til og deaktivere grupper (f.eks. legge til «Skatteøkonomisk avdeling» eller «Økonomiavdelingen»). Følgende grupper er de innebygde standardene ved oppstart (seed):

| Gruppe | Beskrivelse | Typisk tildeling |
|---|---|---|
| `FA` | Finansavdelingen i FIN | FA-ansatte |
| `FIN-FAG` | Øvrige avdelinger i FIN | FIN-ansatte utenfor FA |
| `FAG` | Fagdepartementene (alle departementer unntatt FIN) | Ansatte i øvrige departementer |
| `POL` | Politisk ledelse | Politikere |

Datamodell for en gruppe (se også 3.6): `id`, `kode` (stabil nøkkel, f.eks. `SKOK`), `navn` (visningsnavn), `aktiv` (bool), `er_standard` (bool, om gruppen er en innebygd standard). En frists `synlig_for` (3.1) refererer til gruppe-`kode`, slik at nye grupper virker uten skjemaendring. Når en gruppe deaktiveres, beholdes historiske frister som pekte på den, men gruppen tilbys ikke i nye valg.

Gruppene danner en uformell rangstige (FA ser typisk mest blant ikke-administratorer, FAG ser kun det som er relevant for fagdepartementene, ikke FINs interne arbeid), men modellen er ikke et nivåtall. Synlighet er et **sett av grupper per frist**, slik at vilkårlige overlapp er mulig: en frist kan være synlig for `FA` + `POL` men ikke `FAG`, en annen for `FA` + `FAG` men ikke `POL`. Mapping fra Entra-attributter (departement, avdeling) til gruppe defineres i konfigurasjon; nye, egendefinerte grupper kan enten mappes fra Entra-attributter eller tildeles eksplisitt per bruker. `POL` settes typisk eksplisitt.

### 2.4 Standard og regler for synlighet

- Når en frist opprettes manuelt, **må** administrator velge synlighetsgrupper aktivt (ingen stilltiende standard).
- Det skal kunne defineres **synlighetsregler** som forhåndsutfyller forslag, f.eks. «frister fra rundskriv er synlige for alle grupper unntatt `POL`». Reglene gir et forslag administrator kan overstyre.
- `POL` skal **aldri** settes automatisk av en regel — synlighet for politisk ledelse velges alltid eksplisitt av administrator.
- Når «generér neste år» (5.4) lager forslag fra fjorårets frister, **videreføres synligheten** fra forrige år som standard (administrator kan justere).

All filtrering på synlighet skjer på server. Klienten mottar kun frister brukeren har rett til å se.

---

## 3. Datamodell

### 3.1 Frist (deadline)

Den publiserte enheten brukeren ser.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | uuid | Primærnøkkel |
| `tittel` | string | Kort beskrivelse av oppgaven |
| `dato` | date | Selve fristen |
| `budsjettaar` | int | Budsjettåret fristen tilhører (ikke nødvendigvis = kalenderår for `dato`) |
| `kategori` | enum | `budsjett` \| `gulbok` \| `regnskap` |
| `loep` | string (fk) | Referanse til løp/milepæl i malen (se 3.3) |
| `kilde` | string | Opphav: rundskriv-id (f.eks. «R-4/2026») eller «manuell» |
| `dokument_id` | uuid \| null | Kobling til behandlet kildedokument (se 3.4), hvis fristen stammer fra et rundskriv |
| `synlig_for` | string[] | Sett av synlighetsgrupper, f.eks. `["FA","FAG"]` (se 2.3) |
| `status` | enum | `forslag` \| `godkjent` \| `fullfoert` |
| `opphav` | enum | `robot` \| `bruker` \| `admin` |
| `foreslaatt_av` | string \| null | Brukeridentifikasjon når `opphav = bruker`; hentes automatisk fra innlogget identitet (Entra ID) |
| `notat` | text | Fritekst |
| `gjenta_regel_id` | uuid \| null | Kobling til gjentaksregel (se 3.3) hvis fristen er årlig tilbakevendende |

`budsjettaar` som eget felt er bevisst: en frist kan falle i januar t men gjelde budsjettåret t+1. Visningen grupperer og fargelegger på `budsjettaar`. `synlig_for` håndheves på server; en frist sendes aldri til en bruker som ikke har en matchende gruppe.

### 3.2 Kategori og synlighet

Tre kategorier styrer filtrering i visningen (uavhengig av fargekoding på budsjettår):

- `budsjett` — marskonferanse, hovedbudsjettskriv/rammefordeling, revidert nasjonalbudsjett, nysaldering
- `gulbok` — bekreftelsesbrev og innlevering av tekst til Gul bok
- `regnskap` — statsregnskapets årsavslutning, rapportering til statsregnskapen

Standardvisning: `budsjett` + `gulbok` på, `regnskap` av (kan slås på). Brukeren skal kunne huke av/på per kategori.

### 3.3 Årsmal og gjentaksregler

Malen lagrer **regler**, ikke datoer. Når et nytt budsjettår genereres, beregnes konkrete datoer fra reglene som utkast.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | uuid | Primærnøkkel |
| `loep` | string | Funksjonsnavn, f.eks. «rammefordeling», «marskonferanse» |
| `kategori` | enum | Som 3.2 |
| `regeltype` | enum | `fast_dato` \| `relativ_ukedag` \| `relativ_til_milepael` |
| `regelparametre` | json | Se under |
| `valgaarssensitiv` | bool | Om høstløpet påvirkes av valg (se kap. 6) |

Regeltyper:

- `fast_dato`: samme dato hvert år (justeres til nærmeste virkedag ved helg). Parametre: `{ "maaned": 7, "dag": 20 }`.
- `relativ_ukedag`: f.eks. «andre uke i mars». Parametre: `{ "maaned": 3, "uke": 2, "ukedag": "man" }`.
- `relativ_til_milepael`: forskyvning fra annen milepæl. Parametre: `{ "anker_loep": "fremleggelse", "offset_dager": -7 }`.

`relativ_til_milepael` flytter seg automatisk når ankeret flyttes — nyttig for frister bundet til budsjettframleggelsen.

### 3.4 Behandlet dokument (deduplisering)

For å unngå at samme rundskriv foreslås flere ganger, føres et register over dokumenter systemet allerede har sett. Et dokument skal **ikke** gi nye forslag hvis det er registrert her — uavhengig av om fristene fra det ble godkjent eller avvist.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | uuid | Primærnøkkel |
| `kilde` | string | Hvilken kilde (f.eks. `regjeringen`) |
| `dokument_nokkel` | string | Stabil identifikator for dokumentet (se under) |
| `innhold_hash` | string | Hash (f.eks. SHA-256) av dokumentinnholdet |
| `tittel` | string | Dokumentets tittel |
| `url` | string | Lenke til dokumentet |
| `forst_sett` | datetime | Når dokumentet ble oppdaget |
| `behandlet_status` | enum | `ny` \| `forslag_laget` \| `ferdig_behandlet` |

`dokument_nokkel` bør være stabil og uavhengig av nummer (siden rundskrivnummer kan skifte). Anbefalt: en normalisert kombinasjon av kilde + løp + budsjettår der det lar seg utlede, ellers en normalisert URL/filsti. `innhold_hash` fanger tilfellet der samme dokument republiseres med endret innhold: hvis nøkkelen er kjent men hash er ny, kan systemet flagge «oppdatert versjon» til administrator i stedet for å lage dublettforslag. Oppdagelsesjobben (4.2) sjekker mot dette registeret **før** den lager forslag, og hopper over alt som allerede er registrert.

### 3.5 Bruker

Brukeren autentiseres via Entra ID; identitet og navn hentes fra token, ikke fra manuell inntasting.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | string | Stabil bruker-id fra Entra (oid/sub) |
| `navn` | string | Visningsnavn fra Entra (brukes i `foreslaatt_av`) |
| `funksjonsrolle` | enum | `administrator` \| `bidragsyter` \| `leser` (se 2.2) |
| `grupper` | string[] | Synlighetsgrupper (se 2.3), utledet fra Entra-attributter + evt. eksplisitt tildeling |
| `er_fin` | bool | Om brukeren er FIN-ansatt (forutsetning for å kunne bli administrator) |

Administratorrollen lagres her og settes av en annen administrator inne i appen, ikke via Entra-grupper. Funksjonsrolle og synlighetsgrupper er adskilte akser.

### 3.6 Synlighetsgruppe

Grupper er administrerbare data (jf. 2.3), slik at administrator kan opprette nye uten kodeendring.

| Felt | Type | Beskrivelse |
|---|---|---|
| `id` | uuid | Primærnøkkel |
| `kode` | string | Stabil nøkkel det refereres til fra `frist.synlig_for`, f.eks. `SKOK` |
| `navn` | string | Visningsnavn, f.eks. «Skatteøkonomisk avdeling» |
| `aktiv` | bool | Om gruppen tilbys i nye valg |
| `er_standard` | bool | Om gruppen er en innebygd standard (FA, FIN-FAG, FAG, POL) |

`frist.synlig_for` lagrer en liste av gruppe-`kode`. Det gjør at en ny gruppe virker umiddelbart i synlighetsvalg og utskrift uten skjemaendring. Deaktivering (`aktiv = false`) skjuler gruppen fra nye valg, men historiske frister beholder referansen.

---

## 4. Kilder og innhenting

### 4.1 Kildeabstraksjon

Innhenting bygges som et utbyttbart ledd. Definér et grensesnitt `Kilde` med to operasjoner:

- `oppdag()` → liste over dokumentreferanser (id, tittel, dato, url)
- `hent(dokumentreferanse)` → råtekst/PDF-innhold

Første implementasjon: `RegjeringenKilde`. Senere: `DFOeKilde`. Resten av systemet er kildeagnostisk; alle kilder leverer inn i samme klassifisering og godkjenningskø.

### 4.2 RegjeringenKilde — oppdagelse

Oversiktsside (sortert på år og nummer):
`https://www.regjeringen.no/no/dokument/dep/fin/rundskriv/arkiv/id446220/`

Tabellrader har kolonnene Nummer (med lenke til PDF), Tittel, Dato, Status. PDF-URL-mønster for årlige rundskriv:
`https://www.regjeringen.no/globalassets/departementene/fin/rundskriv/arlige/{aar}/r-{nr}-{aar}.pdf`
(eldre årganger bruker varianten `.../upload/fin/vedlegg/okstyring/rundskriv/arlige/{aar}/...`).

Jobben kjører periodisk (f.eks. daglig), leser oversiktssiden, og registrerer rader som ikke er sett før. Før den lager forslag, sjekkes hvert dokument mot registeret over behandlede dokumenter (3.4): er `dokument_nokkel` allerede registrert, hoppes dokumentet over og foreslås ikke på nytt — også når fristene tidligere ble avvist. Er nøkkelen kjent men `innhold_hash` ny, flagges dokumentet som «oppdatert versjon» til administrator framfor å lage dublettforslag.

### 4.3 Totrinns filtrering

**Trinn 1 — nummerserie.** Status-/nummerlogikk:
- Nummer 1–99 = årlig karakter → kan inneholde frister, behandles videre.
- Nummer 100–199 = varig karakter (regelverk) → ignoreres.

**Trinn 2 — tittelgjenkjenning.** Match tittel mot kjente mønstre per løp. Titlene er svært formelfaste over tid. Foreslåtte mønstre (case-insensitivt delstreng/regex):

| Løp | Kategori | Tittelmønster | Typisk nr. (hint, ikke nøkkel) |
|---|---|---|---|
| marskonferanse | budsjett | «materialet til regjeringens konferanse i mars» | R-9 |
| rammefordeling | budsjett | «hovudbudsjettskriv» / «hovedbudsjettskriv» | R-4 |
| rnb | budsjett | «tilleggsbevilgninger og omprioriteringer våren» | R-3 |
| nysaldering | budsjett | «tilleggsløyvingar i haustsesjonen» / «ny saldering» | R-7 |
| gulbok | gulbok | «bekreftelsesbrev og innlevering av tekst til gul bok» | R-6 |
| statsregnskap | regnskap | «årsavslutning og frister for innrapportering» | R-8 |
| rapportering | regnskap | «rapportering til statsrekneskapen» | R-10 |

Nummeret brukes kun som svakt hint/validering, aldri som nøkkel — det kan skifte mellom år.

**Sikkerhetsnett.** Et årlig rundskriv (trinn 1 = ja) som ikke matcher noe tittelmønster, kastes ikke. Det legges i godkjenningskøen som «ukjent type» til manuell vurdering. Tilnærmingen er bevisst konservativ: heller én falsk positiv enn én oversett frist.

### 4.4 Datouttrekk fra PDF

Rundskrivene er tekstbaserte. Fristene står typisk i en tidsplan-/kalenderseksjon, ofte som tabell («Frist for innsending av satsingsforslag 23. januar 2026») eller punktliste. Uttrekket er det vanskeligste trinnet og blir aldri feilfritt; bruk språkmodell til tolkning framfor faste regex der formuleringene varierer. Noen frister er relative i kilden («ultimo mars», «innen seks dager etter Stortingets åpning») — disse mappes til gjentaksregler, ikke harde datoer. Alt uttrekk er forslag til godkjenningskøen, aldri direkte publisering.

---

## 5. Arbeidsflyt (administrator)

### 5.1 Godkjenningskø

Felles innboks for forslag fra robot og brukere. Hvert forslag er et kort med opphavsmerke (robot/bruker + kilde/navn), fristens felter inkludert **foreslått synlighet**, og handlinger:

- **Godkjenn** → `status = godkjent`, blir synlig i visningen.
- **Juster** → åpner redigeringsskjema (5.2), deretter godkjenn.
- **Avvis** → forkastes.
- For «ukjent type»: **Vurder** (åpner skjema for manuell kategorisering) eller **Avvis**.

Foreslått synlighet fra en bruker (5.3) er et **forslag administrator kan overstyre**, ikke en endelig innstilling. `POL` foreslått av en bruker krever uansett aktiv bekreftelse fra administrator (jf. 2.4).

### 5.2 Redigeringsskjema

Samme skjema brukes for å justere innkommet forslag og for å legge inn frist manuelt. Felter: tittel, dato, budsjettår, kategori, løp, notat og **synlighetsgrupper** (2.3). Ved manuell opprettelse må synlighetsgrupper velges aktivt; synlighetsregler (2.4) kan forhåndsutfylle valget, men `POL` settes aldri automatisk. Når skjemaet åpnes fra et brukerforslag, forhåndsutfylles synligheten med brukerens forslag (administrator justerer ved behov). Opphavsmerke vises når forslaget kom fra bruker, med navn hentet fra innlogget identitet. Skjemaet har et valg «Gjenta neste år» som — hvis aktivert — knytter fristen til en gjentaksregel (3.3), slik at også brukerinnsendte, godkjente frister kan bli del av malen.

### 5.3 Brukerforslag

En bidragsyter (2.2) kan sende inn forslag til en frist via et enklere skjema enn administratorens. Feltene er tittel, dato, budsjettår, kategori, notat og **foreslått synlighet** (hvilke grupper bidragsyteren mener fristen bør være synlig for). Navn settes automatisk fra innlogget identitet. Forslaget får `status = forslag` og `opphav = bruker`, og legges i samme godkjenningskø (5.1). Foreslått synlighet bæres med forslaget og vises for administrator, som beslutter endelig synlighet ved godkjenning. Forslaget er usynlig for andre inntil det godkjennes.

### 5.4 Generér neste år

Velg målbudsjettår. Systemet leser malen, beregner datoer fra hver gjentaksregel, og legger alle som `status = forslag` i godkjenningskøen. **Synligheten videreføres fra fjorårets tilsvarende frist** som standard, slik at administrator slipper å sette grupper på nytt (kan justeres). Frister med uendret regel markeres som kurante; valgårssensitive frister (kap. 6) flagges for ekstra kontroll. Administrator gjennomgår og godkjenner på vanlig måte.

---

## 6. Valgårslogikk

Høstløpet (særlig regjeringskonferansen i august og eventuelt tillegg til Gul bok) påvirkes av valg. Norsk valgsyklus er fast og kan beregnes uten oppslag:

- **Stortingsvalg**: hvert fjerde år — 2025, 2029, 2033 … (september). Påvirker høstløpet sterkt (ny regjering kan endre framdrift, ofte tilleggsproposisjon).
- **Kommune-/fylkestingsvalg**: midt imellom — 2027, 2031, 2035 … (september). Endrer ikke regjeringen; svakere påvirkning.

Funksjon: `valgtype(aar) → stortingsvalg | kommunevalg | ingen`.

Ved generering (5.4): for milepæler med `valgaarssensitiv = true` skal systemet ved stortingsvalgår **ikke** gjette en presis dato, men flagge fristen og be administrator sette dato manuelt når den er kjent (datoen avhenger av regjeringsdannelsen). Ved kommunevalgår gis en mildere påminnelse. Banner øverst i generér-visningen varsler om valgår før enkeltfrister vurderes.

---

## 7. Visning (brukerflate)

Felles datagrunnlag og felles filter (kategori + budsjettår), tre presentasjoner brukeren kan bytte mellom:

1. **Tidslinje/liste** — kronologisk, «hva kommer nå». Hovedvisning.
2. **Kalender** — månedsvisning.
3. **Årshjul (syklusvisning)** — grafisk oversikt over ett budsjettløp i sin helhet (strekker seg ~18 mnd fra teknisk justering i januar t-1 til statsregnskap i t+1). Brukes til å studere prosessen for ett budsjett.

Fargekoding: **budsjettår** (slik at overlappende løp skilles på en kronologisk tidslinje). Kategorifilter styrer hva som vises. Standard budsjettårsfilter: vis aktive løp (de 2–3 pågående), eldre år kan hukes på for tilbakeblikk.

Visningen viser kun frister brukeren har rett til å se etter `synlig_for` (2.3). Filtreringen gjøres på server før data sendes til klienten — kategorifilteret over er et brukervalg på toppen av dette, ikke en sikkerhetsmekanisme. Administrator har en egen indikator per frist som viser hvilke grupper den er synlig for.

---

## 8. Utskrift til Word (FINs notatmal)

Administrator kan eksportere frister til et Word-dokument i FINs notatmal, for eksempel som underlag til et møte eller til politisk ledelse.

Funksjonen tar to valg: **rolle/gruppe** og **periode** (datointervall, f.eks. et budsjettår eller et halvår). Utvalget er de fristene den valgte gruppen faktisk har tilgang til, altså frister der `synlig_for` inneholder gruppens `kode`, innenfor perioden. Det gjør at «skriv ut for `POL`» gir nøyaktig det settet politisk ledelse selv ville sett, og «skriv ut for `FAG`» utelater FIN-interne frister. Administrator kan i tillegg velge å skrive ut «alt» (sitt eget fulle innsyn) når dokumentet er til intern bruk.

Dokumentet bygges på FINs notatmal (samme mal som dette dokumentets Word-variant): logo, «Notat»-merke og sidetall. Innhold: en tittel som angir gruppe og periode, og fristene listet kronologisk, gruppert på budsjettår, med dato, tittel, kategori og eventuelt notat. Genereringen skjer i backend (der tilgang og data allerede er kjent), og filen lastes ned av administrator.

Avgrensning: utskrift er i første omgang en administratorfunksjon. Om vanlige brukere senere skal kunne skrive ut sitt eget innsyn, gjenbrukes samme mekanisme med innlogget brukers grupper som utvalg.

---

## 9. Teknisk oppsett (Azure, Entra ID, GitHub)

### 9.1 Arkitektur

Webapp med tre lag: frontend (visning), backend-API (forretningslogikk, autorisering, datafiltrering), database (frister, mal, brukere, behandlede dokumenter), samt en bakgrunnsjobb (innhenting + klassifisering).

**Kritisk: all autorisering og synlighetsfiltrering skjer i backend-API-et, aldri i frontend.** Frontend mottar kun data brukeren har rett til å se. Dette er grunnen til at en ren statisk hosting (se 9.4) ikke er tilstrekkelig alene.

### 9.2 Drift på Azure (infrastruktur som kode med Bicep)

All Azure-infrastruktur beskrives som kode i **Bicep** og legges i repoet (f.eks. `/infra`), slik at miljøet kan gjenskapes og endres sporbart. Bicep-malene bør dekke:

- Hosting for backend-API og frontend (f.eks. Azure App Service eller Container Apps).
- Database (f.eks. Azure Database for PostgreSQL eller Azure SQL).
- Bakgrunnsjobb/scheduler for periodisk innhenting (f.eks. en cron-utløst container-jobb eller Azure Functions timer).
- Entra ID-appregistrering og rollekonfigurasjon (kan refereres/parametriseres fra Bicep; selve appregistreringen kan også settes opp separat og refereres).
- Hemmeligheter i Azure Key Vault (ingen hemmeligheter i repo eller i Bicep-parametre i klartekst).
- Logging/overvåking (Application Insights e.l.).

Utrulling skjer via GitHub Actions som validerer og deployer Bicep (what-if → deploy) mot Azure.

### 9.3 Autentisering og autorisering (Entra ID)

- Pålogging skjer med departementenes SSO via **Entra ID** (OpenID Connect). Backend validerer token på hver forespørsel.
- **Identitet og navn** hentes fra token-claims; brukeren taster aldri inn navn manuelt (jf. `foreslaatt_av`).
- **Funksjonsrolle** (administrator/bidragsyter/leser) styres i appen (2.2). Administrator settes av en annen administrator; første administrator seedes ved idriftsetting.
- **Synlighetsgrupper** (2.3) utledes fra Entra-attributter (f.eks. departement/avdeling, eller tildelte Entra-grupper) via konfigurerbar mapping, supplert med eksplisitt tildeling der det trengs (særlig `POL`).
- Tilgang for andre departementer enn FIN forutsetter at løsningens Entra-appregistrering er **multi-tenant** eller på annen måte åpnet for gjestebrukere/øvrige departementers kontoer. Dette er et forhold som må avklares med IT (se kap. 10), siden det berører tenant-oppsett på tvers av departementene.

### 9.4 GitHub: Codespaces og Pages

- **Codespaces** brukes som utviklingsmiljø og er uproblematisk: et skybasert dev-miljø der Claude Code og verktøy kjører. Påvirker ikke produksjonsdrift.
- **Pages** server kun statiske filer og kan **ikke** håndheve tilgang eller skjule data; alt som sendes dit kan leses av enhver innlogget. Pages er derfor **ikke egnet** til å vise frister med gruppebasert synlighet, fordi sensitive (FIN-interne) frister ikke kan beskyttes der. Pages kan på sin høyde brukes til en offentlig/innloggingsfri landingsside eller en demo med kun åpne testdata. **Den faktiske applikasjonen med tilgangsstyrte data må serveres fra backend på Azure (9.1–9.3).** Hvis ett enkelt hostingledd ønskes, la Azure servere både frontend og API.

### 9.5 Bakgrunnsjobb

Periodisk kjøring (daglig) for oppdagelse; deduplisering mot behandlede dokumenter (3.4) før forslag lages; uttrekk og klassifisering ved nye dokumenter; resultat til godkjenningskø. Bygg kildeleddet bak `Kilde`-grensesnittet fra start (4.1), selv om bare regjeringen.no implementeres nå.

---

## 10. Brukerhistorier

Skrevet på formen «som rolle vil jeg … slik at …». Akseptkriterier i kursiv. «Synlig for» viser hvilke synlighetsgrupper (2.3) historien gjelder.

### 10.1 Administrator (FIN)

- Som administrator vil jeg kunne **gi andre FIN-ansatte administratortilgang** inne i appen, slik at flere kan forvalte fristene. *Kun FIN-ansatte kan velges; handlingen logges.*
- Som administrator vil jeg **se alle frister uavhengig av synlighetsgruppe**, slik at jeg har full oversikt. *Hver frist viser hvilke grupper den er synlig for.*
- Som administrator vil jeg **godkjenne, justere, avvise og legge inn frister**, og ved opprettelse **velge synlighetsgrupper**, slik at riktig publikum ser riktig frist. *`POL` må alltid velges eksplisitt; synlighetsregler kan forhåndsutfylle resten.*
- Som administrator vil jeg at **et rundskriv jeg har behandlet (godkjent eller avvist) ikke dukker opp som forslag igjen**, slik at jeg slipper dubletter. *Dokumentet registreres som behandlet (3.4) og foreslås ikke på nytt.*
- Som administrator vil jeg at **«generér neste år» viderefører fjorårets synlighet**, slik at jeg ikke setter grupper på nytt hvert år. *Synlighet kan likevel justeres per frist.*
- Som administrator vil jeg kunne **opprette nye synlighetsgrupper** (f.eks. «Skatteøkonomisk avdeling»), slik at tilgangen kan speile organisasjonen. *Ny gruppe blir umiddelbart tilgjengelig i synlighetsvalg og utskrift uten kodeendring.*
- Som administrator vil jeg kunne **skrive ut frister til Word i FINs notatmal** ved å velge gruppe og periode, slik at jeg får et delbart underlag. *Utvalget følger den valgte gruppens tilgang; «alt» kan velges for internt bruk.*

### 10.2 Saksbehandler FA (Finansavdelingen)

- Som saksbehandler i FA vil jeg **se frister merket for `FA`**, slik at jeg følger Finansavdelingens arbeid. *Ser frister der `synlig_for` inneholder `FA`.*
- Som FA-saksbehandler vil jeg **sende inn forslag** til frister og **foreslå hvilke grupper fristen bør være synlig for**, slik at egne oppgaver kommer inn med riktig publikum antydet. *Forslag går til godkjenningskøen med mitt navn fra innlogging; foreslått synlighet vises for administrator, som beslutter endelig; blir synlig først etter godkjenning.*

### 10.3 Saksbehandler FIN, annen avdeling

- Som saksbehandler i en annen FIN-avdeling vil jeg **se frister merket for `FIN-FAG`**, slik at jeg ser det som er relevant for min avdeling. *Ser frister der `synlig_for` inneholder `FIN-FAG`.*
- Som FIN-ansatt utenfor FA vil jeg kunne **sende inn forslag**, slik at min avdelings frister fanges. *Som 9.2.*

### 10.4 Saksbehandler i fagdepartement (ikke FIN)

- Som ansatt i et fagdepartement vil jeg **se frister merket for `FAG`**, slik at jeg vet hva mitt departement må levere. *Ser frister der `synlig_for` inneholder `FAG`; ser ikke FINs interne frister.*
- Som fagdepartementsansatt har jeg **kun lesetilgang**. *Kan ikke sende forslag og kan ikke bli administrator.*

### 10.5 Politisk ledelse

- Som politiker vil jeg **se frister merket for `POL`**, slik at jeg har oversikt over de fristene som angår politisk ledelse. *Ser frister der `synlig_for` inneholder `POL`; `POL` settes alltid eksplisitt av administrator.*
- Politisk ledelse har **kun lesetilgang**.

### 10.6 Overlappende synlighet (gjelder alle)

- Som administrator vil jeg kunne merke en frist synlig for **vilkårlige kombinasjoner** av grupper (f.eks. `FA` + `POL`, eller `FA` + `FAG` uten `POL`), slik at riktig undergruppe ser den. *Synlighet er et sett av grupper, ikke et nivåtall; en bruker ser fristen hvis én av brukerens grupper er i settet.*

---

## 11. Anbefalt byggerekkefølge

1. **Fase 1 — fundament**: datamodell (kap. 3), Entra ID-innlogging og funksjonsroller (2.2), synlighetsgrupper med server-side filtrering og administrasjon av egendefinerte grupper (2.3, 3.6), manuell innlegging med synlighetsvalg, de tre visningene, kategorifilter og fargekoding. Grunnleggende Bicep for hosting + database. Gir verdi med ekte tilgangsstyring.
2. **Fase 2 — automatikk og samhandling**: `RegjeringenKilde` med oppdagelse, deduplisering mot behandlede dokumenter (3.4), totrinns filtrering, datouttrekk, godkjenningskø. Brukerforslag med foreslått synlighet (5.3) inn i samme kø. Utskrift til Word (kap. 8). Bakgrunnsjobb i Azure.
3. **Fase 3 — mal og generering**: gjentaksregler, «generér neste år» med videreført synlighet, synlighetsregler (2.4), valgårslogikk.

---

## 12. Åpne forhold før bygging

- **IT-/sikkerhetsgodkjenning** av en skyløsning på Azure som henter og lagrer (offentlige) rundskriv og deles på tvers av departementene. Rundskrivene er offentlige, men en delt løsning i departementskontekst krever normalt godkjenning uansett. Avklares før kode skrives.
- **Tverrdepartemental Entra-tilgang.** At ansatte i øvrige departementer skal kunne logge inn, forutsetter at appregistreringen i Entra ID er åpnet for dette (multi-tenant eller gjestebruker-oppsett). Tenant-oppsett på tvers av departementene må avklares med IT, og avgjør om `FAG`-brukere i praksis kan autentiseres.
- **Mapping fra Entra-attributter til synlighetsgrupper** (departement/avdeling → `FA`/`FIN-FAG`/`FAG`/`POL`) må defineres konkret sammen med IT, siden den avhenger av hvilke attributter/grupper som faktisk finnes i katalogen. `POL` settes trolig eksplisitt.
- **Behandling av sensitive frister.** Siden enkelte frister kun er for FIN-interne øyne, må det bekreftes at lagring og filtrering på Azure tilfredsstiller departementets krav til de aktuelle dataene.
- Endelig presisjonsgrad på datouttrekk (4.4) avhenger av valgt språkmodell/verktøy.
- Behandling av kommunevalgår: egen mild variant eller likt med stortingsvalg (kap. 6) — kan besluttes i fase 3.
