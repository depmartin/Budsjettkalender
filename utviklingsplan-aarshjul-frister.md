# Utviklingsplan: Årshjul for budsjettfrister

Plan for prosjektutvikling og påfølgende programmering av en delt fristoversikt for arbeidet med statsbudsjettet. Eier: Seksjon for statsbudsjett og regnskap (SBR), Finansavdelingen, Finansdepartementet. Planen bygger på kravdokumentet (@kravdokument-aarshjul-frister_v2.md) og legger til grunn kontekstopplegget for Claude Code beskrevet i opplegget for CLAUDE.md.

Planen har to deler. Del A etablerer prosjektrammen og kontekstapparatet før kode skrives. Del B er selve byggeplanen, faset etter kravdokumentets anbefalte rekkefølge, med konkrete leveranser, verifikasjon og hva som skal skrives til prosjektets hukommelse underveis.

---

## Del A — Prosjektramme og kontekstoppsett

### A.1 Hva som skal være på plass før første kodeøkt

Kravdokumentet peker på fire forhold som må avklares før bygging (kap. 12). Disse er styrende for at planen i det hele tatt kan gjennomføres, og bør håndteres som forutsetninger, ikke som oppgaver underveis:

- IT- og sikkerhetsgodkjenning av en skyløsning på Azure som henter og lagrer rundskriv og deles på tvers av departementene.
- Tverrdepartemental Entra-tilgang, det vil si om appregistreringen åpnes for øvrige departementers kontoer (multi-tenant eller gjestebruker). Dette avgjør om `FAG`-brukere faktisk kan autentiseres.
- Konkret mapping fra Entra-attributter til synlighetsgrupper, definert sammen med IT.
- Bekreftelse på at lagring og filtrering på Azure tilfredsstiller departementets krav til FIN-interne frister.

To av disse — tverrdepartemental tilgang og endelig attributtmapping — kan løpe parallelt med fase 1, fordi fase 1 kan bygges og demonstreres mot FINs egen tenant alene. De øvrige to bør være avklart før produksjonsdata legges inn. Planen legger til grunn at fase 1 starter mot FIN-intern tilgang, og at tverrdepartemental innlogging aktiveres når Entra-oppsettet er klart.

### A.2 Kontekstapparatet (CLAUDE.md-opplegget)

Prosjektet bygges med Claude Code over mange økter. Hver økt starter med tomt kontekstvindu, og det som overlever mellom økter er det som står skrevet på disk. Før første kodeøkt opprettes derfor tre filer i repoet, etter opplegget for CLAUDE.md:

1. `CLAUDE.md` i rotmappen — kort, stabil indeks med bærende prinsipper og pekere. Holdes under om lag 80 linjer. Innholdet er allerede utformet i opplegget og kopieres inn som det står, med kravdokumentets filnavn som import (`@kravdokument-aarshjul-frister_v2.md`).
2. `.claude/rules/beslutningslogg.md` — prosjektets hukommelse. Kronologisk logg der hver oppføring fanger én beslutning med dato, begrunnelse og konsekvens, samt en «Status nå»-blokk øverst.
3. `.claude/rules/arkitektur.md` — stabil teknisk referanse med stack, mappestruktur og kommandoer. Fylles ut når stacken er valgt tidlig i fase 1.

Den daglige rytmen følges fra første økt: les beslutningsloggen ved start og oppsummer status, skriv inn beslutninger i det de tas, og oppdater «Status nå» ved slutten av hver økt. Denne planen henviser eksplisitt til hva som skal skrives til loggen ved avslutning av hver fase, slik at hukommelsen holdes oppdatert uten at det glemmes.

### A.3 Bærende prinsipper som ikke endres uten loggføring

Følgende prinsipper fra kravdokumentet styrer hele designet og gjentas her fordi de er rammen alle senere valg vurderes mot. Endres ett av dem, skal både `CLAUDE.md` og beslutningsloggen oppdateres:

- Frister identifiseres på funksjon, aldri på rundskrivnummer, fordi nummeret kan skifte mellom år.
- Automatisk uttrekk går aldri rett til publisering — alt passerer godkjenningskøen.
- Tilgang og synlighet håndheves på server, aldri kun i nettleseren. Data en bruker ikke har rett til å se, sendes aldri til klienten.
- Synlighet styres av grupper som kan overlappe fritt, ikke av et nivåtall.
- Et rundskriv som er behandlet før, foreslås ikke på nytt.
- Kilde er et utbyttbart ledd bak ett felles grensesnitt.

### A.4 Teknologivalg som besluttes i oppstart av fase 1

Stacken er ikke låst i kravdokumentet, men rammene er gitt: drift på Azure, infrastruktur som kode i Bicep, Entra ID for innlogging, og et skarpt skille mellom frontend, backend-API, database og bakgrunnsjobb. Følgende valg legges til grunn som anbefaling og bekreftes i første økt, hvorpå de skrives til `arkitektur.md`:

