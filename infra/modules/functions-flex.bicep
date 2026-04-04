param location string
param functionAppName string
param hostingPlanName string
param userAssignedIdentityResourceId string
param userAssignedIdentityPrincipalId string
param userAssignedIdentityClientId string
param storageAccountName string
param storagePrimaryBlobEndpoint string
param deploymentContainerName string
param appInsightsName string
param appInsightsConnectionString string
param databaseConnectionString string
param firecrawlApiKeyReference string
param firecrawlBaseUrl string
param firecrawlTimeoutSeconds int
param firecrawlRequestsPerMinute int
param ingestionMaxCandidateLinksPerSource int
param ingestionMaxArticlesPerSource int
param ingestionMinContentLength int
param adminMigrationsEnabled bool
param maximumInstanceCount int
param instanceMemoryMb int
param tags object = {}

var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: hostingPlanName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityResourceId}': {}
    }
  }
  properties: {
    httpsOnly: true
    keyVaultReferenceIdentity: userAssignedIdentityResourceId
    serverFarmId: hostingPlan.id
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storagePrimaryBlobEndpoint}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: userAssignedIdentityResourceId
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: instanceMemoryMb
        maximumInstanceCount: maximumInstanceCount
      }
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: {
    Admin__Migrations__Enabled: string(adminMigrationsEnabled)
    APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'ClientId=${userAssignedIdentityClientId};Authorization=AAD'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    AzureWebJobsStorage__accountName: storageAccountName
    AzureWebJobsStorage__blobServiceUri: 'https://${storageAccountName}.blob.${environment().suffixes.storage}'
    AzureWebJobsStorage__clientId: userAssignedIdentityClientId
    AzureWebJobsStorage__credential: 'managedidentity'
    AzureWebJobsStorage__queueServiceUri: 'https://${storageAccountName}.queue.${environment().suffixes.storage}'
    AzureWebJobsStorage__tableServiceUri: 'https://${storageAccountName}.table.${environment().suffixes.storage}'
    Database__ConnectionString: databaseConnectionString
    Firecrawl__ApiKey: firecrawlApiKeyReference
    Firecrawl__BaseUrl: firecrawlBaseUrl
    Firecrawl__RequestsPerMinute: string(firecrawlRequestsPerMinute)
    Firecrawl__TimeoutSeconds: string(firecrawlTimeoutSeconds)
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    Ingestion__MaxArticlesPerSource: string(ingestionMaxArticlesPerSource)
    Ingestion__MaxCandidateLinksPerSource: string(ingestionMaxCandidateLinksPerSource)
    Ingestion__MinContentLength: string(ingestionMinContentLength)
  }
}

resource storageBlobOwnerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentityPrincipalId, storageBlobDataOwnerRoleId)
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
  }
}

resource storageQueueContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentityPrincipalId, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
  }
}

resource storageTableContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentityPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
  }
}

resource appInsightsPublisherAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, userAssignedIdentityPrincipalId, monitoringMetricsPublisherRoleId)
  scope: applicationInsights
  properties: {
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
  }
}

output functionAppName string = functionApp.name
output functionAppHostname string = '${functionApp.name}.azurewebsites.net'
