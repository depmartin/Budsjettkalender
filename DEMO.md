# Prøv demoen lokalt

En kjørbar demo av Årshjul for budsjettfrister — **uten Azure SQL og uten ekte
Entra-innlogging**. Bruker SQLite og en enkel persona-velger. Autorisering og
server-side synlighetsfiltrering kjører nøyaktig som i produksjon; kun
identitetskilden og databasen er byttet. Demoen henter **ikke** PDF-er fra
regjeringen.no (venter på IT-avklaring) — i stedet kan du laste opp rundskriv
manuelt for å se hele innhentingspipelinen.

## Krav

- .NET 10 SDK. Mangler den i et ferskt miljø, se
  `.claude/rules/arkitektur.md` → «Miljøoppsett».

## Start

**Linux/macOS (bash/zsh):**
```bash
ASPNETCORE_ENVIRONMENT=Demo dotnet run --project backend/Aarshjul.Web --no-launch-profile
```

**Windows (PowerShell):**
```powershell
$env:ASPNETCORE_ENVIRONMENT="Demo"; dotnet run --project backend/Aarshjul.Web --no-launch-profile
```

`--no-launch-profile` er **nødvendig**: uten det overstyrer `launchSettings.json`
miljøvariabelen med `Development`, og appen prøver å koble til Azure SQL i stedet
for demo-databasen (feiler med «The ConnectionString property has not been
initialized»).

Appen stopper opp med `Now listening on: http://localhost:5000` — det betyr at den
kjører (ikke lukk vinduet). Åpne så `http://localhost:5000/demo` i nettleseren.
Første gang opprettes en lokal SQLite-fil (`aarshjul-demo.db`, git-ignorert) med
demodata. Vil du nullstille, stopp appen (Ctrl+C), slett filen og start på nytt.

## Logg inn

Gå til **`/demo`** og velg en persona. De fem dekker rollene og synlighets-
gruppene:

| Persona | Rolle | Ser |
|---|---|---|
| Dag Admin | administrator (FA) | alt + admin-flatene |
| Frida FA | bidragsyter (FA) | FA-frister, kan foreslå |
| Finn FIN-FAG | bidragsyter (FIN-FAG) | FIN-FAG-frister, kan foreslå |
| Frank Fagdep | leser (FAG) | kun FAG-frister |
| Pia Politiker | leser (POL) | kun POL-frister |

Bruk **«Bytt persona»** øverst for å veksle.

## Prøv dette

1. **Synlighet:** Logg inn som **Frank Fagdep (FAG)** — se at FIN-interne og
   POL-frister ikke vises. Bytt til **Dag Admin** og se at alt vises med
   gruppemerker (FA/FIN-FAG/FAG/POL) på hvert kort.
2. **De tre visningene:** Veksle mellom **Nå**, **Kalender** og **Årshjul**;
   skru kategorier (Budsjett/Gul bok/Regnskap) og budsjettår av/på.
3. **Godkjenningskøen** (som admin, **Kø**): se robotforslag med
   uttrekksbevis (tolket verdi + kildeutdrag + konfidens), brukerforslag og
   endringsforslag (før/etter). Godkjenn ett — det publiseres og dukker opp i
   visningene.
4. **Last opp rundskriv** (som admin, **Last opp**): last opp en rundskriv-PDF.
   Den kjøres gjennom pipelinen (dedup → filtrering → datouttrekk) og havner som
   forslag i køen. *Datouttrekket er en deterministisk stand-in som standard;
   sett en ekte Claude-nøkkel for å bruke ekte språkmodell (se under).*
5. **Generér neste år** (som admin, **Generér**) og **Årsmal** (**Mal**): se
   hvordan et budsjettår genereres fra gjentaksregler, med valgårsmerking.
6. **Skriv ut til Word** (som admin, **Eksporter**): last ned et .docx for en
   valgt gruppe og periode.

## Hva demoen ikke gjør

- Henter ikke fra regjeringen.no (Cloudflare/IT-avklaring). Bruk «Last opp» i
  mellomtiden.
- Bruker ikke ekte Entra eller Azure SQL.
- Datouttrekket er en stand-in med mindre du kobler på en ekte Claude-nøkkel (se under).

## Ekte datouttrekk med Claude (valgfritt)

Pipelinen kaller Claude bak `IDatouttrekk` når en API-nøkkel er satt, ellers brukes
den deterministiske stand-in-en. For å slå på ekte uttrekk i demoen, sett
miljøvariabelen før du starter appen:

```powershell
# Windows PowerShell
$env:ANTHROPIC_API_KEY="<din-nokkel>"
$env:ASPNETCORE_ENVIRONMENT="Demo"
dotnet run --project backend/Aarshjul.Web --no-launch-profile
```

```bash
# macOS/Linux
ANTHROPIC_API_KEY="<din-nokkel>" ASPNETCORE_ENVIRONMENT=Demo \
  dotnet run --project backend/Aarshjul.Web --no-launch-profile
```

Alternativt kan nøkkel/modell/URL settes under seksjonen `Datouttrekk` i appsettings
(`ApiNokkel`, `Modell`, `BasisUrl`). Resten av pipelinen er uendret — kun tolkningen
av PDF-teksten byttes fra stand-in til ekte modell. Endelig provider/lokasjon (ekstern
Claude API vs. Azure-vertet) er et IT-styringsspørsmål (kravdok. kap. 12).
