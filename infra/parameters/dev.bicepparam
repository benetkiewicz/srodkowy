using '../main.bicep'

param location = 'polandcentral'
param environmentName = 'dev'
param sqlAdminGroupName = 'Srodkowy-Admins'
param sqlAdminObjectId = '06c68322-dfbe-4239-a4f7-64b2b0f54c7b'
param tenantId = 'bb3fba58-53a1-487d-89f1-45e17aa903f6'
param firecrawlBaseUrl = 'https://api.firecrawl.dev'
param firecrawlTimeoutSeconds = 60
param firecrawlRequestsPerMinute = 10
param ingestionMaxCandidateLinksPerSource = 25
param ingestionMaxArticlesPerSource = 10
param ingestionMinContentLength = 600
param adminMigrationsEnabled = true
param maximumInstanceCount = 100
param instanceMemoryMb = 2048
