param location string
param name string
param databaseName string
param aadAdminName string
param aadAdminObjectId string
param aadAdminTenantId string
param aadAdminPrincipalType string = 'Group'
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2020-11-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    administrators: {
      azureADOnlyAuthentication: true
      login: aadAdminName
      principalType: aadAdminPrincipalType
      sid: aadAdminObjectId
      tenantId: aadAdminTenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
}

resource allowAzureServicesRule 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

output serverName string = sqlServer.name
output serverFqdn string = '${sqlServer.name}${environment().suffixes.sqlServerHostname}'
output databaseName string = database.name
