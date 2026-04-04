# Środkowy Portal — Architecture

## Overview

Środkowy is a Polish news portal built around narrative comparison. It scrapes articles from curated left-leaning and right-leaning Polish media sources every 12 hours, clusters them by event, generates a neutral central synthesis with annotated markers via LLM, and publishes a static site showing how each ideological camp frames the same story.

## Current Status

Phase 1 is implemented locally.

- Azure Functions isolated worker solution bootstrapped under `src/functions`
- EF Core migrations wired for SQL Server / Azure SQL
- Curated source list seeded into the database
- Firecrawl-backed raw ingestion working through manual HTTP triggers
- Raw articles and ingestion runs persisted in SQL Server Express
- Bicep and GitHub Actions scaffolding added for Azure deployment into `rg-srodkowy-pc`

The following remain target-state only for now: Durable orchestration, embeddings, clustering, synthesis, edition publishing, and read-side content API endpoints.

## Current Deployment Model

The current cloud deployment model is intentionally minimal:

- Azure Functions Flex Consumption hosts the backend in `poland-central`
- Azure SQL Database uses Microsoft Entra-only authentication and public networking
- Azure Key Vault stores secrets and is read through Key Vault references
- GitHub Actions deploys infrastructure and function code
- EF migrations run through a dedicated admin-only HTTP function inside the Function App
- Local development continues to use `local.settings.json` as the primary configuration source

This model avoids VNet, private endpoints, ACR, and Container Apps for the first cloud slice. The main accepted tradeoff is public-endpoint networking for Azure SQL and Key Vault.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Astro 5 + React islands + Tailwind CSS 4 |
| Backend | C# / .NET 8, Azure Functions (isolated worker) |
| Orchestration | Azure Durable Functions |
| Database | Azure SQL Database |
| LLM | OpenAI API (GPT-4o for synthesis, text-embedding-3-small for clustering) |
| Scraping | Firecrawl API |
| Frontend hosting | Azure Static Web Apps |
| IaC | Bicep |
| CI/CD | GitHub Actions |
| Testing | xUnit (.NET), Vitest (Astro/React), Playwright (E2E) |

## Repository Structure

```
srodkowy/
├── src/
│   ├── functions/                         # .NET solution
│   │   ├── Srodkowy.Functions/            # Azure Functions project
│   │   │   ├── Configuration/             # Options classes, source registry
│   │   │   ├── Ingestion/                 # Manual HTTP-triggered ingestion functions
│   │   │   ├── Models/                    # Source, Article, IngestionRun
│   │   │   ├── Persistence/               # DbContext, design-time factory, EF migrations
│   │   │   ├── Services/                  # Firecrawl client, ingestion service, URL/content helpers
│   │   │   ├── Program.cs                 # Host builder + DI
│   │   │   └── Srodkowy.Functions.csproj
│   │   ├── Srodkowy.Functions.Tests/      # xUnit unit tests
│   │   └── Srodkowy.sln
│   │
│   └── frontend/                          # Planned Astro app
│
├── infra/                                 # Bicep IaC for backend deployment
├── tests/                                 # Planned E2E and integration tests
├── docs/
│   ├── vision_first_product_definition.md
│   └── architecture.md                    # This file
└── AGENTS.md
```

## Pipeline Flow

### Current Phase 1 Flow

- Manual HTTP trigger starts ingestion for all active sources or one specific source
- For each source, the backend scrapes a discovery page with Firecrawl `links` format
- Candidate URLs are normalized, host-filtered, and article-filtered
- Candidate article pages are scraped individually with Firecrawl `markdown` format
- The backend extracts title, plain text, markdown, publish date, and metadata JSON
- Raw articles are deduplicated by URL and stored in `Articles`
- Each run is tracked in `IngestionRuns`
- Requests are processed sequentially and paced to stay within Firecrawl free-plan `/scrape` limits
- Cloud deployments apply EF migrations through a dedicated admin-only HTTP function after code publish

### Target Pipeline

The target pipeline runs every 12 hours as a Durable Functions orchestration.

#### Step 1: Ingestion

- Timer trigger fires every 12h (06:00, 18:00 UTC+1)
- Orchestrator fans out: one activity function per source
- Each activity calls Firecrawl API to scrape the source URL
- Extracts articles: title, content, published date, URL
- Stores raw articles in Azure SQL Database
- Deduplicates by URL

