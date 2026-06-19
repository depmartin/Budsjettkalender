# Fase 3 — Mal og generering: detaljert plan

Detaljert byggeplan for fase 3, lagt fram før kode skrives (jf. utviklingsplanen
Del C og kravdokumentets kap. 11). Planen er forankret i den faktiske fase 1-koden
(PR #1, merget til main): .NET 10-solution `backend/Aarshjul.slnx` lagdelt i
Domain/Application/Infrastructure/Web/Tests, Blazor Web App (Interactive Server) +
minimal-API, EF Core mot Azure SQL.

Se @.claude/rules/arkitektur.md for stack, miljøoppsett, kommandoer og kart over hvor
fase 1-koden bor, og @.claude/rules/beslutningslogg.md for fremdrift og åpne forhold.
Fase 2-planen ligger i @fase2-plan.md.

Kravgrunnlag: @kravdokument-aarshjul-frister_v2.md kap. 3.3, 5.4, 6;
@SYSTEMARKITEKTUR.md kap. 3.1, 3.3, 7, 8; @BRUKERHISTORIER.md 2.4, 4.5.

> MERK OM REKKEFØLGE: Fase 3 planlegges nå fordi all planlegging skal være ferdig før
> koding starter. **Fase 3-koding forutsetter at Fase 2 er kodet** — særlig
> godkjenningskøens publiseringshandling (fase2-plan Steg F) og redigeringsskjemaet
> (Steg G). Selve datamodellen og publiseringsmønsteret finnes allerede fra Fase 1.

---

## 1. Mål og avgrensning

Fase 3 gjør årshjulet flerårig: et kommende budsjettår genereres fra en **mal av
gjentaksregler** framfor å legges inn manuelt. Fasen leverer de fire tingene
kravdokumentet og utviklingsplanen beskriver:

1. **Gjentaksreglene tas i bruk** med de tre regeltypene `FastDato`, `RelativUkedag`,
   `RelativTilMilepael`.
2. **«Generér neste år»**: les malen, beregn konkrete datoer fra hver regel, og legg
   alle som `Forslag` (`ForslagType = Generert`) på en egen gjennomgangsflate adskilt
   fra den løpende køen. Synligheten videreføres fra fjorårets tilsvarende frist.
3. **Synlighetsregler** som forhåndsutfyller foreslått synlighet — `POL` settes aldri
   automatisk.
4. **Valgårslogikk** med `valgtype(aar)`. I stortingsvalgår flagges valgårssensitive
   frister og settes tentativt framfor å gjette en presis dato.

**Bærende prinsipp som ikke svekkes:** ingenting publiseres uten godkjenning. Genererte
frister er forslag som passerer en administrators gjennomgang før de blir frister.

---

## 2. Utgangspunkt i koden (det fase 3 gjenbruker)

Fase 1 la fundamentet fase 3 bygger på:

- **`Gjentaksregel`** (`backend/Aarshjul.Domain/Gjentaksregel.cs`): `Id`, `Loep`,
  `Kategori`, `Regeltype`, `Regelparametre` (JSON-streng), `Valgaarssensitiv`. Allerede
  registrert i `AppDbContext` (`DbSet<Gjentaksregel> Gjentaksregler`,
  `backend/Aarshjul.Infrastructure/AppDbContext.cs`).
- **`Regeltype`-enum** (`backend/Aarshjul.Domain/Enums.cs`): `FastDato`, `RelativUkedag`,
  `RelativTilMilepael`.
- **`Frist`** (`backend/Aarshjul.Domain/Frist.cs`): har `GjentaRegelId` (kobling
  frist→regel), `Loep`, `Budsjettaar`, `Datopresisjon`, `Datokvalifikator`,
  `Sorteringsdag` og `OppdaterSorteringsdag()`.
- **`Datoberegning.Sorteringsdag(...)`** (`backend/Aarshjul.Domain/Datoberegning.cs`)
  og **`Datohjelp.Idag(...)`** (norsk tid, `backend/Aarshjul.Domain/Datohjelp.cs`) —
  gjenbrukes for sorteringsdag og «i dag».
- **`Forslag` + `ForslagType.Generert`** (`backend/Aarshjul.Domain/Forslag.cs`):
  genererte frister blir `Forslag` med `ForslagType = Generert`, fristfeltene,
  `ForeslaattSynlighet` (JSON-liste av gruppekoder) og valgfri `GjentaRegelId`-kobling.
- **`FristskrivingTjeneste`/`IFristskriving`**
  (`backend/Aarshjul.Infrastructure/Frister/FristskrivingTjeneste.cs`,
  `backend/Aarshjul.Application/Frister/IFristskriving.cs`): publiseringsmønster med
  synlighetsvalidering (`ValiderAsync`; minst én aktiv gruppe; POL kun ved aktivt valg).
  Godkjenning av et generert forslag publiserer via samme mønster.
- **`Synlighetsfilter`/`Synlighetskontekst`**
  (`backend/Aarshjul.Application/Synlighet/`): gjenbrukes for admin-innsyn i de
  genererte forslagene.
- **Auth-policyer** `ErAdministrator` (`backend/Aarshjul.Web/Sikkerhet/Autorisasjon.cs`):
  generering og malforvaltning er administratorhandlinger.
- **`Startdata.cs`** (`backend/Aarshjul.Infrastructure/`): seeder i dag standardgrupper
  + første admin — utvides med seeding av standard gjentaksregler.

---

## 3. Modelltillegg fase 3 trenger

Datamodellen er i hovedsak komplett. To vurderinger:

- **Typede regelparametre.** Legg parameter-modeller (DTO-er) i Application-laget som
  (de)serialiseres mot `Gjentaksregel.Regelparametre` (JSON), framfor å endre skjema:
  - `FastDato`: `{ "maaned": 7, "dag": 20 }`
  - `RelativUkedag`: `{ "maaned": 3, "uke": 2, "ukedag": "man" }`
  - `RelativTilMilepael`: `{ "anker_loep": "fremleggelse", "offset_dager": -7 }`
  **Ingen ny kolonne / EF-migrasjon nødvendig for dette.**
- **Synlighetsregler.** Anbefaling: start **konfig-drevet** (en enkel regel «genererte/
  rundskrivfrister synlige for alle aktive grupper unntatt POL» i `appsettings`), og
  løft til egen tabell kun ved behov. Hvis tabell velges, navngis migrasjonen
  `Fase3Generering`. Besluttes i Steg E.

---

## 4. Byggesteg

Hvert steg fullføres og verifiseres (`dotnet test backend/Aarshjul.slnx` grønt) før
neste. Stegene er ordnet etter avhengighet.

### Steg A — Typede regelparametre + datoberegning fra regler
Parameter-modeller for hver `Regeltype` (kap. 3). Utvid `Datoberegning` (Domain) med
beregning per regeltype:
- `FastDato`: gitt måned/dag i målåret.
- `RelativUkedag`: n-te forekomst av en ukedag i en måned.
- `RelativTilMilepael`: anker-løpets beregnede dato + `offset_dager`.

**Virkedagjustering (lukket):** en frist som lander på lørdag/søndag flyttes til
nærmeste virkedag, men **aldri over et årsskifte** — justeringen holdes innenfor samme
kalenderår. **Helligdager holdes utenfor i første omgang** (kun helg) — åpent punkt.
Sorteringsdag settes via eksisterende `Datoberegning.Sorteringsdag(...)` /
`Frist.OppdaterSorteringsdag()`.

### Steg B — Valgårslogikk
Ren domenefunksjon `Valgaar.Valgtype(int aar) → Stortingsvalg | Kommunevalg | Ingen`:
stortingsvalg 2025/2029/2033…, kommunevalg 2027/2031/2035…, ellers ingen. Fullt
testbar uten oppslag. **Kommunevalg-variantens styrke besluttes under koding**
(mild påminnelse vs. likt med stortingsvalg).

### Steg C — Genereringstjeneste (Application/Infrastructure)
`IGenereringstjeneste` med input målbudsjettår; leser `Gjentaksregler`; beregner dato
per regel og lager `Forslag` (`ForslagType = Generert`, `GjentaRegelId` satt,
`Status = Forslag`). Beregningen løser `RelativTilMilepael` i **avhengighetsrekkefølge**
(anker-løp beregnes før de som peker på det).

- **Valgårssensitive frister i stortingsvalgår:** ikke gjett presis dato — lag tentativt
  forslag (`Datopresisjon = Maaned`) og sett valgårsflagg per frist.
- **Anker-kjeder og sirkularitet (lukket).** `RelativTilMilepael` kan kjede seg over
  flere ledd. Genereringen løser kjeden topologisk og **oppdager sirkulære eller
  uoppløselige kjeder** (manglende anker-løp) og **feiler tydelig med varsel til
  administrator** framfor å produsere en vilkårlig dato. Utvei ved uoppløselig kjede:
  administrator setter ankeret (typisk budsjettframleggelsen) manuelt.
- **Tentativitet arves nedover kjeden (lukket).** En frist som henger på et tentativt
  anker blir **selv tentativ** — den arver ankerets *presisjon* (`Datopresisjon`/
  `Datokvalifikator`), ikke bare ankerets dato. Tentativitet er dermed en egenskap ved
  beregningen langs kjeden, ikke bare ved en `Valgaarssensitiv`-regel: en frist kan bli
  tentativ uten selv å være valgårssensitiv. **Låst (designintervju 2026-06-19):** dette
  skrives inn som en **eksplisitt beregningsregel** allerede i Steg C, kodet og testet fra
  start.
- **Genereringsflyt ved manuelt anker — låst til to-trinns (designintervju 2026-06-19).**
  Flyten er ikke ren ett-knapps-generering når et anker må settes manuelt (valgårssensitivt
  framleggings-anker): **administrator setter de nødvendige ankrene først, deretter beregnes
  resten** fra de satte ankrene. Påvirker Steg F-flaten direkte (to trinn, ikke én knapp).

### Steg D — Videreføring av synlighet
For hvert generert forslag slås fjorårets tilsvarende frist opp via `Loep` +
`(målbudsjettår − 1)`, og dens `synlig_for` kopieres til `Forslag.ForeslaattSynlighet`
som standard.

- **Synlighet videreføres kun fra fjorårsfrister med `Status = Godkjent`.** For alle
  øvrige tilfeller — ingen fjorårsfrist, eller en fjorårsfrist som var avvist/ikke
  godkjent — får forslaget **tom synlighet**, og administrator må velge selv (faller
  tilbake på synlighetsregelen fra Steg E der den finnes).
- **POL videreføres aldri stilltiende.** Selv om fjorårsfristen hadde POL, behandles POL
  som i Fase 2: det vises som foreslått, men krever administrators **aktive
  bekreftelse** ved godkjenning. Dette oppfyller prinsippet «POL aldri automatisk».

### Steg E — Synlighetsregler
Mekanisme som forhåndsutfyller `ForeslaattSynlighet` for forslag (gjelder også robot-/
brukerforslag fra Fase 2). **Default-regelen er FIN-internt: FA + FIN-FAG** (designintervju
2026-06-19) — **ikke** FAG og **aldri** POL automatisk. Admin legger til FAG aktivt når en
frist faktisk angår fagdepartementene. Konsistent med v1 FIN-først (FAG kan uansett ikke
autentiseres før IT åpner multi-tenant). Gjelder kun auto/robot-/genererte forslag —
manuell oppretting krever fortsatt aktivt synlighetsvalg. Gjenbrukes av genereringen når
fjorårsfrist mangler (Steg D). Lagringsform (konfig vs. tabell) besluttes her; anbefaling
konfig-drevet (kap. 3).

### Steg F — Generér-flate (Web, admin)
Egen side **adskilt fra godkjenningskøen** (jf. beslutningslogg 2026-06-18 «egen
gjennomgangsflate med fjorårssammenligning»). Krever `ErAdministrator`.
- Velg målbudsjettår; **valgårsflagg per frist (intet banner)**.
- Per frist: ny beregnet dato ved siden av **fjorårets faktiske dato**, videreført
  synlighet, tentativ-merking, og evt. anker-feil fra Steg C.
- Handlinger: godkjenn / juster (gjenbruk `Admin/RedigerFrist`) / avvis. **Godkjenn**
  publiserer `Forslag → Frist` via `FristskrivingTjeneste`-mønsteret (synlighetsvalidering,
  POL kun ved aktivt valg).
- **Frist uten konkret dag krever aktiv «er du sikker?»-bekreftelse** (eksisterende
  beslutning om tentative frister).

### Steg G — Forvalt årsmalen (Web, admin) + seeding
Enkel CRUD-flate for `Gjentaksregler` (opprett/rediger/deaktiver). Seed standard
gjentaksregler for de syv kjente løpene (kravdok. 4.3: marskonferanse, rammefordeling,
rnb, nysaldering, gulbok, statsregnskap, rapportering) i `Startdata.cs`. «Gjenta neste
år»-valget fra Fase 2 (Steg G i fase2-plan) knytter en godkjent frist til en
`Gjentaksregel`, slik at også manuelt/brukerinnsendte frister kan bli del av malen.

---

## 5. Avhengigheter til Fase 2
- **Publisering ved godkjenning** (fase2-plan Steg F) og **redigeringsskjemaet**
  (Steg G) må være kodet før Steg F/G her kan fullføres.
- **Varsel** (fase2-plan Steg J) gjenbrukes for å varsle administrator om uoppløselig
  anker-kjede / parse-feil under generering (Steg C).
Datamodell og publiseringsmønster finnes allerede fra Fase 1.

---

## 6. Det avgjørende kvalitetskravet
- **Ingenting publiseres uten godkjenning** — alle genererte frister er
  `Forslag(Generert)` som passerer gjennomgangsflaten.
- **POL settes aldri automatisk** — verken videreføring (Steg D) eller synlighetsregel
  (Steg E) kan produsere POL.
- **Synlighet verifiseres på spørringssvaret**, ikke bare i UI (gjenbruk
  `Synlighetsfilter`).

---

## 7. Verifikasjon før fase 3 regnes som fullført
xUnit mot SQLite in-memory + `WebApplicationFactory<Program>`, som fase 1 etablerte:
1. Datoberegning per regeltype gir korrekt dato; en frist som lander på helg justeres til
   nærmeste virkedag uten å krysse årsskifte.
2. En `RelativTilMilepael`-frist flytter seg når ankeret flyttes.
3. En flerleddet anker-kjede løses i riktig rekkefølge; en sirkulær/uoppløselig kjede
   feiler tydelig og varsler administrator framfor å gi en vilkårlig dato.
4. Tentativitet arves: en ikke-valgårssensitiv frist som henger på et tentativt anker
   blir selv tentativ (arver presisjon).
5. `Valgtype(aar)` gir rett verdi for stortingsvalg / kommunevalg / ingen.
6. Generering produserer `Forslag(Generert)`; fjorårets synlighet videreføres **kun fra
   godkjente** fjorårsfrister og **uten** POL satt automatisk.
7. Stortingsvalgår: valgårssensitive frister flagges og får tentativ (måneds-)dato, ikke
   en gjettet dag.
8. Godkjenning av et generert forslag publiserer en `Frist` via skrivetjenesten med
   synlighetsvalidering (POL kun ved aktivt valg).

---

## 8. Avklarte designvalg (lukket i planleggingen)
- **Anker-kjeder:** topologisk løsning; sirkulær/uoppløselig kjede feiler tydelig med
  varsel til administrator; manuelt satt anker er utvei.
- **Tentativitet:** arves nedover anker-kjeden (arver ankerets presisjon, ikke bare
  dato); en frist kan bli tentativ uten selv å være valgårssensitiv.
- **Fjorårsoppslag for synlighet:** kun fra fjorårsfrister med `Status = Godkjent`;
  ellers tom synlighet. POL aldri stilltiende.
- **Virkedagjustering:** nærmeste virkedag ved helg, men aldri over årsskifte.
- **Regelparametre:** typede DTO-er mot eksisterende JSON-felt — ingen skjemaendring.
- **Genereringsflyt ved manuelt anker (designintervju 2026-06-19):** **to-trinns** — admin
  setter nødvendige valgårssensitive ankre først, deretter beregnes resten.
- **Tentativ-arv (designintervju 2026-06-19):** kodes som **eksplisitt beregningsregel** i
  Steg C fra start (arver ankerets presisjon, ikke bare dato).
- **Synlighetsregel-default (designintervju 2026-06-19):** FIN-internt (FA + FIN-FAG),
  ikke FAG, aldri POL automatisk; gjelder kun auto/robot-/genererte forslag.

## 9. Gjenstående åpne punkter (besluttes ved koding / loggføres da)
- **Kommunevalg-variantens styrke:** mild påminnelse vs. likt med stortingsvalg.
- **Helligdager:** holdes utenfor i første omgang (kun helg) — utvides ved behov.
- **Synlighetsregel-lagring:** konfig vs. egen tabell.

## 10. Til beslutningsloggen ved fasens slutt
Hvordan regel→dato ble implementert; virkedag-/helligdagshåndtering; hvordan anker-kjede
og sirkularitet håndteres i praksis; den valgte genereringsflyten ved manuelt anker; om
synlighetsregler ble konfig- eller tabelldrevet; og den endelige kommunevalg-varianten.
