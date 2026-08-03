# Fase 4 — Internkalender for SBR: plan og gjennomføring

Byggeplan for internkalenderen — en SBR-intern arbeidsliste atskilt fra de publiserte
fristene. Lagt fram og godkjent før koding (jf. CLAUDE.md). Forankret i den faktiske
.NET-koden: `backend/Aarshjul.slnx` lagdelt i Domain/Application/Infrastructure/Web/Tests,
Blazor Web App (Interactive Server) + EF Core.

Kravgrunnlag: @kravdokument-aarshjul-frister_v2.md kap. 13; @SYSTEMARKITEKTUR.md kap. 12;
@BRUKERHISTORIER.md kap. 8.

> STATUS: **KODET 2026-08-03 (trinn 1–3) — på `main`.** 32 nye tester (216 totalt), demo
> verifisert (SBR-gating, sider rendrer, seed). EF-migrasjon `Internkalender`.

---

## 1. Mål og avgrensning

En egen «fane» kun for SBR (i v1 = administrator, bak policy `ErSbr`) med gjøremål organisert i
budsjettrunder. To nivåer: generelle regler (mal) og konkrete runder man huker av i. Avgrensning
v1: ingen eksport/Outlook/varsler; internkalenderen er en ren i-app-funksjon.

## 2. Bærende valg (fra designintervjuet 2026-08-03)

- **Runder:** fire faste rundetyper (Marsrunden, Augustrunden inkl. gul bok/Prop. 1 S, RNB,
  Nysalderingen) instansiert per år, med kalenderår-forskyvning (Mars/August = t-1, RNB/Nysaldering
  = t); pluss Regnskap (per regnskapsår) og Øvrig (stående, kun manuell).
- **Generelt → konkret:** regler knyttes til én eller flere rundetyper («hver runde» = alle) og
  genereres **enkeltvis** inn i hver valgt runde. Konkret runde = **snapshot**; «Synkroniser» viser
  regelendringer som forslag admin godtar enkeltvis, og rører aldri avhukede/manuelt endrede gjøremål.
- **Tidfesting:** konkret dato, tentativ måned (primo/medio/ultimo), relativt til anker-løp
  (gjenbruker frist-mekanismen), eller rundeposisjon; manglende anker → «venter på ankerdato».
- **Tilgang:** kun SBR; flere ansvarlige (brukerliste eller fritekst); «kun mine»-filter og en
  personlig tverr-rundevisning.
- **Avhuking:** hvem som helst i SBR kan huke av (bekreftelse hvis ikke selv ansvarlig); hvem/når
  lagres; ferdige flyttes til «ferdig»-visning; kan gjenåpnes. Hurtiginnlegging med bare tittel.

## 3. Trinn (hvert verifisert med `dotnet test` grønt)

- **Trinn 1 — kjerne:** domenemodell (`InternRunde`, `InterntGjoeremaal`, `GjoeremaalAnsvarlig`,
  `Runder`-logikk, enums), DbContext, policy `ErSbr`, `IInternkalender`/`InternkalenderTjeneste`
  (runder + gjøremål + tidfesting→sorteringsdag + avhuking + «mine» + personfilter), flater
  (oversikt, rundevisning, gjøremål-skjema, «Mine gjøremål»), nav-fane, demo-seed. Tester: 19.
- **Trinn 2 — maler + generering:** `GjoeremaalRegel`/`RegelRundetype`/`RegelAnsvarlig`,
  `IGjoeremaalRegler`/`GjoeremaalRegelTjeneste` (CRUD), `GenererRundeAsync` (oversetter
  årsuavhengig regel-tidfesting til konkret dato for rundeåret, kopierer ansvarlige), regel-flater.
  Tester: 7.
- **Trinn 3 — synkronisering:** `ForberedSynkAsync`/`SynkroniserAsync` (legg til/oppdater/fjern som
  forslag; aldri avhukede/manuelt endrede), synk-flate. Tester: 6.
- **Migrasjon + dok + merge:** EF-migrasjon `Internkalender`; oppdatert kravdok./SYSTEMARKITEKTUR/
  BRUKERHISTORIER/arkitektur.md/beslutningslogg; demo-verifisert; merget til `main`.

## 4. Kvalitetskrav

- **SBR-intern på server:** alle flater bak `ErSbr`; ingen internkalender-data til en ikke-SBR-klient
  (verifisert i demo: bidragsyter nektes, anon → innlogging).
- **Snapshot + trygg synk:** generering er et øyeblikksbilde; synk overskriver/fjerner aldri
  automatisk, og rører aldri avhukede eller manuelt endrede gjøremål (regresjonstestet).
- **Ærlig tidfesting:** manglende anker gir «venter på ankerdato», aldri en gjettet dato.

## 5. Mulige senere utvidelser

- Egen SBR-akse skilt fra administrator (policy `ErSbr` er allerede isolert).
- Eksport av en runde (Word/.ics) og/eller varsel til ansvarlig — gjenbruk av eksisterende ledd.
- Anker-oppslag også mot dato-vindu (ikke bare budsjettår), og milepæl-nedtrekksliste som i malregelskjemaet.
- Flerfrist-presisjon og helligdager (som for fristgenereringen).