#### Step 2: Clustering

- Generates embeddings for all new articles using OpenAI `text-embedding-3-small`
- Computes cosine similarity between article embeddings
- Groups articles into event clusters using agglomerative clustering
- Filters: keeps only clusters where both left AND right camp sources are represented
- Ranks clusters by: number of sources, camp balance, narrative divergence

#### Step 3: Synthesis

- For each qualifying cluster, sends article texts to GPT-4o with a structured prompt
- Generates:
  - Central synthesis (calm, factual, ~150-300 words in Polish)
  - Markers: phrases in the synthesis that reveal framing differences
  - Left perspective: summary + cited excerpts from left-camp articles
  - Right perspective: summary + cited excerpts from right-camp articles
- Stores as a Story with associated StorySides in Azure SQL Database
- Creates an Edition record linking all stories for this cycle

#### Step 4: Publish

- Marks the edition as `live` in the database
- Triggers GitHub Actions workflow via repository dispatch
- GitHub Actions runs `astro build`, which fetches from the Content API
- Deploys built static files to Azure Static Web Apps
- Azure SWA distributes to global CDN edge nodes
- Previous edition is atomically replaced

## HTTP Endpoints

### Current Endpoints

| Endpoint | Description |
|---|---|
| `POST /api/ingestion/run` | Run ingestion for all active sources |
| `POST /api/ingestion/run/{sourceId}` | Run ingestion for a specific source |
| `POST /api/admin/migrations/apply` | Apply EF Core migrations when `Admin:Migrations:Enabled` is true |

### Target Content API

| Endpoint | Description |
|---|---|
| `GET /api/editions/current` | Current live edition with story summaries |
| `GET /api/editions/{id}` | Specific edition |
| `GET /api/stories/{id}` | Full story: synthesis, markers, both sides, source links |
| `GET /api/sources` | Curated source list with camp assignments |

These target read endpoints will serve two consumers:
1. Astro build process (at build time)
2. React islands (at runtime, for lazy-loaded side panels if needed)

## Database Schema

The target database is Azure SQL Database, with local development using SQL Server Express. The current phase-1 schema is SQL Server-compatible and is managed with EF Core migrations.

### Current Schema

#### Sources
| Column | Type | Description |
|---|---|---|
| Id | uniqueidentifier | PK |
| Name | nvarchar(200) | Display name |
| BaseUrl | nvarchar(500) | Base URL for scraping |
| DiscoveryUrl | nvarchar(500) | Landing page used for link discovery |
| Camp | nvarchar(20) | `left` or `right` |
| Active | bit | Whether to include in scraping |

#### Articles
| Column | Type | Description |
|---|---|---|
| Id | uniqueidentifier | PK |
| SourceId | uniqueidentifier | FK → Sources |
| Title | nvarchar(500) | Article title |
| Url | nvarchar(1000) | Original article URL (unique) |
| ContentMarkdown | nvarchar(max) | Raw article markdown from Firecrawl |
| ContentText | nvarchar(max) | Normalized plain text |
| PublishedAt | datetimeoffset | Article publish date |
| ScrapedAt | datetimeoffset | When we scraped it |
| MetadataJson | nvarchar(max) | Firecrawl metadata payload |

#### IngestionRuns
| Column | Type | Description |
|---|---|---|
| Id | uniqueidentifier | PK |
| StartedAt | datetimeoffset | Run start time |
| CompletedAt | datetimeoffset null | Run completion time |
| Status | nvarchar(40) | `running`, `completed`, `completed_with_errors`, `failed` |
| TriggeredBy | nvarchar(100) | Trigger source identifier |
| SourceCount | int | Number of sources included in the run |
| DiscoveredLinkCount | int | Raw links returned from discovery pages |
| CandidateLinkCount | int | Links kept after filtering |
| ArticleCount | int | Newly inserted articles |
| ErrorSummary | nvarchar(max) null | Aggregated run errors |

### Target Schema

#### Editions
| Column | Type | Description |
|---|---|---|
| id | uniqueidentifier | PK |
| created_at | datetimeoffset | When the pipeline ran |
| published_at | datetimeoffset | When the edition went live |
| status | nvarchar(20) | `building`, `live`, `archived` |
| cycle | nvarchar(20) | `morning` or `evening` |

