# backend/kilder

`Aarshjul.Kilder` — det utbyttbare kildeleddet (kravdok. 4.1, SYSTEMARKITEKTUR 5).

- `IKilde` — grensesnittet: `OppdagAsync()` og `HentAsync(referanse)`. Resten av systemet
  er kildeagnostisk.
- `OppdagResultat`/`Oppdagutfall` — uttrykker utfall, ikke bare data: en vellykket, tom
  kjøring (`IngenDokumenter`) skilles fra en parse-feil (`KlarteIkkeParse`), slik at en
  stille feil ikke forveksles med en stille periode uten nye rundskriv.
- `HentResultat`/`Hentutfall` — nedlasting lyktes/feilet; råinnhold (PDF-bytes).
  Tekst-/datouttrekk skjer i et senere ledd (Steg E), ikke i kilden.
- `Dokumentreferanse` — kildeagnostisk referanse (nøkkel, tittel, dato, url, nummer-hint).

- `Totrinnsfilter`/`Loepmonster` — Steg D: nummerserie + tittelgjenkjenning (kravdok. 4.3).
- `RegjeringenParser` — ren HTML-parsing av rundskrivarkivet (AngleSharp), adskilt fra
  nettverksleddet så den kan testes offline mot en lagret kopi (`Aarshjul.Tests/Fixtures/`).
  Leser PDF-URL fra `href` (filnavnene er inkonsekvente over årene), utleder kanonisk
  `DokumentNokkel = r-{nr}-{aar}`, og tolker to-/firesifret år.
- `RegjeringenKilde` — Steg B: henter arkivsiden/PDF med ekte User-Agent og kaller parseren.

Dedup mot behandlet-dokument-registeret skjer nedstrøms (Steg C), ikke i kilden.

Implementasjoner: `RegjeringenKilde` (Steg B) er første; DFØ og andre kobles på senere
bak samme grensesnitt uten ombygging.

> **Live-tilgang (2026-07):** `www.regjeringen.no` ligger bak Cloudflares «challenge» og
> svarer 403 på ikke-nettleserklienter. `OppdagAsync`/`HentAsync` er strukturelt ferdige, men
> live henting krever IT-avklaring (Cloudflare-whitelisting / offisielt API) eller nettleser-
> rendering. Parsing- og URL-logikken er ferdig og testet mot fixtur i mellomtiden.
