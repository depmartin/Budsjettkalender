# Systemarkitektur: Årshjul for budsjettfrister

Dette dokumentet beskriver den tekniske arkitekturen og datamodellen for løsningen, med vekt på de avgjørelsene som ble tatt i designgjennomgangen og som utvider eller presiserer kravdokumentet (@kravdokument-aarshjul-frister_v2.md). Brukerhistoriene som arkitekturen skal støtte, står i @BRUKERHISTORIER.md.

Dokumentet er strukturert slik: først de bærende arkitekturprinsippene, deretter lagdelingen, så datamodellen med de utvidelsene designet krever, og til slutt de sentrale mekanismene (synlighet, godkjenningskø, varsler, generering).

---

## 1. Bærende prinsipper

Følgende prinsipper styrer alle senere valg og endres ikke uten at beslutningen loggføres:

- Frister identifiseres på funksjon, aldri på rundskrivnummer, fordi nummeret kan skifte mellom år.
- Automatisk uttrekk går aldri rett til publisering — alt passerer godkjenningskøen.
- All autorisering og synlighetsfiltrering skjer i backend, aldri i frontend. Frontend mottar kun data brukeren har rett til å se.
- Synlighet styres av grupper som kan overlappe fritt, ikke av et nivåtall.
- Et rundskriv som er behandlet før, foreslås ikke på nytt. Et avvist brukerforslag kan derimot gjenbrukes.
- Kilde er et utbyttbart ledd bak ett felles grensesnitt.
- Løsningen er en kalender, ikke et statusverktøy: datoen styrer livssyklusen til en frist, og det finnes ingen «forsinket»-tilstand.

---

## 2. Lagdeling

Løsningen består av fire lag, i tråd med kravdokumentets kapittel 9.

Frontend er ansvarlig for presentasjon alene. Den mottar ferdig filtrerte data og håndhever ingen tilgang. De tre visningene (tidslinje/liste, kalender, årshjul), landingsflaten, godkjenningskøen og administratorflatene lever her.

Backend-API-et er ansvarlig for forretningslogikk, autorisering og datafiltrering. Hver forespørsel validerer brukerens token og avgjør hva vedkommende har rett til å se eller gjøre. Det er her synlighetsfiltreringen ligger, og det er grunnen til at et rent statisk hostingledd ikke er tilstrekkelig.

Databasen lagrer frister, årsmal og gjentaksregler, behandlede dokumenter, brukere, synlighetsgrupper, forslag og varsler.

Bakgrunnsjobben kjører periodisk og står for oppdagelse, deduplisering, uttrekk og klassifisering fra kildene, og leverer forslag inn i godkjenningskøen.

Drift skjer på Azure, med infrastruktur beskrevet som kode i Bicep og utrulling via GitHub Actions. Innlogging skjer med departementenes SSO via Entra ID, og backend validerer token på hver forespørsel. Frontend serveres fra samme Azure-ledd som API-et, slik at tilgangsstyrte data aldri ligger på et rent statisk ledd.

---

## 3. Datamodell

Datamodellen tar utgangspunkt i kravdokumentets kapittel 3 og utvides på fem punkter som følger av designavklaringene. Utvidelsene er markert tydelig nedenfor.

### 3.1 Frist

Den publiserte enheten brukeren ser. Feltene fra kravdokumentet videreføres: `id`, `tittel`, `dato`, `budsjettaar`, `kategori`, `loep`, `kilde`, `dokument_id`, `synlig_for`, `status`, `opphav`, `foreslaatt_av`, `notat` og `gjenta_regel_id`.

**Utvidelse — datopresisjon.** Fristen får et presisjonsfelt ved siden av datoen, fordi enkelte frister bevisst er mindre presise enn en konkret dag (se 7). Presisjonsnivået er normalt `dag`. Det kan løsnes til `maaned`, unntaksvis med en kvalifikator `primo`, `medio` eller `ultimo`. For frister uten konkret dag beregnes og lagres en avledet sorteringsdag når fristen lagres (for eksempel den 15. for «medio», den 1. for «primo», den siste for «ultimo», eller den 1. for ren månedsangivelse), slik at all kronologisk plassering og sortering fortsatt har et entydig punkt å bygge på. Brukeren ser den ærlige, tentative angivelsen, mens sorteringsdagen kun brukes internt.

