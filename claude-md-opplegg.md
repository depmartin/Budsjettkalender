# Opplegg for CLAUDE.md og kontekstbevaring mellom byggeøkter

Dette opplegget er laget for prosjektet «Årshjul for budsjettfrister». Formålet er at Claude Code skal huske arkitektur og beslutninger mellom økter, slik at hver ny økt starter der den forrige slapp i stedet for å gjette på nytt.

Pakken består av tre filer du legger i prosjektmappen:

1. `CLAUDE.md` i rotmappen — den lille, stabile indeksen som lastes hver økt.
2. `.claude/rules/beslutningslogg.md` — den voksende hukommelsen: arkitektur og beslutninger.
3. `.claude/rules/arkitektur.md` — stabil teknisk referanse (stack, mappestruktur, kommandoer).

Prinsippet bak: Claude Code starter hver økt med tomt kontekstvindu. Det som overlever, er det som står skrevet i disse filene på disk — ikke det dere ble enige om i en tidligere samtale. Derfor er regelen: **enhver beslutning som betyr noe utover den aktuelle oppgaven, skal skrives inn i en av disse filene før økten avsluttes.**

---

## Fil 1 — `CLAUDE.md` (rot)

Hold denne under ~80 linjer. Den skal være indeks og faste regler, ikke et oppslagsverk. Kopier blokken under inn i `CLAUDE.md`:

```markdown
# Prosjekt: Årshjul for budsjettfrister

Delt fristoversikt for arbeidet med statsbudsjettet (Seksjon for statsbudsjett og regnskap, FIN).
Full kravspesifikasjon: @kravdokument-aarshjul-frister.md

## Hva dette er
- Webapp som samler budsjettfrister, henter dem fra FINs rundskriv på regjeringen.no, suppleres manuelt.
- Flerårig årshjul: tidligere år kan studeres, kommende år genereres fra mal.
- To roller: administrator (endrer, godkjenner, genererer) og bruker (leser, kan foreslå).

## Bærende prinsipper (ENDRES IKKE uten å oppdatere beslutningsloggen)
- Frister identifiseres på FUNKSJON, aldri på rundskrivnummer (nummer kan skifte mellom år).
- Automatisk uttrekk går ALDRI rett til publisering — alt passerer godkjenningskø.
- Skarpt rolleskille: ingen brukerhandling endrer data direkte.
- Kilde er et utbyttbart ledd (regjeringen.no nå, DFØ senere) bak ett felles grensesnitt.

## Referanser
- Arkitektur, stack, kommandoer: @.claude/rules/arkitektur.md
- Beslutninger og hvorfor (LES DENNE FØRST hver økt): @.claude/rules/beslutningslogg.md

## Arbeidsmåte
- Bygg i faser slik kravdokumentet beskriver. Fullfør og verifiser én fase før neste.
- YOU MUST legge fram en plan før du skriver kode i en ny fase, og vente på godkjenning.
- IMPORTANT: Ved slutten av hver økt, oppdater beslutningsloggen med nye beslutninger, valgt
  fremgangsmåte og åpne spørsmål. Skriv det FØR økten blir lang.
- Når en beslutning endrer noe i «Bærende prinsipper», oppdater begge steder.
```

To detaljer å merke seg. `@filnavn` importerer filen automatisk inn i konteksten ved oppstart — første gang godkjenner du importene i en dialog. Og `YOU MUST` / `IMPORTANT` brukes bevisst kun på de få reglene som er kritiske; brukt overalt mister de effekt.

---

## Fil 2 — `.claude/rules/beslutningslogg.md` (hukommelsen)

Dette er kjernen i kontekstbevaring. Den er en kronologisk logg der hver oppføring fanger én beslutning: hva, hvorfor, og konsekvens. Start med rammen under; Claude Code (og du) føyer til nederst etter hvert.

```markdown
# Beslutningslogg

Kronologisk. Nyeste øverst. Hver oppføring: dato, beslutning, begrunnelse, konsekvens.
Dette er prosjektets hukommelse mellom økter. Les hele ved start av hver økt.

## Status nå
- Aktiv fase: (fyll inn, f.eks. «Fase 1 — datamodell og visning»)
- Sist fullført: (fyll inn)
- Neste steg: (fyll inn)
- Åpne spørsmål: (fyll inn)

## Beslutninger

### [ÅÅÅÅ-MM-DD] Eksempel: valg av datolagring i malen
- Beslutning: Årsmalen lagrer regler, ikke datoer.
- Begrunnelse: Gjør «generér neste år» mulig; frister settes omtrent likt hvert år.
- Konsekvens: Tre regeltyper (fast_dato, relativ_ukedag, relativ_til_milepael).
```

Hvorfor en egen logg framfor å la auto-memory ta det: auto-memory finnes og hjelper, men den er maskinlokal og styres ikke fullt av deg. En beslutningslogg i prosjektet er eksplisitt, leses ved hver oppstart via importen i CLAUDE.md, og overlever kontekstkomprimering fordi den ligger på disk. Det gir deg forutsigbar hukommelse du selv eier.

---

## Fil 3 — `.claude/rules/arkitektur.md` (stabil referanse)

Dette er «substantivene»: hvor ting er og hvordan prosjektet kjøres. Den fylles ut når stacken er valgt (fase 1), og endres sjelden. Skjelett:

```markdown
# Arkitektur og kommandoer

## Teknologistabel
- (fylles inn når valgt — f.eks. frontend, backend, database)

## Mappestruktur
- (fylles inn — hvor bor hva)

## Kommandoer (kjør disse, ikke gjett)
- Installer: (f.eks. npm install)
- Kjør lokalt: (f.eks. npm run dev)
- Test: (f.eks. npm test)
- Bygg: (f.eks. npm run build)

## Datamodell
- Se full spesifikasjon i @kravdokument-aarshjul-frister.md kap. 3
- Sentrale tabeller: frist, gjentaksregel/mal
```

Dokumentasjonen er tydelig på at de faktiske kommandoene bør stå skrevet, slik at agenten kjører dem i stedet for å gjette feil verktøy og kaste bort omganger på det.

---

## Den daglige rytmen

Tre vaner gjør at hukommelsen faktisk holder mellom økter.

Ved start av økt: be Claude Code lese beslutningsloggen og oppsummere status og neste steg før noe annet. Da bekrefter du at den er «på sporet» før arbeidet begynner.

Underveis: når dere lander en beslutning som vil gjelde videre (et teknologivalg, en navnekonvensjon, en tolkning av et krav), be agenten skrive den inn i beslutningsloggen med en gang. Ikke vent til slutten; lange økter kan komprimeres, og da forsvinner det som bare ble sagt i samtalen.

Ved slutt av økt: be agenten oppdatere «Status nå» i beslutningsloggen — aktiv fase, sist fullført, neste steg, åpne spørsmål. Dette er den ene rutinen som mest pålitelig lar neste økt starte der denne slapp.

## To fallgruver å unngå

Ikke fyll CLAUDE.md med personlighet («vær grundig», «tenk som en senior utvikler»). Slikt endrer ikke oppførsel målbart og drukner de reglene som faktisk betyr noe. Hold filen til konkrete, etterprøvbare fakta og regler.

Ikke la CLAUDE.md vokse ut av kontroll. Når den nærmer seg ~150 linjer, flytt detaljer ut i `.claude/rules/`-filer og la CLAUDE.md være indeksen som peker dit. Lange filer reduserer hvor godt instruksjonene følges.
```
```
