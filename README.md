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
- [`fase2-plan.md`](fase2-plan.md) — detaljert byggeplan for fase 2 (automatikk).
- [`fase3-plan.md`](fase3-plan.md) — detaljert byggeplan for fase 3 (mal og generering).
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

**Fase 1 (fundament) er ferdig** og ligger i PR #1: .NET 10 / ASP.NET Core + EF Core,
Azure SQL, Blazor (Interactive Server), server-side synlighetsfiltrering, Entra-innlogging,
manuell innlegging, de tre visningene, administrator-innsyn, og grunnleggende Bicep + CI.
23 tester grønt. Planleggingen av Fase 2 (automatikk) og Fase 3 (mal og generering) er
ferdig — se `fase2-plan.md` og `fase3-plan.md`. Neste er koding av Fase 2 (kilde,
godkjenningskø, brukerforslag, Word-utskrift, bakgrunnsjobb), deretter Fase 3.

Se `.claude/rules/beslutningslogg.md` («Status nå») for fremdrift og neste steg, og
`.claude/rules/arkitektur.md` for stack, miljøoppsett og kommandoer.

### Bygg og test

Krever .NET 10 SDK (se «Miljøoppsett» i `arkitektur.md` hvis det mangler i miljøet).

```bash
dotnet build backend/Aarshjul.slnx
dotnet test backend/Aarshjul.slnx
```
