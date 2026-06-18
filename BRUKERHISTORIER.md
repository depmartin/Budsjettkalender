# Brukerhistorier: Årshjul for budsjettfrister

Dette dokumentet beskriver rollene i løsningen og brukerhistoriene knyttet til hver av dem. For hver historie angis hvilken informasjon løsningen må ivareta for at historien skal kunne oppfylles. Dokumentet bygger på kravdokumentet (@kravdokument-aarshjul-frister_v2.md) og presiserer en rekke forhold som ble avklart i designgjennomgangen. Tekniske konsekvenser av disse avklaringene er samlet i @SYSTEMARKITEKTUR.md.

Et gjennomgående prinsipp gjelder alle historier nedenfor: all synlighetsfiltrering skjer på server. En bruker mottar aldri data vedkommende ikke har rett til å se, uavhengig av hva grensesnittet viser.

---

## 1. Rollemodellen

Tilgang bestemmes langs to uavhengige akser. Den ene aksen er **funksjonsrolle** — hva en bruker kan gjøre. Den andre er **synlighetsgruppe** — hvilke frister en bruker ser. En bruker har én funksjonsrolle og én eller flere synlighetsgrupper.

Funksjonsrollene er administrator, bidragsyter og leser. Synlighetsgruppene er de fire innebygde standardene `FA`, `FIN-FAG`, `FAG` og `POL`, supplert med egendefinerte grupper administrator kan opprette. De to aksene henger sammen i praksis — en leser i et fagdepartement tilhører typisk `FAG`, en bidragsyter i Finansavdelingen tilhører `FA` — men de styres hver for seg, slik at funksjonsrolle og synlighet kan settes uavhengig.

De fem brukertypene dette dokumentet dekker er administrator (FA), bidragsyter i FA, bidragsyter i øvrig FIN-avdeling, leser i fagdepartement, og leser i politisk ledelse. Administrator er alltid FA-ansatt.

---

## 2. Felles for alle som leser frister

Disse historiene gjelder enhver innlogget bruker, uavhengig av funksjonsrolle, fordi alle roller leser frister.

### 2.1 Møtet med løsningen — landingsflaten

Som innlogget bruker vil jeg ved innlogging straks se de fristene som er nær i tid og angår meg, slik at jeg vet hva som kommer uten å lete.

Landingsflaten viser unionen av to kriterier: alle frister innen de neste 30 dagene, og minst de fem førstkommende fristene. Det betyr at brukeren alltid ser noe — i en stille periode uten frister de neste 30 dagene fylles flaten av de fem nærmeste likevel, slik at en tom skjerm aldri oppstår. Visningen er rullerende: den regnes fra inneværende dag hver gang.

Informasjon som må ivaretas: per frist vises tittel, dato (eller tentativ angivelse, se 2.4), kategori og budsjettår. Utvalget er filtrert på server mot brukerens synlighetsgrupper, slik at «angår meg» betyr de fristene der `synlig_for` inneholder minst én av brukerens grupper.

### 2.2 Se mer enn det nærmeste

Som bruker vil jeg kunne hente fram frister som ligger lenger fram i tid enn standardutvalget, slik at jeg kan planlegge utover de nærmeste ukene.

Landingsflaten har en «vis flere»-funksjon som henter frister forbi de neste 30 dagene og de fem nærmeste. Også disse er filtrert på brukerens synlighet.

### 2.3 De tre visningene

Som bruker vil jeg kunne veksle mellom tre presentasjoner av det samme datagrunnlaget, slik at jeg kan velge den som passer behovet mitt.

De tre visningene er en tidslinje/liste som hovedvisning, en kalender i månedsvisning, og et årshjul som viser ett budsjettløp grafisk i sin helhet. Alle tre deler felles filter på kategori og budsjettår, og alle tre viser kun frister brukeren har tilgang til. Fargekoding følger budsjettår, slik at overlappende løp skilles fra hverandre på en kronologisk tidslinje.

Informasjon som må ivaretas: kategorifilter med standard `budsjett` og `gulbok` på og `regnskap` av, og et budsjettårsfilter som standard viser de aktive løpene mens eldre år kan hukes på for tilbakeblikk.

### 2.4 Frister uten eksakt dato

Som bruker vil jeg kunne se frister som ennå ikke har en fastsatt dag, slik at jeg vet at de kommer selv om tidspunktet ikke er endelig.