#### Stories
| Column | Type | Description |
|---|---|---|
| id | uniqueidentifier | PK |
| edition_id | uniqueidentifier | FK → editions |
| rank | int | Display order on homepage |
| headline | nvarchar(500) | Generated headline |
| synthesis | nvarchar(max) | Central synthesis text |
| markers | nvarchar(max) | JSON array of marker objects with offsets and metadata |

#### StorySides
| Column | Type | Description |
|---|---|---|
| id | uniqueidentifier | PK |
| story_id | uniqueidentifier | FK → stories |
| camp | nvarchar(20) | `left` or `right` |
| summary | nvarchar(max) | Camp-specific narrative summary |
| excerpts | nvarchar(max) | JSON array of {text, source_name, source_url} |

#### StoryArticles
| Column | Type | Description |
|---|---|---|
| story_id | uniqueidentifier | FK → stories |
| article_id | uniqueidentifier | FK → articles |

Exact similarity search can be implemented with Azure SQL vector functions for the curated MVP dataset. Approximate vector indexing exists in Azure SQL, but it is not required for the first backend slice.

## Implementation Phasing

1. **Phase 1: Raw ingestion** — Implemented locally with Azure Functions, SQL Server Express, EF Core migrations, seeded sources, Firecrawl scraping, and deduplicated raw article persistence.
2. **Phase 2: Embeddings and clustering** — Generate embeddings with OpenAI, store them in Azure SQL `vector(1536)` columns, and form cross-camp candidate story clusters.
3. **Phase 3: Synthesis and content API** — Generate story syntheses, persist editions, stories, and story sides, and expose the read API for the frontend build.

## Key Design Decisions

1. **Pure SSG frontend** — Pages are pre-built every 12h. Zero runtime compute cost. Instant loads via CDN.

2. **React islands for interactivity** — Only marker tooltips and side-panel reveals are hydrated. Everything else is static HTML + Tailwind.

3. **Durable Functions orchestration** — The 4-step pipeline uses fan-out/fan-in for parallel scraping, automatic retries, and status tracking.

4. **Firecrawl for scraping** — Avoids maintaining browser infrastructure. In phase 1 it is called sequentially and paced to fit Firecrawl free-plan `/scrape` limits.

5. **Azure SQL vector support for embeddings** — Keeps operational data and embeddings in one SQL store. No separate vector database is required for MVP.

6. **Curated source list as configuration** — Source-to-camp mapping is a foundational editorial decision, stored as seed data and versioned in code.

7. **Polish content, English code** — All UI text and generated content in Polish. Code, comments, docs, and variable names in English.

8. **Migration endpoint instead of a separate runner** — The first Azure deployment slice applies EF migrations through a function-key-protected admin endpoint, which reduces infrastructure components at the cost of granting the Function App identity `db_ddladmin` permissions.

## Environment Configuration

| Setting | Description |
|---|---|
| `OpenAi:ApiKey` | OpenAI API secret key |
| `OpenAi:ChatModel` | Chat model name (default: `gpt-4o`) |
| `OpenAi:EmbeddingModel` | Embedding model name (default: `text-embedding-3-small`) |
| `Firecrawl:ApiKey` | Firecrawl API key |
| `Firecrawl:TimeoutSeconds` | Per-request timeout budget for Firecrawl calls |
| `Firecrawl:RequestsPerMinute` | Firecrawl `/scrape` pacing budget |
| `Database:ConnectionString` | SQL Server / Azure SQL connection string |
| `Admin:Migrations:Enabled` | Enables the admin migration HTTP endpoint |
| `Ingestion:MaxCandidateLinksPerSource` | Max filtered article links per source |
| `Ingestion:MaxArticlesPerSource` | Max article page scrapes per source |
| `Ingestion:MinContentLength` | Minimum normalized article text length |
| `Pipeline:CronSchedule` | Timer trigger CRON (default: `0 0 5,17 * * *`) |
| `GitHub:Token` | For triggering frontend rebuild |
| `GitHub:RepoOwner` | Repository owner |
| `GitHub:RepoName` | Repository name |

Local development can use SQL Server Express with Integrated Security, for example: `Server=.\SQLEXPRESS;Database=Srodkowy;Integrated Security=True;TrustServerCertificate=True;`.

In Azure, the backend uses the same logical keys, but configuration is supplied through Function App settings, with secrets coming from Key Vault references and `Database:ConnectionString` using managed identity authentication.
