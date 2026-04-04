# Agent Guidelines for Środkowy Portal

This document provides development guidelines for AI agents working in the Środkowy Portal codebase.

## Technology Stack

- **Frontend:** Astro 5 + React islands + Tailwind CSS 4
- **Backend:** C# / .NET 8, Azure Functions (isolated worker model)
- **Orchestration:** Azure Durable Functions
- **Database:** Azure SQL Database
- **LLM:** OpenAI API (GPT-4o, text-embedding-3-small)
- **Scraping:** Firecrawl API
- **Frontend hosting:** Azure Static Web Apps
- **IaC:** Bicep
- **CI/CD:** GitHub Actions
- **Testing:** xUnit (.NET), Vitest (Astro/React), Playwright (E2E)

## Project Structure

```
src/frontend/              # Astro app (SSG, React islands, Tailwind)
src/functions/             # .NET 8 Azure Functions solution
infra/                     # Bicep IaC templates
tests/e2e/                 # Playwright E2E tests
tests/integration/         # Pipeline integration tests
docs/                      # Documentation
.github/workflows/         # CI/CD pipelines
```

## Conventions

- Polish content, English code — all UI text and generated content in Polish; code, variable names, comments, and docs in English.
- No comments in code unless explicitly requested.
- Follow existing code style and patterns in each project.
- Never commit secrets or keys.

## Key Commands

### Backend (.NET)

```bash
# Build
dotnet build src/functions/Srodkowy.sln

# Run tests
dotnet test src/functions/Srodkowy.sln

# Add migration
dotnet ef migrations add <Name> --project src/functions/Srodkowy.Functions/Srodkowy.Functions.csproj --startup-project src/functions/Srodkowy.Functions/Srodkowy.Functions.csproj

# Apply migrations
dotnet ef database update --project src/functions/Srodkowy.Functions/Srodkowy.Functions.csproj --startup-project src/functions/Srodkowy.Functions/Srodkowy.Functions.csproj

# Run functions locally
cd src/functions/Srodkowy.Functions && func start

# Trigger ingestion for all sources
curl -X POST http://localhost:7071/api/ingestion/run

# Trigger ingestion for one source
curl -X POST http://localhost:7071/api/ingestion/run/<source-guid>
```

Current backend scope is raw ingestion only. Clustering, synthesis, read-side content API, and Durable orchestration are planned but not implemented yet.

Cloud deployment currently targets Azure Functions Flex Consumption, Azure SQL Database, and Azure Key Vault without VNet/private endpoints. EF migrations are triggered through a function-key-protected admin endpoint after deploy.

### Frontend (Astro)

```bash
# Install dependencies
cd src/frontend && npm install

# Dev server
cd src/frontend && npm run dev

# Build
cd src/frontend && npm run build

# Lint
cd src/frontend && npm run lint

# Test
cd src/frontend && npm run test
```

### E2E Tests

```bash
cd tests/e2e && npx playwright test
```

### Infrastructure

```bash
az deployment group create --resource-group rg-srodkowy-pc --template-file infra/main.bicep --parameters infra/parameters/dev.bicepparam
```

## Architecture Reference

See [docs/architecture.md](docs/architecture.md) for full architecture details including pipeline flow, database schema, API endpoints, and design decisions.
