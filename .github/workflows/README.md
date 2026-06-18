# .github/workflows

- `ci.yml` — bygger og kjører tester (`dotnet restore/build/test`) på hver PR og push til main.
- `deploy.yml` — manuell (workflow_dispatch): Bicep what-if, og deploy + app-publisering når
  input `deploy` er true. Bruker OIDC-innlogging mot Azure.

## Nødvendige secrets/variabler for `deploy.yml`

Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
`SQL_AD_ADMIN_LOGIN`, `SQL_AD_ADMIN_OBJECT_ID`.

Variabler: `AZURE_RESOURCE_GROUP`, `NAVN_PREFIKS`, `MILJO`.

Innlogging bruker OIDC (ingen lagrede passord) — sett opp en federated credential på
app-registreringen mot dette repoet.
