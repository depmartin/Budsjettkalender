# infra

Bicep-maler for Azure-infrastrukturen (kravdok. 9.2).

- `main.bicep` — App Service-plan + App Service (Blazor + API), Azure SQL (Entra-only auth,
  serverless), Key Vault (RBAC), Application Insights + Log Analytics. Web-appens managed
  identity gis lesetilgang til Key Vault og brukes til SQL-tilkobling (ingen lagrede passord).
- `main.bicepparam` — eksempelparametre; fyll inn reelle verdier eller overstyr i CI.

## Validering og utrulling

- Kompiler/valider lokalt: `bicep build infra/main.bicep`
- What-if: `az deployment group what-if -g <rg> -f infra/main.bicep -p navnPrefiks=<x> miljo=dev sqlAdAdminLogin=<grp> sqlAdAdminObjectId=<oid>`
- Deploy skjer normalt via `.github/workflows/deploy.yml` (manuell dispatch).

Etter første utrulling må web-appens managed identity få databasetilgang i SQL via T-SQL
(`CREATE USER [<web-app-navn>] FROM EXTERNAL PROVIDER;` + `db_datareader`/`db_datawriter`/
`db_ddladmin` for migrasjoner). Dette gjøres som Entra-administrator på databasen.