Enkelte frister har bevisst en mindre presis angivelse enn en konkret dag — typisk valgårssensitive høstfrister der datoen avhenger av regjeringsdannelsen (se 4.5). En slik frist vises ærlig som tentativ, med en angivelse på månedsnivå (unntaksvis primo, medio eller ultimo), og den plasseres likevel kronologisk i alle visninger uten å gi inntrykk av at dagen er bestemt.

### 2.5 Historikk

Som bruker vil jeg kunne se tilbake på frister som har vært, slik at jeg kan studere et tidligere budsjettløp.

En frist forsvinner fra de aktive visningene dagen etter fristdagen — den vises til og med fristdagen selv, og flyttes til historikk dato pluss én dag. Historikken vises ikke automatisk, men kan hentes fram. Historikken filtreres med brukerens nåværende synlighetsregler.

Det er bevisst at løsningen er en ren kalender og ikke et statusverktøy: den forteller hva som skjer når, ikke om en frist ble overholdt. Det finnes derfor ingen «forsinket»-tilstand for noen rolle; datoen styrer alt.

### 2.6 Kilde til fristen

Som bruker vil jeg kunne følge en frist tilbake til rundskrivet den stammer fra, slik at jeg enkelt kan lese mer om bakgrunnen.

Frister som er hentet fra et offentlig rundskriv viser en lenke til kildedokumentet, tilgjengelig for alle som har tilgang til fristen. Lenken arver fristens synlighet: ser man fristen, kan man trygt se lenken, siden måldokumentet uansett er offentlig. Frister uten offentlig kilde — manuelt registrerte og frister generert fra årsmalen — viser ingen lenke og intet opphavsmerke på leserflaten.

Informasjon som må ivaretas: koblingen fra frist til kildedokument og dokumentets URL, slik at lenken kan vises der den finnes.

---

## 3. Bidragsyter (FA og øvrig FIN-avdeling)

Bidragsyter er standard funksjonsrolle for FIN-ansatte. En bidragsyter har alle leserens historier (kapittel 2) i tillegg til historiene nedenfor. En bidragsyter i FA tilhører typisk synlighetsgruppen `FA`; en bidragsyter i en annen FIN-avdeling tilhører `FIN-FAG`. Historiene er like for begge — forskjellen ligger i hvilke frister de ser, ikke i hva de kan gjøre.

### 3.1 Sende inn forslag til ny frist

Som bidragsyter vil jeg kunne melde inn en frist jeg kjenner til, og foreslå hvilke grupper den bør være synlig for, slik at egne oppgaver kommer inn med riktig publikum antydet.

Forslaget legges i godkjenningskøen og er usynlig for andre inntil en administrator godkjenner det. Navnet mitt settes automatisk fra innlogget identitet; jeg taster det aldri inn selv.

Informasjon som må ivaretas: tittel, dato, budsjettår, kategori, fritekstnotat og foreslått synlighet. Foreslått synlighet er et forslag administrator kan overstyre, ikke en endelig innstilling.

### 3.2 Følge med på egne forslag

Som bidragsyter vil jeg kunne se hva som skjedde med forslagene jeg har sendt inn, slik at jeg vet om de venter, er godkjent eller avvist.

Bidragsyteren har en egen «mine forslag»-oversikt som viser status på hvert innsendt forslag: venter, godkjent eller avvist. Dette lukker sløyfen, slik at innsending ikke oppleves som å sende noe inn i et tomrom.

Informasjon som må ivaretas: status per forslag, og koblingen mellom forslaget og bidragsyterens identitet.

### 3.3 Få beskjed ved avgjørelse

Som bidragsyter vil jeg få beskjed når et forslag er behandlet, slik at jeg ikke må sjekke manuelt.

Bidragsyteren varsles inne i løsningen — et varsel i en innboks eller en teller som vises ved neste innlogging. Ved avvisning kan administrator legge ved en kort begrunnelse. Varsling skjer kun i løsningen; det sendes ingen e-post eller annen melding ut av løsningen.

Informasjon som må ivaretas: et varsel knyttet til brukerens identitet, med lest/ulest-tilstand, opprettet når et forslag godkjennes eller avvises.

### 3.4 Sende inn et avvist forslag på nytt

Som bidragsyter vil jeg kunne justere et avvist forslag og sende det inn på nytt, slik at en liten feil ikke betyr at arbeidet er tapt.

