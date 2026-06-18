# Arkitektur og kommandoer

Stabil teknisk referanse for prosjektet «Årshjul for budsjettfrister»: stack, mappestruktur og kommandoer. Endres sjelden. Designbeslutningene bak datamodellen står i `beslutningslogg.md`; brukerflate og roller i `BRUKERHISTORIER.md`; full systemarkitektur i `SYSTEMARKITEKTUR.md`.

> STATUS: UTKAST. Stacken under er en **anbefaling** som skal bekreftes i første økt av fase 1 før kode skrives. Punkter merket «(bekreftes)» er ikke endelig valgt. Når et valg er tatt, oppdater dette dokumentet og loggfør valget med begrunnelse i `beslutningslogg.md`.

## Teknologistabel

Rammene er gitt av kravdokumentet (kap. 9) og ligger fast: drift på Azure, infrastruktur som kode i Bicep, innlogging med Entra ID (OpenID Connect), og et skarpt skille mellom frontend, backend-API, database og bakgrunnsjobb. Det konkrete språk-/rammeverksvalget er åpent og bør tas av hensyn til hvem som skal vedlikeholde løsningen internt.

- **Backend-API** (bekreftes): ett rammeverk som dekker både API og den periodiske bakgrunnsjobben, slik at autorisering og synlighetsfiltrering ligger ett sted. All tilgangskontroll skjer her, aldri i frontend.
- **Frontend** (bekreftes): serveres fra samme Azure-ledd som API-et. GitHub Pages er uegnet til den faktiske appen fordi den ikke kan håndheve tilgang; brukes på sin høyde til en åpen landingsside.
- **Database** (bekreftes): Azure Database for PostgreSQL eller Azure SQL. `frist.synlig_for` lagres som en liste av gruppekoder (se SYSTEMARKITEKTUR.md kap. 3).
- **Bakgrunnsjobb** (bekreftes): cron-utløst container-jobb eller timer-basert funksjon for periodisk oppdagelse.
- **Datouttrekk** (bekreftes): språkmodell brukes til tolkning av frister i rundskriv der formuleringene varierer (kravdokumentet 4.4). Modell/leverandør og presisjonsgrad besluttes senere; uttrekket må uansett levere strukturert per-felt-resultat med tolket verdi, kildespenn og konfidens (SYSTEMARKITEKTUR.md 3.2). For oppdaterte API-detaljer, se https://docs.claude.com/en/api/overview.
- **Hosting** (bekreftes): Azure App Service eller Container Apps.
- **Identitet**: Entra ID. Backend validerer token på hver forespørsel; navn/identitet hentes fra token-claims.
- **Hemmeligheter**: Azure Key Vault. Ingen hemmeligheter i repo eller i Bicep-parametre i klartekst.
- **Logging/overvåking**: Application Insights e.l.

## Utviklingsmiljø

- **GitHub Codespaces** som skybasert dev-miljø. Påvirker ikke produksjonsdrift.
- **Claude Code** brukes i utviklingen. Kontekstfilene (`CLAUDE.md` + `.claude/rules/`) leses ved oppstart av hver økt; se den daglige rytmen i CLAUDE.md.

## Mappestruktur

(Fylles ut når stacken er valgt. Foreslått utgangspunkt — juster til valgt rammeverk:)

- `/infra` — Bicep-maler for all Azure-infrastruktur.
- `/backend` — API, forretningslogikk, autorisering, synlighetsfiltrering.
- `/backend/kilder` — `Kilde`-grensesnittet og `RegjeringenKilde` (DFØ senere bak samme grensesnitt).
- `/backend/jobb` — bakgrunnsjobb: oppdagelse, dedup, uttrekk, klassifisering.
- `/frontend` — de tre visningene, landingsflate, godkjenningskø, administratorflater.
- `/.github/workflows` — GitHub Actions (Bicep what-if → deploy).
- `/.claude/rules` — kontekstfiler: `arkitektur.md`, `beslutningslogg.md`.

## Kommandoer (kjør disse, ikke gjett)

(Fylles ut med faktiske kommandoer når stacken er valgt, så agenten kjører rett verktøy framfor å gjette.)

- Installer: (f.eks. `npm install`)
- Kjør lokalt: (f.eks. `npm run dev`)
- Test: (f.eks. `npm test`)
- Bygg: (f.eks. `npm run build`)
- Infra (what-if): (f.eks. `az deployment group what-if ...`)
- Infra (deploy): (via GitHub Actions; lokal kommando ved behov)

## Datamodell

- Full spesifikasjon i `SYSTEMARKITEKTUR.md` kap. 3, og kravdokumentet @kravdokument-aarshjul-frister_v2.md kap. 3.
- Sentrale tabeller: frist, forslag (inkl. endringsforslag), årsmal/gjentaksregel, behandlet dokument, bruker, synlighetsgruppe, varsel.
- Fem utvidelser utover kravdokumentet (alle loggført 2026-06-18): datopresisjon + avledet sorteringsdag, `avvist`-status, uttrekksbevis per felt på robotforslag, endringsforslag med `endrer_frist_id`, og varselbegrepet.

## Faseinndeling

Byggingen følger kravdokumentets tre faser (kap. 11) og utviklingsplanen. Fase 1: fundament (datamodell, Entra, server-side synlighet, manuell innlegging, de tre visningene, grunnleggende Bicep). Fase 2: automatikk (kilde, oppdagelse, dedup, uttrekk, godkjenningskø, brukerforslag, Word-utskrift). Fase 3: mal og generering (gjentaksregler, generér neste år, synlighetsregler, valgårslogikk).
