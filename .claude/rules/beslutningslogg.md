# Beslutningslogg

Kronologisk. Nyeste øverst. Hver oppføring: dato, beslutning, begrunnelse, konsekvens.
Dette er prosjektets hukommelse mellom økter. Les hele ved start av hver økt.

## Status nå
- Aktiv fase: **Fase 1 (fundament) AVSLUTTET 2026-06-18 og PR #1 MERGET til main.** Fase 1-koden ligger nå på `main`. **Fase 2-planlegging AVSLUTTET** (`fase2-plan.md` i rot, forankret i fase 1-koden — alle designvalg lukket, kun FINs notatmal-fil og de fire IT-forholdene krever ekstern avklaring; PR #2). **Fase 3-planlegging AVSLUTTET 2026-06-19** (`fase3-plan.md` i rot, forankret i fase 1-koden — mal/generering, valgårslogikk, synlighetsregler). **All planlegging for fase 2 og 3 er nå ferdig før koding; verken fase 2 eller fase 3 er kodet ennå.** Fase 3-koding forutsetter at fase 2 (særlig godkjenningskøens publisering) er kodet først.
- Sist fullført: Fase 1 steg 1–8 + kodegjennomgang av PR #1 med rettinger. Fem bekreftede funn rettet: (1) EF identitetskonflikt ved redigering som beholder en gruppe, (2) budsjettårsfilter som kollapset, (3) sikkerhet — claims-transformasjonen stripper nå forfalskede rolle-/gruppeclaims, (4) async void rev kretsen ved lastefeil, (5) «i dag» beregnes i norsk tid, ikke UTC. 23/23 tester grønt; Bicep kompilerer.
- Neste steg (Fase 2 — START HER i ny økt):
  1. Les `CLAUDE.md` + denne loggen + `arkitektur.md` (særlig «Miljøoppsett» — .NET 10 SDK må installeres i ferskt miljø — og «Fase 1 — implementert» for hvor koden bor).
  2. Bekreft at `dotnet test backend/Aarshjul.slnx` er grønt før ny kode.
  3. Fase 2-plan foreligger i `fase2-plan.md` (i rot) — forankret i fase 1-koden, med byggesteg, modelltillegg og åpne spørsmål. Få den godkjent før kode (regelen om plan-før-kode gjelder).
  4. Fase 2-leveranser: `Kilde`-grensesnitt (`oppdag()`/`hent()`) + `RegjeringenKilde` i `backend/kilder`; oppdagelse + dedup mot `BehandletDokument`; totrinns filtrering (nummerserie + tittelmønster); datouttrekk (språkmodell, per-felt-bevis); **godkjenningskø** (gjenbruk `Synlighetsfilter`; publisering av godkjent forslag via samme mønster som `FristskrivingTjeneste`); brukerforslag (bidragsyter) + `Varsel`; Word-utskrift per gruppe/periode; bakgrunnsjobb i `backend/jobb` (form avklares). Tabellene Forslag/UttrekksBevis/BehandletDokument/Gjentaksregel/Varsel finnes allerede i modellen.
  5. Ekte ende-til-ende-verifisering av Fase 1 (mot reell Azure SQL/Entra-tenant) er en utrullingsoppgave, ikke en kodeoppgave.
- Åpne spørsmål: De fire IT-forholdene i kravdokumentets kap. 12. Konkret Entra attributt→gruppe-mapping (mekanismen er konfigurerbar via EntraGrupper-seksjonen; verdier avklares med IT). Lokal/CI-kjøring krever .NET 10 SDK (installeres i miljøet) og en database for integrasjon mot ekte SQL.
- Driftsherding før produksjon (identifisert i gjennomgangen, utsatt til utrulling): (a) databasemigrering bør kjøres som eget deploy-steg, ikke ved app-oppstart (unngå crash-loop ved utilgjengelig/pauset DB og race ved skalering); (b) SQL-brannmurens «Allow Azure services» (0.0.0.0) er bred for FIN-interne data — vurder strammere nettverksisolering; (c) web-appens managed identity må gis DB-bruker via T-SQL (dokumentert i infra/README); (d) `HttpSynlighetskontekst` er global ISynlighetskontekst-binding — fungerer for dagens kall, men komponenter må bruke Synlighetskontekstkilde i kretsen.

## Beslutninger

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
