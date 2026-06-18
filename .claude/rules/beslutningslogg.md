# Beslutningslogg

Kronologisk. Nyeste øverst. Hver oppføring: dato, beslutning, begrunnelse, konsekvens.
Dette er prosjektets hukommelse mellom økter. Les hele ved start av hver økt.

## Status nå
- Aktiv fase: Fase 1 (fundament) — alle åtte byggesteg implementert. Klar for gjennomgang/verifisering i PR #1 før Fase 2.
- Sist fullført: Fase 1 steg 1–8. Solution + CI; datamodell + migrasjon + seeding; sentralt synlighetsfilter; Entra-auth + filtrert API; admin manuell innlegging m/ validering; de tre visningene m/ felles filter og fargekoding; administrator-innsyn; Bicep (App Service + Azure SQL + Key Vault + App Insights) + deploy-workflow. 20/20 tester grønt; Bicep kompilerer.
- Neste steg: Verifiser fase 1 (manuelt mot en faktisk DB/Entra-tenant), deretter planlegg Fase 2 (RegjeringenKilde, oppdagelse, dedup, datouttrekk, godkjenningskø, brukerforslag, Word-utskrift, bakgrunnsjobb). Hosting bekreftet som App Service; bakgrunnsjobb-form avklares i Fase 2.
- Åpne spørsmål: De fire IT-forholdene i kravdokumentets kap. 12. Konkret Entra attributt→gruppe-mapping (mekanismen er konfigurerbar via EntraGrupper-seksjonen; verdier avklares med IT). Lokal/CI-kjøring krever .NET 10 SDK (installeres i miljøet) og en database for integrasjon mot ekte SQL.

## Beslutninger

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
