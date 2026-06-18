using './main.bicep'

// Eksempelparametre. Fyll inn reelle verdier (eller overstyr i GitHub Actions).
// sqlAdAdminObjectId settes typisk til object-id for en Entra-gruppe av FA-administratorer.

param navnPrefiks = 'aarshjul'
param miljo = 'dev'
param sqlAdAdminLogin = 'FIN-Aarshjul-Admins'
param sqlAdAdminObjectId = '00000000-0000-0000-0000-000000000000'
param appServiceSku = 'B1'
