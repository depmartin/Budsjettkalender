# Prosjekt: Årshjul for budsjettfrister

Delt fristoversikt for arbeidet med statsbudsjettet (Seksjon for statsbudsjett og regnskap, FIN).
Full kravspesifikasjon: @kravdokument-aarshjul-frister_v2.md

## Hva dette er
- Webapp som samler budsjettfrister, henter dem fra FINs rundskriv på regjeringen.no, suppleres manuelt.
- Flerårig årshjul: tidligere år kan studeres, kommende år genereres fra mal.
- To roller: administrator (endrer, godkjenner, genererer) og bruker (leser, kan foreslå).

## Bærende prinsipper (ENDRES IKKE uten å oppdatere beslutningsloggen)
- Frister identifiseres på FUNKSJON, aldri på rundskrivnummer (nummer kan skifte mellom år).
- Automatisk uttrekk går ALDRI rett til publisering — alt passerer godkjenningskø.
- Skarpt rolleskille: ingen brukerhandling endrer data direkte.
- All synlighetsfiltrering skjer på server; data en bruker ikke har rett til, sendes aldri til klienten.
- Kilde er et utbyttbart ledd (regjeringen.no nå, DFØ senere) bak ett felles grensesnitt.

## Referanser
- Arkitektur, stack, kommandoer: @.claude/rules/arkitektur.md
- Beslutninger og hvorfor (LES DENNE FØRST hver økt): @.claude/rules/beslutningslogg.md
- Teknisk design og datamodell: @SYSTEMARKITEKTUR.md
- Roller og brukerflate: @BRUKERHISTORIER.md
- Detaljert byggeplan for fase 2 (automatikk): @fase2-plan.md
- Detaljert byggeplan for fase 3 (mal og generering): @fase3-plan.md

## Arbeidsmåte
- Bygg i faser slik kravdokumentet beskriver. Fullfør og verifiser én fase før neste.
- YOU MUST legge fram en plan før du skriver kode i en ny fase, og vente på godkjenning.
- IMPORTANT: Ved slutten av hver økt, oppdater beslutningsloggen med nye beslutninger, valgt
  fremgangsmåte og åpne spørsmål. Skriv det FØR økten blir lang.
- Når en beslutning endrer noe i «Bærende prinsipper», oppdater begge steder.
