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

Dedup mot behandlet-dokument-registeret skjer nedstrøms (Steg C), ikke i kilden.

Implementasjoner: `RegjeringenKilde` (Steg B) er første; DFØ og andre kobles på senere
bak samme grensesnitt uten ombygging.