**Utvidelse — status.** `status` utvides med verdien `avvist` i tillegg til kravdokumentets `forslag`, `godkjent` og `fullfoert`. Et avvist forslag slettes ikke, men bevares med status `avvist`, slik at bidragsyteren kan se utfallet og eventuelt gjenbruke forslaget. `fullfoert` er i praksis lite brukt på brukerflaten, siden kalenderlogikken flytter frister til historikk av seg selv dato pluss én dag.

Synlighet (`synlig_for`) håndheves alltid på server. En frist sendes aldri til en bruker uten en matchende gruppe.

### 3.2 Forslag

Designet skiller tydeligere mellom et publisert frist-objekt og et forslag enn kravdokumentet gjør eksplisitt, fordi et forslag bærer informasjon som ikke hører hjemme på den publiserte fristen. Et forslag kan være av tre slag: et nytt-frist-forslag, et endringsforslag, eller et generert forslag.

**Felles for alle forslag:** opphav (`robot`, `bruker` eller `admin`), kilde eller innsenders identitet, de foreslåtte fristfeltene, og foreslått synlighet.

**Utvidelse — uttrekksbevis på robotforslag.** Et forslag fra den automatiske innhentingen bærer, per felt, både den tolkede verdien, tekstutdraget verdien er hentet fra, og et konfidensnivå. Dette betyr at datouttrekket (kravdokumentets 4.4) ikke kan returnere bare en ferdig dato, men må levere et strukturert per-felt-resultat med kildespenn og usikkerhetsmarkering. Det per-felt usikkerhetsflagget som tennes i grensesnittet, styres av deterministiske, verifiserbare regler (se 5), ikke av modellens egenvurdering alene; konfidensnivået er ett bidrag, aldri eneste utløser. Tekstutdraget vises alltid ved siden av den tolkede verdien i køen. Denne informasjonen hører til forslaget. Når forslaget godkjennes, er det den rene fristen som lever videre; uttrekksbeviset arkiveres med forslaget og følger ikke den publiserte fristen.

**Utvidelse — endringsforslag.** Et endringsforslag refererer en eksisterende, publisert frist via et felt `endrer_frist_id` og bærer de foreslåtte nye verdiene ved siden av de gjeldende. Den publiserte fristen endres ikke mens forslaget venter; den oppdateres først ved godkjenning. Flere endringsforslag kan referere samme frist samtidig, og de behandles uavhengig — godkjenning av det ene avviser ikke automatisk det andre. Administratorvisningen utleder et «venter endring»-merke på en frist ved å slå opp om det finnes minst ett åpent forslag med `endrer_frist_id` lik fristens id.

### 3.3 Årsmal og gjentaksregler

Uendret fra kravdokumentet. Malen lagrer regler, ikke datoer. De tre regeltypene er `fast_dato`, `relativ_ukedag` og `relativ_til_milepael`, og et felt `valgaarssensitiv` angir om regelen påvirkes av valg. Ved generering beregnes konkrete datoer fra reglene (se 8).

### 3.4 Behandlet dokument

Registeret hindrer at samme rundskriv foreslås flere ganger, med en stabil `dokument_nokkel` og en `innhold_hash`. Et dokument som er registrert, gir ikke nye forslag — uavhengig av om fristene fra det tidligere ble godkjent eller avvist. Dette gjelder rundskriv; mekanismen er bevisst atskilt fra avviste brukerforslag, som derimot kan gjenbrukes. En kjent `dokument_nokkel` med ny `innhold_hash` betyr at dokumentet er **republisert med endret innhold**: systemet re-uttrekker og lager automatisk **endringsforslag** (`Forslag` med `endrer_frist_id`) mot de berørte publiserte fristene, til administrators gjennomgang i køen — ikke et rent varsel og ikke et dublettforslag (designintervju 2026-06-19). Endringsforslagene passerer køen som alt annet; ingenting publiseres uautorisert.