Et avvist brukerforslag bevares (det slettes ikke) og kan redigeres og sendes inn på nytt fra «mine forslag». Dette skiller seg fra rundskriv: et behandlet rundskriv foreslås aldri på nytt, mens et avvist brukerforslag bevisst kan gjenbrukes.

### 3.5 Foreslå endring på en publisert frist

Som bidragsyter vil jeg kunne foreslå en endring på en frist som allerede er publisert — for eksempel når en dato må justeres — slik at oversikten holdes oppdatert uten at jeg må gå utenom løsningen.

Et endringsforslag peker på en eksisterende, publisert frist og bærer de foreslåtte nye verdiene. Den publiserte fristen står uendret og fullt synlig for alle lesere mens forslaget venter; den oppdateres først når administrator godkjenner endringen. Forslaget legges i samme godkjenningskø som øvrige forslag.

Informasjon som må ivaretas: referansen til fristen som foreslås endret, de foreslåtte nye verdiene ved siden av de gjeldende, og bidragsyterens identitet.

---

## 4. Administrator (FA)

Administrator er alltid en FA-ansatt og utpekes av en annen administrator inne i løsningen. Administrator har alle leserens og bidragsyterens historier, og ser i tillegg alle frister uavhengig av synlighetsmerking. Historiene nedenfor er det administrator gjør utover å lese og foreslå.

### 4.1 Behandle godkjenningskøen

Som administrator vil jeg gjennomgå innkomne forslag og avgjøre hvert enkelt, slik at bare kvalitetssikrede frister blir publisert.

Køen er en felles innboks for forslag fra både den automatiske innhentingen og fra brukere. Forslagene er gruppert per kilde, men hvert forslag er et selvstendig kort som godkjennes enkeltvis — det finnes ingen masse-godkjenning av en hel bunke, i tråd med den konservative linjen om at heller én manuell vurdering for mye enn én oversett frist. Hvert kort viser tydelig hvilken kilde forslaget kommer fra. Administrator kan filtrere køen på opphav, kilde, ukjent type og kategori.

Handlingene per kort er å godkjenne, justere (åpner redigeringsskjema, deretter godkjenne), eller avvise. For forslag den automatiske innhentingen ikke klarte å kategorisere («ukjent type») er handlingene å vurdere (manuell kategorisering) eller avvise.

Informasjon som må ivaretas: per forslag dets opphav (robot eller bruker), kilde eller innsenders navn, alle fristens felter, og foreslått synlighet.

### 4.2 Verifisere et automatisk uttrekk mot kilden

Som administrator vil jeg raskt kunne kontrollere om et automatisk uttrekk er korrekt, slik at jeg kan stole på det uten å lete i dokumentet selv.

Et forslag fra den automatiske innhentingen viser ikke bare de tolkede feltene, men også tekstutdraget uttrekket bygger på og en lenke til kildedokumentet. Der uttrekket er usikkert på et felt, vises et usikkerhetsflagg på akkurat det feltet — for eksempel «kontroller dato» — slik at administrator ledes rett til det som må kontrolleres. Dette gjør køen til noe administrator faktisk kvalitetssikrer, framfor noe som bare kvitteres ut fordi verifisering er for tungvint.

Informasjon som må ivaretas: per felt i et robotforslag både den tolkede verdien, tekstutdraget verdien er hentet fra, og en markering av hvor sikkert uttrekket er. Denne tilleggsinformasjonen hører til forslaget; når forslaget er godkjent, er det den rene fristen som lever videre.

### 4.3 Legge inn og redigere frister manuelt

Som administrator vil jeg kunne legge inn en frist for hånd og velge synlighet aktivt, slik at riktig publikum ser riktig frist.

Samme redigeringsskjema brukes for å justere et innkommet forslag og for å legge inn en frist manuelt. Ved manuell opprettelse må synlighetsgrupper velges aktivt — det finnes ingen stilltiende standard. Synlighetsregler kan forhåndsutfylle valget, men `POL` settes aldri automatisk (se 4.7). Skjemaet har et valg for å gjenta fristen neste år, som knytter den til en gjentaksregel i årsmalen.

Informasjon som må ivaretas: tittel, dato, budsjettår, kategori, løp, notat og synlighetsgrupper. Når skjemaet åpnes fra et brukerforslag, forhåndsutfylles synligheten med brukerens forslag.

