# Arkitektur og kommandoer

Stabil teknisk referanse for prosjektet «Årshjul for budsjettfrister»: stack, mappestruktur og kommandoer. Endres sjelden. Designbeslutningene bak datamodellen står i `beslutningslogg.md`; brukerflate og roller i `../../BRUKERHISTORIER.md`; full systemarkitektur i `../../SYSTEMARKITEKTUR.md`.

> STATUS: Fase 1 (fundament) ferdig (PR #1 merget). **Fase 2 — de offline-delbare stegene er kodet (PR #5): Steg A (kildeabstraksjon), D (totrinns filtrering), F (godkjenningskø), G (juster fra køen), H (brukerforslag), I (endringsforslag), J (varsel), K (Word-utskrift), pluss `IDatouttrekk`-forberedelse. 94 tester grønt på .NET 10.** Gjenstående Fase 2 (Steg B oppdagelse, C dedup/versjonsmatching, E live datouttrekk, L bakgrunnsjobb) er **blokkert** til `www.regjeringen.no` åpnes i miljøets egress-allowlist. Fase 3 (mal/generering) er ferdig planlagt (`fase3-plan.md`) og er neste kodetrinn — dens avhengigheter (Steg F/G) er nå på plass. Bekreftet stack: **.NET 10 (LTS) / ASP.NET Core + EF Core, Azure SQL, Blazor Web App (Interactive Server), Azure App Service**; bakgrunnsjobb = `BackgroundService` i web-hosten; datouttrekk bak `IDatouttrekk` (default Claude API, lokasjon IT-avklart). Se beslutningsloggen for full fremdrift og åpne forhold.

## Teknologistabel

Rammene er gitt av kravdokumentet (kap. 9) og ligger fast: drift på Azure, infrastruktur som kode i Bicep, innlogging med Entra ID (OpenID Connect), og et skarpt skille mellom frontend, backend-API, database og bakgrunnsjobb. Det konkrete språk-/rammeverksvalget er åpent og bør tas av hensyn til hvem som skal vedlikeholde løsningen internt.

- **Backend-API** (BEKREFTET 2026-06-18): **.NET (LTS) / ASP.NET Core (C#)**, med Entity Framework Core som dataadgang. Samme kjøretid dekker både API og den periodiske bakgrunnsjobben, slik at autorisering og synlighetsfiltrering ligger ett sted. All tilgangskontroll skjer her, aldri i frontend.
- **Frontend** (BEKREFTET 2026-06-18): **Blazor Web App med render mode Interactive Server**, servert fra samme ASP.NET Core-host som API-et. UI rendres på server slik at data en bruker ikke har rett til aldri sendes til klienten. GitHub Pages er uegnet til den faktiske appen fordi den ikke kan håndheve tilgang; brukes på sin høyde til en åpen landingsside.
- **Database** (BEKREFTET 2026-06-18): **Azure SQL**, aksessert via EF Core. `frist.synlig_for` (liste av gruppekoder, se SYSTEMARKITEKTUR.md kap. 3) modelleres relasjonelt med en koblingstabell (frist ↔ gruppekode), slik at server-side synlighetsfiltrering blir en indeksert join.
- **Bakgrunnsjobb** (BEKREFTET 2026-06-19): .NET `BackgroundService` i web-hosten (delt forretnings- og autoriseringslag), daglig timer-utløst, pluss admin-utløst «sjekk nå». Egen Azure Functions/container-jobb vurderes kun hvis jobben senere må skaleres uavhengig.
- **Datouttrekk** (BEKREFTET form 2026-06-19, lokasjon IT-avklart): abstraheres bak et `IDatouttrekk`-grensesnitt (samme prinsipp som `IKilde`) — tar PDF-tekst inn, leverer strukturert per-felt-resultat (tolket verdi, kildespenn, konfidens) ut (SYSTEMARKITEKTUR.md 3.2). Default Claude API i utvikling/test; **endelig provider/lokasjon (ekstern Claude API vs. Azure-vertet) er et IT-styringsspørsmål** (kravdok. kap. 12) som byttes uten ombygging. Uttrekksfeil: retry til forsøksgrensen, så flagg admin (aldri stille). For API-detaljer, se https://docs.claude.com/en/api/overview.
- **Rammevalg v1** (BEKREFTET 2026-06-19): første brukbare versjon målretter **FIN først** (FA + FIN-FAG + POL eksplisitt) i FINs tenant; FAG/øvrige departementer kobles på når IT har åpnet multi-tenant Entra. Styrer bl.a. synlighetsregelens default (FIN-internt uten FAG).
- **Hosting** (BEKREFTET 2026-06-18): **Azure App Service** (Linux, `DOTNETCORE|10.0`), beskrevet i `infra/main.bicep`.
- **Identitet**: Entra ID. Backend validerer token på hver forespørsel; navn/identitet hentes fra token-claims.
- **Hemmeligheter**: Azure Key Vault. Ingen hemmeligheter i repo eller i Bicep-parametre i klartekst.
- **Logging/overvåking**: Application Insights e.l.

## Utviklingsmiljø

- **GitHub Codespaces / Claude Code-web** som skybasert dev-miljø. Påvirker ikke produksjonsdrift.
- **Claude Code** brukes i utviklingen. Kontekstfilene (`CLAUDE.md` + `.claude/rules/`) leses ved oppstart av hver økt; se den daglige rytmen i CLAUDE.md.

### Miljøoppsett (VIKTIG — kjør ved start av ny økt hvis verktøyene mangler)

Det ferske/ephemeral dev-miljøet har normalt **ikke** .NET-SDK installert. `builds.dotnet.microsoft.com` (install-skriptet) er blokkert av nettverkspolicyen, men `packages.microsoft.com` og `api.nuget.org` er tilgjengelige. Installer .NET 10 SDK via Microsofts apt-feed:

```bash
curl -sSL -o /tmp/ms-prod.deb https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
sudo dpkg -i /tmp/ms-prod.deb && sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

- EF-verktøy (for migrasjoner): `dotnet tool install --global dotnet-ef --version 10.0.9` (legg `~/.dotnet/tools` på PATH).
- Bicep CLI (for validering): last ned `bicep-linux-x64` fra GitHub-release (github.com er tilgjengelig), `chmod +x`.
- Sett `DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1` for renere output.

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

Solution: `backend/Aarshjul.slnx` (.slnx — det nye XML-formatet i .NET 10).

- Bygg: `dotnet build backend/Aarshjul.slnx`
- Test (alle): `dotnet test backend/Aarshjul.slnx` — 23 tester (synlighet, datoberegning, innlegging, claims, HTTP-integrasjon).
- Kjør appen lokalt: `dotnet run --project backend/Aarshjul.Web` (krever DB + Entra-konfig; se appsettings).
- EF-migrasjon (ny): `dotnet ef migrations add <Navn> --project backend/Aarshjul.Infrastructure --startup-project backend/Aarshjul.Infrastructure` (Infrastructure har en `AppDbContextFactory` for design-time).
- EF-migrasjon (oppdater db): `dotnet ef database update --project backend/Aarshjul.Infrastructure --startup-project backend/Aarshjul.Infrastructure`
- Valider Bicep: `bicep build infra/main.bicep`
- Infra (what-if): `az deployment group what-if -g <rg> -f infra/main.bicep -p navnPrefiks=<x> miljo=dev sqlAdAdminLogin=<grp> sqlAdAdminObjectId=<oid>`
- Infra/app-deploy: GitHub Actions `deploy.yml` (manuell dispatch, OIDC).

Tester bruker SQLite in-memory (EF) og `WebApplicationFactory<Program>`; web-appen kjører i miljø `Testing` der SqlServer-DbContext, Entra-auth og claims-transformasjon erstattes/utelates (se `Aarshjul.Tests/TestApplikasjon.cs`).

## Datamodell

- Full spesifikasjon i `../../SYSTEMARKITEKTUR.md` kap. 3, og kravdokumentet @../../kravdokument-aarshjul-frister_v2.md kap. 3.
- Sentrale tabeller: frist, forslag (inkl. endringsforslag), årsmal/gjentaksregel, behandlet dokument, bruker, synlighetsgruppe, varsel.
- Fem utvidelser utover kravdokumentet (alle loggført 2026-06-18): datopresisjon + avledet sorteringsdag, `avvist`-status, uttrekksbevis per felt på robotforslag, endringsforslag med `endrer_frist_id`, og varselbegrepet.

## Fase 1 — implementert (hvor ting bor, og hva Fase 2 gjenbruker)

Nøkkelkomponenter (alle under `backend/`):
- **Synlighet (sikkerhetskritisk):** `Aarshjul.Application/Synlighet/Synlighetsfilter.cs` — ett punkt all synlighet går gjennom (`FiltrerSynlige`). `ISynlighetskontekst` + `Synlighetskontekst` (`FraPrincipal`, `ForGruppe`). Admin = `SerAlt`; tom gruppe feiler lukket.
- **Lese frister:** `IFristlesing`/`Infrastructure/Frister/FristTjeneste.cs` — `HentSynligeAsync`, `HentLandingsutvalgAsync` (union 30 dager / neste 5), `HentTilgjengeligeBudsjettaarAsync`. Bruker norsk dato via `Domain/Datohjelp.cs`.
- **Skrive frister:** `IFristskriving`/`FristskrivingTjeneste.cs` — admin manuell opprett/rediger med synlighetsvalidering (POL kun ved aktivt valg). **Fase 2: godkjenning av et forslag bør publisere via samme mønster.**
- **Grupper:** `IGruppetjeneste`/`Gruppetjeneste.cs`.
- **Auth:** `Infrastructure/Brukere/BrukeroppslagTjeneste.cs` (Entra-claims→`Bruker`, konfig-mapping `EntraGruppeOpsjoner`, POL/admin aldri automatisk), `Web/Sikkerhet/BrukerClaimsTransformation.cs` (beriker principal med rolle/grupper fra DB og **stripper forfalskede** rolle-/gruppeclaims), `Autorisasjon.cs` (policyer `ErAdministrator`, `KanForeslå`), `HttpSynlighetskontekst` (API) + `Synlighetskontekstkilde` (Blazor-krets fra AuthenticationState).
- **Visning:** `Web/Komponenter/Visningsside.cs` (base for de tre visningene), `Web/Visning/Visningstilstand.cs` (felles filter), `Visningformat.cs`, `Fristkort`/`Filterpanel`, sider i `Components/Pages` (Home/Kalender/Aarshjulvisning + `Admin/Frister`,`RedigerFrist`,`Innsyn`). API: `Web/Api/FristEndepunkter.cs` (`/api/frister`, `/landing`).
- **Datamodell:** alle entiteter i `Aarshjul.Domain` (også de som først brukes i Fase 2/3). `AppDbContext` beregner `Sorteringsdag` ved lagring. `Startdata.cs` seeder de fire standardgruppene + første admin (fra `Startdata`-konfig). Init-migrasjon finnes.
- **Konfig (`appsettings.json`):** `ConnectionStrings:Aarshjul`, `AzureAd`, `EntraGrupper` (attributt→gruppekode), `Startdata:FoersteAdminId/Navn`.

For Fase 2 finnes allerede tabellene **Forslag (+ UttrekksBevis), BehandletDokument, Gjentaksregel, Varsel** i modellen. Kildeabstraksjon legges i `backend/kilder`, bakgrunnsjobb i `backend/jobb`. Godkjenningskøen gjenbruker `Synlighetsfilter` for admin-innsyn. Driftsherding før produksjon er listet i beslutningsloggen («Åpne spørsmål»).

## Faseinndeling

Byggingen følger kravdokumentets tre faser (kap. 11) og utviklingsplanen. Fase 1: fundament (datamodell, Entra, server-side synlighet, manuell innlegging, de tre visningene, grunnleggende Bicep). Fase 2: automatikk (kilde, oppdagelse, dedup, uttrekk, godkjenningskø, brukerforslag, Word-utskrift) — detaljert byggeplan i `../../fase2-plan.md`. Fase 3: mal og generering (gjentaksregler, generér neste år, synlighetsregler, valgårslogikk) — detaljert byggeplan i `../../fase3-plan.md`. Planleggingen av både fase 2 og fase 3 er ferdig (begge planer foreligger); kodingen gjenstår og tas fase for fase.