**Utvidelse — forkastet-liste (designintervju 2026-06-19).** Et automatisk uttrekk som er **både** lav konfidens **og** uten gjenkjennelig dato auto-forkastes — men aldri stille: det havner i en synlig, reverserbar «forkastet»-liste administrator kan gjennomgå, hente tilbake i køen, eller slette. Sletting huskes på `(kilde, dokument_nokkel)` slik at samme kilde ikke gjenoppliver elementet; en ny kilde eller neste års dokument (ny nøkkel) kan komme inn på nytt. Dette bevarer den konservative linjen («aldri miste en frist») uten å la køen flomme over: ingenting forsvinner uten spor.

**Utvidelse — uttrekk-mellomtilstand med forsøksteller.** Et dokument er ikke ferdigbehandlet i det øyeblikket det er oppdaget: nedlasting (`hent()`) og uttrekk kan feile eller måtte gjentas. Modellen må derfor kunne uttrykke en mellomtilstand for et dokument som er oppdaget, men ennå ikke ferdig hentet/uttrukket, med en forsøksteller. Et dokument som feiler i `hent()` prøves et fast antall ganger over påfølgende kjøringer, og flagges til administrator når grensen er nådd — det forsvinner aldri stille (se 5). Om mellomtilstanden bor i selve registeret eller i en egen arbeidskø, avgjøres ved kodestart; begge oppfyller kravet.

### 3.5 Bruker

Feltene fra kravdokumentet videreføres: `id` (stabil id fra Entra), `navn`, `funksjonsrolle` (`administrator`, `bidragsyter`, `leser`), `grupper` og `er_fin`. Funksjonsrolle og synlighetsgrupper er adskilte akser. Administratorrollen settes av en annen administrator inne i løsningen, ikke via Entra-grupper.

Gruppemedlemskap utledes som standard fra Entra-attributter via en konfigurerbar mapping, men kan overstyres manuelt av administrator. Modellen må derfor kunne skille en gruppetildeling som stammer fra Entra-mapping fra en som er satt manuelt, slik at en manuell overstyring ikke uten videre overskrives ved neste innlogging.

### 3.6 Synlighetsgruppe

Uendret fra kravdokumentet. En gruppe har `id`, `kode` (stabil nøkkel det refereres til fra `frist.synlig_for`), `navn`, `aktiv` og `er_standard`. Fristens `synlig_for` lagrer en liste av gruppekoder, slik at en ny gruppe virker umiddelbart uten skjemaendring.

### 3.7 Varsel

**Utvidelse — varsel.** Designet innfører et lite varselbegrep, fordi løsningen ellers er rent pull-basert (brukeren henter data), mens bidragsyteren skal få beskjed når et forslag er behandlet. Et varsel er knyttet til en bruker-id, opprettes når administrator godkjenner eller avviser et forslag, bærer en kort tekst (og ved avvisning en eventuell begrunnelse), og har en lest/ulest-tilstand. Varselet lever kun inne i løsningen; det finnes ingen utgående kanal som e-post i denne omgang. Dette er den eneste push-mekanismen mot bruker i hele løsningen.

---

## 4. Synlighet og autorisering

Tilgang avgjøres langs to uavhengige akser. Funksjonsrolle avgjør hva en bruker kan gjøre; synlighetsgruppe avgjør hvilke frister en bruker ser. Begge håndheves i backend.

Synlighetsfiltreringen er den sikkerhetskritiske mekanismen. En leseforespørsel om frister returnerer kun frister der `synlig_for` inneholder minst én av brukerens grupper. Administrator er unntatt filteret og ser alt. Filtreringen skal verifiseres på selve API-svaret, ikke bare på det grensesnittet viser — et API-svar som inneholder en frist brukeren ikke har rett til, er en feil selv om frontend skjuler den.

Administrators innsynsfunksjoner (se @BRUKERHISTORIER.md 4.8) gjenbruker denne mekanismen. «Se som rolle» kjører den aktuelle gruppens synlighetsspørring i backend. Revisjonslisten per gruppe henter alle frister med en gitt kode i `synlig_for` og viser de øvrige gruppene ved siden av. Begge er administratorhandlinger og introduserer ingen ny sikkerhetsmekanisme.