### 4.4 Behandle endringsforslag

Som administrator vil jeg kunne se og avgjøre forslag om å endre en allerede publisert frist, slik at jeg har kontroll på hva som endres.

Endringsforslag ligger i den samme godkjenningskøen og kan filtreres ut som en egen type. Et endringsforslag vises med en før/etter-fremstilling — gjeldende verdi ved siden av foreslått ny verdi — slik at det er tydelig hva som foreslås endret fra hva. Mens et endringsforslag venter, ser administrator et diskret «venter endring»-merke på den berørte fristen. Den publiserte fristen endres først når administrator godkjenner.

Flere bidragsytere kan ha sendt hvert sitt endringsforslag på samme frist. Forslagene vurderes hver for seg; å godkjenne det ene avviser ikke automatisk det andre, fordi det andre kan inneholde en annen, gyldig endring.

Informasjon som må ivaretas: koblingen fra endringsforslaget til den berørte fristen, og hvilke felter som foreslås endret.

### 4.5 Generere et nytt budsjettår

Som administrator vil jeg kunne generere fristene for et kommende budsjettår fra årsmalen, slik at jeg slipper å legge inn alt på nytt hvert år.

Generering leser årsmalen, beregner datoer fra hver gjentaksregel, og legger forslagene fram for gjennomgang. Synligheten videreføres fra fjorårets tilsvarende frist som standard, slik at gruppene ikke må settes på nytt. Gjennomgangen skjer på en egen flate adskilt fra den løpende køen, fordi generering er en bevisst, sesongbestemt handling med en annen rytme enn de forslagene som drypper inn ellers. På denne flaten vises for hver frist den nye beregnede datoen ved siden av fjorårets faktiske dato, slik at administrator ser om malen oppfører seg som forventet.

Valgårssensitive frister i et stortingsvalgår får en markering per frist (det vises ikke et eget banner). En slik frist har ingen meningsfull beregnet dato, siden den avhenger av regjeringsdannelsen. Administrator kan da godkjenne fristen som tentativ — med en angivelse på månedsnivå framfor en konkret dag — men løsningen krever en aktiv «er du sikker?»-bekreftelse før en frist uten konkret dato godkjennes.

Informasjon som må ivaretas: for hver generert frist koblingen til gjentaksregelen og til fjorårets tilsvarende frist (via løp og forrige budsjettår), den beregnede datoen, den videreførte synligheten, og eventuell tentativ presisjon.

### 4.6 Skrive ut til Word

Som administrator vil jeg kunne skrive ut frister til et Word-dokument i FINs notatmal, slik at jeg har et delbart underlag til et møte eller til politisk ledelse.

Utskriften tar to valg: gruppe og periode. Utvalget er de fristene den valgte gruppen faktisk har tilgang til innenfor perioden, slik at «skriv ut for `POL`» gir nøyaktig det settet politisk ledelse selv ville sett, og «skriv ut for `FAG`» utelater FIN-interne frister. Administrator kan i tillegg velge å skrive ut alt — sitt eget fulle innsyn — når dokumentet er til intern bruk. Genereringen skjer i backend, der tilgang og data allerede er kjent.

### 4.7 Styre synlighet for politisk ledelse

Som administrator vil jeg at synlighet for politisk ledelse alltid skal være et bevisst valg, slik at en frist aldri ved et uhell deles med politisk ledelse automatisk.

`POL` er et eget avkrysningsfelt i synlighetsvalget som aldri er forhåndshuket. Verken en synlighetsregel eller et brukerforslag kan sette `POL` automatisk; det krever alltid administrators aktive avkryssing. Foreslår en bruker `POL`, må administrator bekrefte det aktivt ved godkjenning.

### 4.8 Innsyn i hvem som ser hva

Som administrator vil jeg kunne kontrollere hvilke grupper en frist deles med, slik at jeg kan oppdage feil — særlig frister som er delt med politisk ledelse.

Administrator har tre lag innsyn som utfyller hverandre. På hvert fristkort i den daglige visningen vises en diskret gruppemerking som kan utvides ved klikk. «Se som rolle» lar administrator oppleve visningen nøyaktig slik en valgt gruppe ser den. En revisjonsliste per gruppe viser alle frister som er delt med en bestemt gruppe, med de øvrige gruppene synlige ved siden av — det viktigste enkelttilfellet er «alt som er delt med `POL`».

