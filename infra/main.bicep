// Grunnleggende Azure-infrastruktur for «Årshjul for budsjettfrister» (Fase 1).
// App Service (Blazor + API), Azure SQL, Key Vault, Application Insights.
// Hemmeligheter ligger ikke i klartekst her; databasen aksesseres med managed identity
// (Entra-autentisering), og web-appens identitet gis lesetilgang til Key Vault via RBAC.

@description('Plassering for alle ressurser.')
param location string = resourceGroup().location

@description('Kort, unikt prefiks for ressursnavn (små bokstaver/tall).')
@minLength(3)
@maxLength(12)
param navnPrefiks string

@description('Miljønavn, brukes i tagger og navn (f.eks. dev, test, prod).')
param miljo string = 'dev'

@description('Visningsnavn for Entra-administrator av SQL-serveren.')
param sqlAdAdminLogin string

@description('Object-id (sid) for Entra-administrator (bruker eller gruppe) av SQL-serveren.')
param sqlAdAdminObjectId string

@description('SKU for App Service-planen.')
param appServiceSku string = 'B1'

var suffix = uniqueString(resourceGroup().id)
var appServicePlanNavn = '${navnPrefiks}-plan-${miljo}'
var webAppNavn = '${navnPrefiks}-web-${miljo}-${suffix}'
var sqlServerNavn = '${navnPrefiks}-sql-${miljo}-${suffix}'
var sqlDbNavn = 'aarshjul'
var keyVaultNavn = take('${navnPrefiks}kv${suffix}', 24)
var logAnalyticsNavn = '${navnPrefiks}-log-${miljo}'
var appInsightsNavn = '${navnPrefiks}-ai-${miljo}'
var taggar = {
  prosjekt: 'aarshjul-budsjettfrister'
  miljo: miljo
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsNavn
  location: location
  tags: taggar
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsNavn
  location: location
  kind: 'web'
  tags: taggar
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanNavn
  location: location
  tags: taggar
  sku: { name: appServiceSku }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppNavn
  location: location
  tags: taggar
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      connectionStrings: [
        {
          // EF Core leser ConnectionStrings:Aarshjul. Managed identity via Entra-autentisering.
          name: 'Aarshjul'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDbNavn};Authentication=Active Directory Default;Encrypt=True;'
        }
      ]
      appSettings: [
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'KeyVault__Uri'
          value: keyVault.properties.vaultUri
        }
      ]
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultNavn
  location: location
  tags: taggar
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

// Web-appens managed identity får lese hemmeligheter fra Key Vault (Key Vault Secrets User).
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
resource kvRolle 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerNavn
  location: location
  tags: taggar
  properties: {
    // Kun Entra-autentisering — ingen SQL-passord å lagre.
    administrators: {
      administratorType: 'ActiveDirectory'
      login: sqlAdAdminLogin
      sid: sqlAdAdminObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: sqlDbNavn
  location: location
  tags: taggar
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
  }
}

// Tillat andre Azure-tjenester (App Service) å nå SQL-serveren.
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output webAppNavnUt string = webApp.name
output webAppVertsnavn string = webApp.properties.defaultHostName
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultNavnUt string = keyVault.name