`POL` har en særlig regel i logikken, ikke i datamodellen: ingen automatisk prosess — verken en synlighetsregel ved manuell opprettelse eller et brukerforslag — kan legge `POL` til en frists `synlig_for`. `POL` kan kun settes ved administrators aktive valg. Dette håndheves i backend ved at synlighetsregler aldri produserer `POL`, og ved at godkjenning av et brukerforslag som foreslo `POL` krever et eksplisitt felt fra administrator.

---

## 5. Kilder og innhenting

Innhenting bygges bak et `Kilde`-grensesnitt med operasjonene `oppdag()` og `hent()`, slik at flere kilder kan kobles på senere uten ombygging. Første implementasjon er `RegjeringenKilde`; resten av systemet er kildeagnostisk.

Grensesnittet uttrykker utfall, ikke bare data. `oppdag()` skiller tre tilstander: fant nye, ingen nye (men kjørte greit), og klarte ikke parse. En tom liste fra en vellykket kjøring skilles dermed fra en feilet kjøring, slik at en stille periode uten nye rundskriv aldri forveksles med en stille feil (for eksempel at oversiktssidens struktur er endret).

Oppdagelsesjobben kjører periodisk, leser oversiktssiden, og sjekker hvert dokument mot registeret over behandlede dokumenter før den lager forslag. Totrinns filtrering skiller årlige rundskriv fra varig regelverk (nummerserie), og matcher deretter tittel mot kjente løpsmønstre. Et årlig rundskriv uten kjent tittelmønster kastes ikke, men legges i køen som «ukjent type» til manuell vurdering.

Datouttrekket bruker språkmodell til tolkning der formuleringene varierer, og leverer det strukturerte per-felt-resultatet beskrevet i 3.2 — tolket verdi, kildespenn og konfidens per felt. Uttrekket ligger bak et eget grensesnitt (samme prinsipp som kildeleddet), slik at modellvalg og kjørelokasjon (ekstern Claude API vs. Azure-vertet) er et utbyttbart, IT-avklart ledd og ikke en innebygd binding (designintervju 2026-06-19). Relative frister i kilden («ultimo mars») mappes til gjentaksregler framfor harde datoer. Alt uttrekk er forslag.

Det per-felt usikkerhetsflagget styres av deterministiske, verifiserbare regler, slik at det er testbart og ikke avhenger av modellens selvtillit. Flagget tennes blant annet når en relativ formulering er tolket til en hard dato, når kildespennet ikke inneholder en gjenkjennelig dato, eller når en fornuftsregel er brutt (for eksempel en dato utenfor budsjettløpets forventede vindu). Modellens egenkonfidens inngår som ett bidrag, men er aldri eneste utløser.

**Liveness og stille feil.** Et automatisk ledd som svikter stille er verre enn ingen automatikk; administrator må kunne se at innhentingen lever og hvor den eventuelt står. «Klarte ikke parse» fra `oppdag()` varsler administrator. Administratorflaten viser «sist vellykkede innhenting», der `oppdag()` og `hent()`/uttrekk spores hver for seg, slik at det fremgår hvilket ledd som eventuelt står stille. Et dokument som gjentatte ganger feiler i `hent()` flagges til administrator når forsøksgrensen er nådd (3.4).

Kildelenken som vises på leserflaten (se @BRUKERHISTORIER.md 2.6) bygger på fristens `dokument_id` og dokumentets URL i behandlet-dokument-registeret. Lenken vises kun når den finnes, og arver fristens synlighet; måldokumentet er offentlig, så det oppstår ingen lekkasje.

---

## 6. Godkjenningskøen

Køen er den felles innboksen for alle forslag, uansett opphav. Den organiseres som selvstendige enkeltkort gruppert per kilde, og hvert kort avgjøres for seg — det finnes ingen masse-godkjenning. Administrator kan filtrere på opphav, kilde, ukjent type, kategori og forslagstype (der endringsforslag er en egen type).

Robotkort viser tolkede felter, tekstutdrag, kildelenke og per-felt usikkerhetsflagg (3.2). Brukerkort viser innsenders navn fra innlogget identitet og foreslått synlighet. Endringsforslag vises med en før/etter-fremstilling mot den berørte fristen (3.2).

Handlingene er godkjenn, juster (som åpner redigeringsskjemaet) og avvis, samt vurder for «ukjent type». Ved avvisning bevares forslaget med status `avvist`, og et varsel opprettes for innsenderen ved brukerforslag (3.7).

