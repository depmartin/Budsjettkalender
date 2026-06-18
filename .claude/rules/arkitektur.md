# Arkitektur og kommandoer

Stabil teknisk referanse for prosjektet «Årshjul for budsjettfrister»: stack, mappestruktur og kommandoer. Endres sjelden. Designbeslutningene bak datamodellen står i `beslutningslogg.md`; brukerflate og roller i `../../BRUKERHISTORIER.md`; full systemarkitektur i `../../SYSTEMARKITEKTUR.md`.

> STATUS: Backend og database er bekreftet (2026-06-18, se beslutningsloggen). Punkter merket «(bekreftes)» er fortsatt anbefalinger, ikke endelig valgt. Når et valg tas, oppdater dette dokumentet og loggfør valget med begrunnelse i `beslutningslogg.md`.

## Teknologistabel

Rammene er gitt av kravdokumentet (kap. 9) og ligger fast: drift på Azure, infrastruktur som kode i Bicep, innlogging med Entra ID (OpenID Connect), og et skarpt skille mellom frontend, backend-API, database og bakgrunnsjobb. Det konkrete språk-/rammeverksvalget er åpent og bør tas av hensyn til hvem som skal vedlikeholde løsningen internt.

- **Backend-API** (BEKREFTET 2026-06-18): **.NET (LTS) / ASP.NET Core (C#)**, med Entity Framework Core som dataadgang. Samme kjøretid dekker både API og den periodiske bakgrunnsjobben, slik at autorisering og synlighetsfiltrering ligger ett sted. All tilgangskontroll skjer her, aldri i frontend.
- **Frontend** (BEKREFTET 2026-06-18): **Blazor Web App med render mode Interactive Server**, servert fra samme ASP.NET Core-host som API-et. UI rendres på server slik at data en bruker ikke har rett til aldri sendes til klienten. GitHub Pages er uegnet til den faktiske appen fordi den ikke kan håndheve tilgang; brukes på sin høyde til en åpen landingsside.
- **Database** (BEKREFTET 2026-06-18): **Azure SQL**, aksessert via EF Core. `frist.synlig_for` (liste av gruppekoder, se SYSTEMARKITEKTUR.md kap. 3) modelleres relasjonelt med en koblingstabell (frist ↔ gruppekode), slik at server-side synlighetsfiltrering blir en indeksert join.
- **Bakgrunnsjobb** (bekreftes): foretrukket som en .NET `BackgroundService`/Worker i samme løsning som API-et (delt forretnings- og autoriseringslag), timer-utløst for periodisk oppdagelse. Alternativ: separat timer-utløst Azure Functions/container-jobb.
- **Datouttrekk** (bekreftes): språkmodell brukes til tolkning av frister i rundskriv der formuleringene varierer (kravdokumentet 4.4). Modell/leverandør og presisjonsgrad besluttes senere; uttrekket må uansett levere strukturert per-felt-resultat med tolket verdi, kildespenn og konfidens (SYSTEMARKITEKTUR.md 3.2). For oppdaterte API-detaljer, se https://docs.claude.com/en/api/overview.
- **Hosting** (bekreftes): Azure App Service eller Container Apps.
- **Identitet**: Entra ID. Backend validerer token på hver forespørsel; navn/identitet hentes fra token-claims.
- **Hemmeligheter**: Azure Key Vault. Ingen hemmeligheter i repo eller i Bicep-parametre i klartekst.
- **Logging/overvåking**: Application Insights e.l.

## Utviklingsmiljø

- **GitHub Codespaces** som skybasert dev-miljø. Påvirker ikke produksjonsdrift.
- **Claude Code** brukes i utviklingen. Kontekstfilene (`CLAUDE.md` + `.claude/rules/`) leses ved oppstart av hver økt; se den daglige rytmen i CLAUDE.md.

## Mappestruktur

- `/backend` — .NET-solution `Aarshjul.slnx` med lagdelte prosjekter:
  - `Aarshjul.Domain` — entiteter, enums, domenelogikk (datopresisjon/sorteringsdag).
  - `Aarshjul.Application` — applikasjonstjenester, DTO-er, synlighetsfilter, brukerabstraksjoner.
  - `Aarshjul.Infrastructure` — `AppDbContext`, EF-konfig, migrasjoner, seeding, tjenester.
  - `Aarshjul.Web` — ASP.NET Core-host: Blazor-komponenter (visninger, adminflater), minimal-API, Entra-auth. Selve app-UI bor her (Interactive Server).
  - `Aarshjul.Tests` — xUnit (synlighet, datoberegning, HTTP-integrasjon mot SQLite).
  - `/backend/kilder`, `/backend/jobb` — plassholdere for Fase 2 (Kilde + bakgrunnsjobb).
- `/frontend` — plassholder for en eventuell åpen landingsside (app-UI bor i web-hosten).
- `/infra` — Bicep-maler for all Azure-infrastruktur.
- `/.github/workflows` — GitHub Actions (`ci.yml`; senere Bicep what-if → deploy).
- `/.claude/rules` — kontekstfiler: `arkitektur.md`, `beslutningslogg.md`.

## Kommandoer (kjør disse, ikke gjett)

(.NET-kommandoer. Endelige prosjekt-/sti-navn settes når solution-oppsettet lages i fase 1.)

- Gjenopprett pakker: `dotnet restore`
- Bygg: `dotnet build`
- Kjør lokalt: `dotnet run --project backend`
- Test: `dotnet test`
- EF-migrasjon (ny): `dotnet ef migrations add <Navn>`
- EF-migrasjon (oppdater db): `dotnet ef database update`
- Infra (what-if): `az deployment group what-if -g <rg> -f infra/main.bicep`
- Infra (deploy): via GitHub Actions (`.github/workflows`); lokal `az deployment group create` ved behov

## Datamodell

- Full spesifikasjon i `../../SYSTEMARKITEKTUR.md` kap. 3, og kravdokumentet @../../kravdokument-aarshjul-frister_v2.md kap. 3.
- Sentrale tabeller: frist, forslag (inkl. endringsforslag), årsmal/gjentaksregel, behandlet dokument, bruker, synlighetsgruppe, varsel.
- Fem utvidelser utover kravdokumentet (alle loggført 2026-06-18): datopresisjon + avledet sorteringsdag, `avvist`-status, uttrekksbevis per felt på robotforslag, endringsforslag med `endrer_frist_id`, og varselbegrepet.

## Faseinndeling

Byggingen følger kravdokumentets tre faser (kap. 11) og utviklingsplanen. Fase 1: fundament (datamodell, Entra, server-side synlighet, manuell innlegging, de tre visningene, grunnleggende Bicep). Fase 2: automatikk (kilde, oppdagelse, dedup, uttrekk, godkjenningskø, brukerforslag, Word-utskrift). Fase 3: mal og generering (gjentaksregler, generér neste år, synlighetsregler, valgårslogikk).
