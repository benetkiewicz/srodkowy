param location string = resourceGroup().location
param environmentName string = 'dev'
param sqlAdminGroupName string
param sqlAdminObjectId string
param tenantId string = subscription().tenantId
param firecrawlBaseUrl string = 'https://api.firecrawl.dev'
param firecrawlTimeoutSeconds int = 60
param firecrawlRequestsPerMinute int = 10
param ingestionMaxCandidateLinksPerSource int = 25
param ingestionMaxArticlesPerSource int = 15
param cleanupBatchSize int = 25
param cleanupLookbackHours int = 96
param cleanupMaxInputCharacters int = 24000
param cleanupMinCleanedLength int = 320
param embeddingBatchSize int = 25
param embeddingLookbackHours int = 96
param embeddingMaxInputCharacters int = 12000
param openAiCleanupModel string = 'gpt-4o'
param openAiChatModel string = 'gpt-4o'
param openAiEmbeddingModel string = 'text-embedding-3-small'
param adminMigrationsEnabled bool = true
param maximumInstanceCount int = 100
param instanceMemoryMb int = 2048

var uniqueSuffix = take(toLower(uniqueString(subscription().subscriptionId, resourceGroup().id, environmentName, 'srodkowy')), 6)
var tags = {
  environment: environmentName
  project: 'srodkowy'
  regionCode: 'pc'
}
var functionIdentityName = 'uami-func-srodkowy-pc'
var functionAppName = 'func-srodkowy-pc-${uniqueSuffix}'
var hostingPlanName = 'plan-srodkowy-pc-${uniqueSuffix}'
var storageAccountName = 'stfuncsrodkowypc${uniqueSuffix}'
var deploymentContainerName = 'app-package-${uniqueSuffix}'
var logAnalyticsName = 'law-srodkowy-pc'
var applicationInsightsName = 'appi-srodkowy-pc'
var keyVaultName = 'kv-srodkowy-pc-${uniqueSuffix}'
var sqlServerName = 'sql-srodkowy-pc-${uniqueSuffix}'
var sqlDatabaseName = 'sqldb-srodkowy-pc'

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    name: functionIdentityName
    tags: tags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    applicationInsightsName: applicationInsightsName
    location: location
    logAnalyticsName: logAnalyticsName
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    deploymentContainerName: deploymentContainerName
    location: location
    name: storageAccountName
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    name: keyVaultName
    principalId: identity.outputs.principalId
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    aadAdminObjectId: sqlAdminObjectId
    aadAdminPrincipalType: 'Group'
    aadAdminTenantId: tenantId
    aadAdminName: sqlAdminGroupName
    databaseName: sqlDatabaseName
    location: location
    name: sqlServerName
    tags: tags
  }
}

var databaseConnectionString = 'Server=tcp:${sql.outputs.serverFqdn},1433;Database=${sql.outputs.databaseName};Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;User Id=${identity.outputs.clientId};'
var firecrawlApiKeyReference = '@Microsoft.KeyVault(VaultName=${keyVault.outputs.name};SecretName=firecrawl-api-key)'
var openAiApiKeyReference = '@Microsoft.KeyVault(VaultName=${keyVault.outputs.name};SecretName=openai-api-key)'

module functions 'modules/functions-flex.bicep' = {
  name: 'functions'
  params: {
    adminMigrationsEnabled: adminMigrationsEnabled
    appInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    appInsightsName: monitoring.outputs.applicationInsightsName
    cleanupBatchSize: cleanupBatchSize
    cleanupLookbackHours: cleanupLookbackHours
    cleanupMaxInputCharacters: cleanupMaxInputCharacters
    cleanupMinCleanedLength: cleanupMinCleanedLength
    databaseConnectionString: databaseConnectionString
    deploymentContainerName: storage.outputs.deploymentContainerName
    embeddingBatchSize: embeddingBatchSize
    embeddingLookbackHours: embeddingLookbackHours
    embeddingMaxInputCharacters: embeddingMaxInputCharacters
    firecrawlApiKeyReference: firecrawlApiKeyReference
    firecrawlBaseUrl: firecrawlBaseUrl
    firecrawlRequestsPerMinute: firecrawlRequestsPerMinute
    firecrawlTimeoutSeconds: firecrawlTimeoutSeconds
    functionAppName: functionAppName
    hostingPlanName: hostingPlanName
    ingestionMaxArticlesPerSource: ingestionMaxArticlesPerSource
    ingestionMaxCandidateLinksPerSource: ingestionMaxCandidateLinksPerSource
    instanceMemoryMb: instanceMemoryMb
    location: location
    maximumInstanceCount: maximumInstanceCount
    openAiApiKeyReference: openAiApiKeyReference
    openAiCleanupModel: openAiCleanupModel
    openAiChatModel: openAiChatModel
    openAiEmbeddingModel: openAiEmbeddingModel
    storageAccountName: storage.outputs.accountName
    storagePrimaryBlobEndpoint: storage.outputs.primaryBlobEndpoint
    tags: tags
    userAssignedIdentityClientId: identity.outputs.clientId
    userAssignedIdentityPrincipalId: identity.outputs.principalId
    userAssignedIdentityResourceId: identity.outputs.resourceId
  }
}

output functionAppName string = functions.outputs.functionAppName
output functionAppHostname string = functions.outputs.functionAppHostname
output functionIdentityName string = identity.outputs.name
output functionIdentityClientId string = identity.outputs.clientId
output keyVaultName string = keyVault.outputs.name
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