- Backend-API og bakgrunnsjobb som beskytter all autorisering og synlighetsfiltrering. Et samlet rammeverk som dekker både API og periodisk jobb er å foretrekke fremfor to ulike kjøretidsmiljøer.
- Database egnet for Azure-drift (Azure Database for PostgreSQL eller Azure SQL), med `synlig_for` som en liste av gruppekoder slik datamodellen forutsetter.
- Frontend servert fra samme Azure-ledd som API-et, slik at det unngås at tilgangsstyrte data noensinne ligger på et rent statisk ledd. GitHub Pages er uegnet til den faktiske applikasjonen og brukes på sin høyde til en åpen landingsside.
- GitHub Codespaces som utviklingsmiljø og GitHub Actions for utrulling av Bicep (what-if, deretter deploy).

Det endelige språk- og rammeverksvalget tas i samråd med dem som skal forvalte løsningen videre, slik at det kan vedlikeholdes internt. Valget loggføres med begrunnelse, fordi det er en beslutning som gjelder utover den enkelte oppgaven.

---

## Del B — Byggeplan

Byggingen følger kravdokumentets anbefalte rekkefølge i tre faser. Hver fase fullføres og verifiseres før neste påbegynnes. For hver fase legges det fram en plan før kode skrives, og planen godkjennes før bygging.

### Fase 1 — Fundament

Målet med fase 1 er en løsning som gir reell verdi med ekte tilgangsstyring, selv uten automatisk innhenting. En koordinator skal kunne legge inn frister manuelt med riktig synlighet, og saksbehandlere skal kunne se nøyaktig de fristene de har rett til.

Leveranser:

- Datamodell etter kravdokumentets kapittel 3: frist, årsmal og gjentaksregel, behandlet dokument, bruker og synlighetsgruppe. Modellen settes opp komplett tidlig, også de tabellene som først tas i bruk i senere faser, slik at senere arbeid ikke krever skjemaendringer.
- Entra ID-innlogging og de tre funksjonsrollene administrator, bidragsyter og leser. Første administrator settes opp ved idriftsetting (seed). Identitet og navn hentes fra token-claims.
- Synlighetsgrupper med server-side filtrering. De fire standardgruppene `FA`, `FIN-FAG`, `FAG` og `POL` seedes. Administrator kan opprette, gi nytt navn til og deaktivere grupper i appen, og nye grupper virker uten kodeendring fordi `frist.synlig_for` refererer til gruppekode.
- Manuell innlegging av frist med aktivt valg av synlighetsgrupper. Ingen stilltiende standard for synlighet, og `POL` velges alltid eksplisitt.
- De tre visningene: tidslinje/liste som hovedvisning, kalender og årshjul. Felles datagrunnlag og felles filter på kategori og budsjettår.
- Kategorifilter med standard `budsjett` og `gulbok` på, `regnskap` av, og fargekoding på budsjettår.
- Grunnleggende Bicep for hosting og database, og GitHub Actions for utrulling.

Det avgjørende kvalitetskravet i fase 1 er at synlighetsfiltreringen ligger i backend. En bruker uten rett til en frist skal aldri motta den i klienten. Dette verifiseres ikke bare gjennom det visningen viser, men ved å kontrollere at API-svaret i seg selv er filtrert.

Verifikasjon før fase 1 regnes som fullført:

- En administrator ser alle frister uavhengig av merking, med indikator for hvilke grupper hver frist er synlig for.
- En `FAG`-bruker mottar i API-svaret kun frister der `synlig_for` inneholder `FAG`, og ser ingen FIN-interne frister.
- En frist merket `FA` + `POL` er synlig for en `FA`-bruker og en `POL`-bruker, men ikke en `FAG`-bruker.
- En ny, administratoropprettet gruppe kan velges som synlighet uten at koden er endret.
- Manuell innlegging avviser lagring uten aktivt valgt synlighet.

Til beslutningsloggen ved fasens slutt: valgt stack og begrunnelse, hvordan synlighetsfiltreringen er implementert på server, hvordan mapping fra Entra-attributter til grupper er løst i fase 1, og åpne spørsmål om tverrdepartemental tilgang.

### Fase 2 — Automatikk og samhandling

Fase 2 kobler på automatisk innhenting fra regjeringen.no og åpner for brukerforslag, uten å svekke prinsippet om at ingenting publiseres uten godkjenning.

Leveranser:

- Kildeabstraksjonen `Kilde` med operasjonene `oppdag()` og `hent()`, og `RegjeringenKilde` som første implementasjon. Grensesnittet bygges fra start selv om bare én kilde finnes nå, slik at DFØ eller andre kan kobles på senere uten ombygging.
- Oppdagelse mot rundskrivarkivet på regjeringen.no, med deduplisering mot registeret over behandlede dokumenter før forslag lages. Et dokument som er behandlet før, foreslås ikke på nytt, uavhengig av om fristene fra det ble godkjent eller avvist. Kjent nøkkel med ny innholdshash flagges som «oppdatert versjon» fremfor å gi dublett.
- Totrinns filtrering: nummerserie som skiller årlige rundskriv fra varig regelverk, deretter tittelgjenkjenning mot kjente løpsmønstre. Et årlig rundskriv som ikke matcher noe mønster, kastes ikke, men legges i køen som «ukjent type» til manuell vurdering.
- Datouttrekk fra PDF, der språkmodell brukes til tolkning fremfor faste regler der formuleringene varierer. Relative frister i kilden mappes til gjentaksregler fremfor harde datoer. Alt uttrekk er forslag.
- Godkjenningskøen som felles innboks for forslag fra robot og brukere, med handlingene godkjenn, juster, avvis, og vurder for ukjent type.
- Brukerforslag fra bidragsytere via et enklere skjema, med foreslått synlighet som administrator kan overstyre. Navn settes fra innlogget identitet.
- Utskrift til Word i FINs notatmal, med valg av gruppe og periode. Utvalget følger den valgte gruppens faktiske tilgang, og genereringen skjer i backend der tilgang og data er kjent.
- Bakgrunnsjobb i Azure for periodisk oppdagelse.

Verifikasjon før fase 2 regnes som fullført:

- Et nytt årlig rundskriv på oversiktssiden gir forslag i køen, og samme rundskriv gir ikke nytt forslag ved neste kjøring.
- Et rundskriv med varig karakter ignoreres, og et årlig rundskriv uten kjent tittelmønster havner som «ukjent type».
- Et brukerforslag legger seg i samme kø med opphavsmerke og foreslått synlighet, og er usynlig for andre inntil det godkjennes.
- `POL` foreslått av en bruker krever aktiv bekreftelse fra administrator.
- Utskrift for `FAG` utelater FIN-interne frister, og utskrift for `POL` gir nøyaktig det settet politisk ledelse selv ville sett.

Til beslutningsloggen ved fasens slutt: hvordan dokumentnøkkel og innholdshash er utledet, hvilket verktøy som ble valgt for datouttrekk og oppnådd presisjonsgrad, og hvordan kildeabstraksjonen er formet slik at DFØ kan kobles på senere.

### Fase 3 — Mal og generering

Fase 3 gjør årshjulet flerårig ved å la kommende år genereres fra mal.

Leveranser:

- Gjentaksreglene tas i bruk med de tre regeltypene fast dato, relativ ukedag og relativ til milepæl. Frister bundet til budsjettframleggelsen flytter seg automatisk når ankeret flyttes.
- «Generér neste år» som leser malen, beregner datoer fra reglene og legger alle som forslag i køen. Synligheten videreføres fra fjorårets tilsvarende frist som standard.
- Synlighetsregler som forhåndsutfyller forslag, der `POL` aldri settes automatisk.
- Valgårslogikk med funksjonen `valgtype(aar)`. Ved stortingsvalgår flagges valgårssensitive frister for manuell datosetting fremfor gjetting, og et banner varsler om valgår i generér-visningen.

Verifikasjon før fase 3 regnes som fullført:

- Generering av et nytt budsjettår produserer forslag med korrekt beregnede datoer fra hver regeltype.
- En frist definert som relativ til budsjettframleggelsen flytter seg når ankeret endres.
- Synligheten fra fjoråret følger med genererte forslag, og `POL` er ikke satt automatisk.
- Et stortingsvalgår flagger de valgårssensitive høstfristene og viser banner.

Til beslutningsloggen ved fasens slutt: hvordan beregningen fra regler til datoer er implementert, hvordan justering til virkedag håndteres, og beslutningen om kommunevalgår behandles som egen mild variant eller likt med stortingsvalg.

---

## Del C — Arbeidsmåte gjennom hele prosjektet

Noen vaner gjelder uavhengig av fase og er det som holder kvaliteten oppe over mange økter.

Hver fase innledes med en plan som legges fram og godkjennes før kode skrives. Planen brytes ned i mindre, verifiserbare steg, og ett steg fullføres og kontrolleres før neste.

Server-side håndheving av tilgang testes som en egen ting, ikke bare gjennom det grensesnittet viser. Et API-svar som inneholder en frist brukeren ikke har rett til, er en feil selv om frontend skjuler den.

Beslutninger som gjelder utover den aktuelle oppgaven skrives til beslutningsloggen i det de tas, ikke ved øktens slutt, fordi lange økter kan komprimeres. «Status nå» oppdateres ved hver økts slutt med aktiv fase, sist fullført, neste steg og åpne spørsmål.

Kontekstfilene holdes magre. `CLAUDE.md` er en indeks, ikke et oppslagsverk, og fylles ikke med personlighet eller generelle formaninger. Når den vokser, flyttes detaljer ut i `.claude/rules/`-filene.