Disse innsynsfunksjonene gjenbruker den samme server-side filtreringen som alt annet; «se som `FAG`» betyr at backend kjører `FAG`-brukerens synlighetsspørring.

### 4.9 Forvalte synlighetsgrupper

Som administrator vil jeg kunne opprette og forvalte synlighetsgrupper, slik at tilgangen kan speile organisasjonen.

Administrator kan opprette nye grupper (for eksempel «Skatteøkonomisk avdeling»), gi dem nytt navn og deaktivere dem. En ny gruppe blir umiddelbart tilgjengelig i synlighetsvalg og utskrift uten kodeendring. En deaktivert gruppe tilbys ikke i nye valg, men historiske frister som pekte på den beholder referansen.

### 4.10 Forvalte brukeres gruppemedlemskap

Som administrator vil jeg kunne styre hvilke grupper en bruker tilhører, slik at også egendefinerte grupper får medlemmer.

Gruppemedlemskap utledes som standard fra Entra-attributter via en konfigurerbar mapping, men administrator kan i tillegg overstyre manuelt inne i løsningen — legge en bruker til eller fjerne fra en gruppe. Dette krever en enkel brukeradministrasjonsflate der administrator kan finne en bruker og se de utledede gruppene før en eventuell manuell justering.

### 4.11 Utpeke andre administratorer

Som administrator vil jeg kunne gi andre FA-ansatte administratortilgang, slik at flere kan forvalte fristene og løsningen ikke står og faller med én person.

Kun FA-ansatte kan velges som administrator. Den normale veien til administratorrollen går alltid gjennom en sittende administrator som utpeker en ny. Det finnes ingen selvbetjent mulighet for en FA-ansatt til å gi seg selv administratortilgang.

For det tilfellet at det ikke lenger finnes noen aktiv administrator — slik at køen er frosset og ingen kan utpeke en ny — finnes en nødvei utenfor løsningen: en gjenoppretting via driftsmiljøet (samme mekanisme som setter den første administratoren ved idriftsetting). Denne nødveien forutsetter tilgang til driftsmiljøet og er ikke en funksjon i grensesnittet.

---

## 5. Leser i fagdepartement (`FAG`)

En leser i et fagdepartement har leserens historier i kapittel 2 og ingen flere. Synlighetsgruppen er `FAG`.

### 5.1 Se egne leveransefrister

Som ansatt i et fagdepartement vil jeg se de fristene som er merket for `FAG`, slik at jeg vet hva mitt departement må levere og når.

Brukeren ser frister der `synlig_for` inneholder `FAG`, og ser ikke FINs interne frister. Landingsflaten, de tre visningene, historikken og kildelenkene fungerer som beskrevet i kapittel 2, alltid avgrenset til det `FAG` har tilgang til.

### 5.2 Kun lesetilgang

Som ansatt i et fagdepartement har jeg kun lesetilgang. Jeg kan ikke sende inn forslag og kan ikke bli administrator. Dette er en egenskap ved rollen, ikke en handling — men det er verdt å ivareta i grensesnittet ved at handlinger som å foreslå frister ikke tilbys denne brukeren.

---

## 6. Leser i politisk ledelse (`POL`)

Politisk ledelse har leserens historier i kapittel 2, med den samme begrensningen til kun lesetilgang som fagdepartementene. Synlighetsgruppen er `POL`.

### 6.1 Se frister som angår politisk ledelse

Som politiker vil jeg se de fristene som er merket for `POL`, slik at jeg har oversikt over det som angår politisk ledelse.

Brukeren ser frister der `synlig_for` inneholder `POL`. `POL` settes alltid eksplisitt av administrator (se 4.7), slik at det som vises her er bevisst delt med politisk ledelse. Landingsflaten og visningene fungerer som i kapittel 2, avgrenset til `POL`.

---

## 7. Overlappende synlighet (gjelder alle)

Synlighet er et sett av grupper per frist, ikke et nivåtall. En frist kan derfor merkes for vilkårlige kombinasjoner — `FA` og `POL` uten `FAG`, eller `FA` og `FAG` uten `POL`. En bruker ser fristen dersom minst én av brukerens grupper er i fristens sett. Dette gir den fleksibiliteten at riktig undergruppe ser en frist uten at synligheten må følge en fast rangordning.
