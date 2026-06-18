# Budsjettkalender — Årshjul for budsjettfrister

Delt fristoversikt for arbeidet med statsbudsjettet. Eier: Seksjon for statsbudsjett og
regnskap (SBR), Finansavdelingen, Finansdepartementet.

Løsningen samler budsjettfrister fra FINs offentlige rundskriv på regjeringen.no,
suppleres manuelt, og presenterer dem som et flerårig årshjul med tre visninger
(tidslinje/liste, kalender, årshjul). Innlogging skjer med departementenes SSO (Entra ID),
synlighet håndheves på server, og automatiske uttrekk passerer alltid en godkjenningskø.
Drift på Azure med infrastruktur som kode (Bicep).

## Dokumentasjon

- [`kravdokument-aarshjul-frister_v2.md`](kravdokument-aarshjul-frister_v2.md) — full kravspesifikasjon.
- [`SYSTEMARKITEKTUR.md`](SYSTEMARKITEKTUR.md) — teknisk arkitektur og datamodell.
- [`BRUKERHISTORIER.md`](BRUKERHISTORIER.md) — roller og brukerhistorier.
- [`utviklingsplan-aarshjul-frister.md`](utviklingsplan-aarshjul-frister.md) — prosjektramme og faseplan.
- [`claude-md-opplegg.md`](claude-md-opplegg.md) — opplegg for kontekstbevaring mellom byggeøkter.

## Utviklingskontekst (Claude Code)

- [`CLAUDE.md`](CLAUDE.md) — stabil indeks med bærende prinsipper og pekere; lastes hver økt.
- [`.claude/rules/beslutningslogg.md`](.claude/rules/beslutningslogg.md) — prosjektets hukommelse. Les denne først hver økt.
- [`.claude/rules/arkitektur.md`](.claude/rules/arkitektur.md) — stack, mappestruktur og kommandoer.

## Mappestruktur

- `infra/` — Bicep-maler for Azure-infrastruktur.
- `backend/` — API, forretningslogikk, autorisering, synlighetsfiltrering.
- `backend/kilder/` — `Kilde`-grensesnittet og `RegjeringenKilde`.
- `backend/jobb/` — bakgrunnsjobb: oppdagelse, dedup, uttrekk, klassifisering.
- `frontend/` — de tre visningene, landingsflate, godkjenningskø, administratorflater.
- `.github/workflows/` — GitHub Actions (Bicep what-if → deploy).

## Status

Fase 0 — design og spesifikasjon er fullført. Kontekstapparat og mappestruktur er etablert.
Neste steg er Fase 1 (fundament); se «Status nå» i `.claude/rules/beslutningslogg.md`.