---

## 7. Datopresisjon og tentative frister

Fordi enkelte frister ikke har en fastsatt dag, bærer fristen et presisjonsnivå ved siden av datoen (3.1). Dette gjelder særlig valgårssensitive frister, men mekanismen er generell. Regelen er at alt har et entydig sorteringspunkt: selv en frist angitt som «medio august» får en avledet sorteringsdag ved lagring, slik at landingsflatens logikk («innen 30 dager / de neste fem»), historikkgrensen (dato pluss én dag) og plasseringen i alle tre visninger fungerer uendret. Grensesnittet viser den tentative angivelsen ærlig og later aldri som om dagen er fastsatt.

En tentativ frist teller med i landingsflatens utvalg på lik linje med presise frister, men bærer alltid en synlig tentativ-merking, slik at ingen planlegger feil rundt en dato som ikke er endelig.

---

## 8. Generering av nytt budsjettår

Generering leser årsmalen og beregner en konkret dato fra hver gjentaksregel. Faste datoer justeres til nærmeste virkedag ved helg. Datoer relativt til en milepæl beregnes fra ankeret, slik at de flytter seg automatisk når ankeret flyttes. Synligheten videreføres fra fjorårets tilsvarende frist som standard.

For å vise administrator fjorårets dato ved siden av den nye (se @BRUKERHISTORIER.md 4.5), slår genereringen opp fjorårets tilsvarende frist via `loep` og forrige budsjettår. De genererte forslagene legges på en egen gjennomgangsflate, atskilt fra den løpende køen.

Valgårslogikken bygger på at den norske valgsyklusen er fast og kan beregnes: en funksjon `valgtype(aar)` gir `stortingsvalg`, `kommunevalg` eller `ingen`. For en regel med `valgaarssensitiv = true` i et stortingsvalgår beregner ikke systemet en presis dato, men markerer fristen per frist (uten et eget banner). En slik frist kan godkjennes som tentativ med en månedsangivelse, men backend og grensesnitt krever en aktiv bekreftelse før en frist uten konkret dag godkjennes.

---

## 9. Utskrift til Word

Administrator kan eksportere frister til et Word-dokument i FINs notatmal (kravdokumentets kap. 8). Funksjonen tar to valg — gruppe og periode — og utvalget er fristene der `synlig_for` inneholder gruppens `kode` innenfor perioden. Dermed gir «skriv ut for `POL`» nøyaktig det settet politisk ledelse selv ville sett, og «skriv ut for `FAG`» utelater FIN-interne frister. Genereringen skjer i backend, der tilgang og data allerede er kjent, slik at utvalget er en gjenbruk av den samme server-side synlighetsfiltreringen som ellers (4).

Dokumentet bærer en synlig topptekst generert fra det faktiske utvalgskriteriet (gruppe og periode), slik at en utskrift ikke kan forveksles med en annen gruppes utvalg. Administrator kan i tillegg velge «alt» (eget fulle innsyn) for internt bruk; denne varianten merkes tydeligst som FIN-internt. Selve utskriftshandlingen logges ikke — i tråd med linjen om å unngå egen handlingslogging der aktivt administrator-innsyn (4) dekker kontrollbehovet.

## 10. Forhold som må avklares med IT før produksjon

Følgende forhold fra kravdokumentets kapittel 12 er forutsetninger for arkitekturen og må avklares med IT før produksjonsdata legges inn: IT- og sikkerhetsgodkjenning av skyløsningen, tverrdepartemental Entra-tilgang (multi-tenant eller gjestebruker, som avgjør om `FAG`-brukere kan autentiseres), konkret mapping fra Entra-attributter til synlighetsgrupper, og bekreftelse på at lagring og filtrering på Azure tilfredsstiller departementets krav til FIN-interne frister. Tverrdepartemental tilgang og endelig attributtmapping kan utvikles mot FINs egen tenant i mellomtiden.

Nødgjenoppretting av administrator (se @BRUKERHISTORIER.md 4.11) skjer via driftsmiljøet — samme mekanisme som seeder den første administratoren — og forutsetter Azure-tilgang. Den er bevisst ikke en funksjon i grensesnittet.
