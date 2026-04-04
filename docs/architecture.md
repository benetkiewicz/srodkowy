# Środkowy Portal — Architecture

## Overview

Środkowy is a Polish news portal built around narrative comparison. It scrapes articles from curated left-leaning and right-leaning Polish media sources every 12 hours, clusters them by event, generates a neutral central synthesis with annotated markers via LLM, and publishes a static site showing how each ideological camp frames the same story.

## Current Status

Stage 0 is verified in Azure dev, and the next backend slice now includes article preparation.

- Azure Functions isolated worker solution runs on `.NET 10` under `src/functions`
- EF Core 10 migrations are wired for SQL Server 2025 / Azure SQL
- OpenTelemetry exports host and worker telemetry to Azure Monitor
- Curated source list seeded into the database
- Firecrawl-backed raw ingestion working through manual HTTP triggers
- Raw articles and ingestion runs persisted in SQL Server Developer 2025 locally and Azure SQL in cloud
- Manual article-cleanup and embedding-preparation endpoints are implemented in the backend
- Bicep and GitHub Actions scaffolding added for Azure deployment into `rg-srodkowy-pc`

The following remain target-state only for now: clustering, synthesis, edition publishing, Durable orchestration, and read-side content API endpoints.

## Current Deployment Model

The current cloud deployment model is intentionally minimal:

- Azure Functions Flex Consumption hosts the backend in `poland-central`
- Azure SQL Database uses Microsoft Entra-only authentication and public networking
- Azure Key Vault stores secrets and is read through Key Vault references
- GitHub Actions deploys infrastructure and function code
- EF migrations run through a dedicated admin-only HTTP function inside the Function App
- Local development continues to use `local.settings.json` as the primary configuration source

This model avoids VNet, private endpoints, ACR, and Container Apps for the first cloud slice.

### Accepted Tradeoffs

- No VNet or private endpoints in the first Azure slice
- Azure SQL uses a public endpoint with `Allow Azure services and resources to access this server`
- Azure Key Vault uses a public endpoint with RBAC-based secret access
- The Function App identity holds `db_ddladmin` so migrations can run inside the app
- EF migrations are triggered through a function-key-protected HTTP endpoint instead of a separate migration runner

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Astro 5 + React islands + Tailwind CSS 4 |
| Backend | C# / .NET 10, Azure Functions (isolated worker) |
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

### Current Flow

- Manual HTTP trigger starts ingestion for all active sources or one specific source
- For each source, the backend scrapes a discovery page with Firecrawl `links` format
- Candidate URLs are normalized, host-filtered, and article-filtered
- Candidate article pages are scraped individually with Firecrawl `markdown` format
- The backend extracts title, plain text, markdown, publish date, and metadata JSON
- Raw articles are deduplicated by URL and stored in `Articles`
- Raw article bodies then pass through an article-preparation stage:
  - all scraped articles go through LLM-first cleanup/extraction
  - a lightweight deterministic normalization pass removes exact duplicate blocks and normalizes input before the LLM call
  - cleanup status uses `pending/running/completed/failed/stale` so articles are not sent twice for the same content version
  - embeddings are generated from `Title + CleanedContentText`, not raw scraped text
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

#### Step 2: Article Preparation

- Runs a lightweight normalization pass on raw markdown
- Sends every scraped article through an LLM cleanup/extraction prompt keyed by the article title
- Stores `CleanedContentText`, cleanup status, flags, quality score, and non-article classification hints on the `Articles` row
- Uses `running` claim semantics to avoid duplicate cleanup or embedding work in overlapping runs
- Generates embeddings for prepared articles using OpenAI `text-embedding-3-small`

#### Step 3: Clustering

- Computes cosine similarity between article embeddings
- Groups articles into event clusters using agglomerative clustering
- Filters: keeps only clusters where both left AND right camp sources are represented
- Ranks clusters by: number of sources, camp balance, narrative divergence

#### Step 4: Synthesis

- For each qualifying cluster, sends article texts to GPT-4o with a structured prompt
- Generates:
  - Central synthesis (calm, factual, ~150-300 words in Polish)
  - Markers: phrases in the synthesis that reveal framing differences
  - Left perspective: summary + cited excerpts from left-camp articles
  - Right perspective: summary + cited excerpts from right-camp articles
- Stores as a Story with associated StorySides in Azure SQL Database
- Creates an Edition record linking all stories for this cycle

#### Step 5: Publish

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
| `POST /api/ops/articles/cleanup/run` | Run LLM-first article cleanup / preparation |
| `POST /api/ops/articles/embeddings/run` | Generate embeddings from prepared article text |
| `POST /api/ops/migrations/apply` | Apply EF Core migrations when `Admin:Migrations:Enabled` is true |

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

The target database is Azure SQL Database, with local development using SQL Server Developer 2025. The current schema is SQL Server 2025-compatible and is managed with EF Core migrations.

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
| CleanedContentText | nvarchar(max) null | LLM-cleaned article body used by downstream AI stages |
| CleanupStatus | nvarchar(30) | `pending`, `running`, `completed`, `failed`, `stale` |
| CleanedAt | datetimeoffset null | When cleanup last ran |
| CleanupStartedAt | datetimeoffset null | When the current/last cleanup claim started |
| CleanupRunId | uniqueidentifier null | Claim token for the cleanup batch that owns the row |
| CleanupProcessor | nvarchar(100) null | Cleanup pipeline version and model id |
| CleanupError | nvarchar(max) null | Cleanup failure detail |
| CleanupInputHash | nvarchar(64) null | Hash of title + raw markdown for rerun detection |
| CleanupFlagsJson | nvarchar(max) | JSON array of cleanup / classification flags |
| QualityScore | int | Cleanup validation confidence score |
| NeedsReview | bit | Whether the cleanup result should be reviewed before relying on it |
| IsProbablyNonArticle | bit | Whether the page looks like low-value or non-article content |
| Embedding | vector(1536) null | Native Azure SQL / SQL Server vector for clustering |
| EmbeddingModel | nvarchar(100) null | Embedding model id |
| EmbeddingStatus | nvarchar(30) | `pending`, `running`, `completed`, `failed`, `stale` |
| EmbeddedAt | datetimeoffset null | When embedding last ran |
| EmbeddingStartedAt | datetimeoffset null | When the current/last embedding claim started |
| EmbeddingRunId | uniqueidentifier null | Claim token for the embedding batch that owns the row |
| EmbeddingError | nvarchar(max) null | Embedding failure detail |
| EmbeddingTextHash | nvarchar(64) null | Hash of title + cleaned text for rerun detection |
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

1. **Phase 1: Raw ingestion** — Implemented with Azure Functions, SQL Server / Azure SQL, EF Core migrations, seeded sources, Firecrawl scraping, and deduplicated raw article persistence.
2. **Phase 2: Article preparation and embeddings** — Run all scraped articles through LLM-first cleanup/extraction, classify probable non-articles, and persist native Azure SQL `vector(1536)` embeddings from cleaned text.
3. **Phase 3: Clustering** — Form cross-camp candidate story clusters from prepared article embeddings.
4. **Phase 4: Synthesis and content API** — Generate story syntheses, persist editions, stories, and story sides, and expose the read API for the frontend build.

## Key Design Decisions

1. **Pure SSG frontend** — Pages are pre-built every 12h. Zero runtime compute cost. Instant loads via CDN.

2. **React islands for interactivity** — Only marker tooltips and side-panel reveals are hydrated. Everything else is static HTML + Tailwind.

3. **Durable Functions orchestration** — The 5-step pipeline uses fan-out/fan-in for parallel scraping, preparation, clustering, synthesis, and publishing with retries and status tracking.

4. **Firecrawl for scraping** — Avoids maintaining browser infrastructure. In phase 1 it is called sequentially and paced to fit Firecrawl free-plan `/scrape` limits.

5. **Azure SQL vector support for embeddings** — Keeps operational data and embeddings in one SQL store. No separate vector database is required for MVP.

6. **Curated source list as configuration** — Source-to-camp mapping is a foundational editorial decision, stored as seed data and versioned in code.

7. **Polish content, English code** — All UI text and generated content in Polish. Code, comments, docs, and variable names in English.

8. **Migration endpoint instead of a separate runner** — The first Azure deployment slice applies EF migrations through a function-key-protected admin endpoint, which reduces infrastructure components at the cost of granting the Function App identity `db_ddladmin` permissions.

## Environment Configuration

| Setting | Description |
|---|---|
| `OpenAi:ApiKey` | OpenAI API secret key |
| `OpenAi:CleanupModel` | Cleanup/extraction model name (default: `gpt-4o`) |
| `OpenAi:ChatModel` | Chat model name (default: `gpt-4o`) |
| `OpenAi:EmbeddingModel` | Embedding model name (default: `text-embedding-3-small`) |
| `Firecrawl:ApiKey` | Firecrawl API key |
| `Firecrawl:TimeoutSeconds` | Per-request timeout budget for Firecrawl calls |
| `Firecrawl:RequestsPerMinute` | Firecrawl `/scrape` pacing budget |
| `Database:ConnectionString` | SQL Server / Azure SQL connection string |
| `Admin:Migrations:Enabled` | Enables the admin migration HTTP endpoint |
| `Ingestion:MaxCandidateLinksPerSource` | Max filtered article links per source |
| `Ingestion:MaxArticlesPerSource` | Max article page scrapes per source |
| `Cleanup:BatchSize` | Max articles to clean in one manual run |
| `Cleanup:LookbackHours` | Article lookback window for cleanup selection |
| `Cleanup:MaxInputCharacters` | Max normalized markdown length sent to LLM cleanup |
| `Cleanup:MinCleanedLength` | Minimum cleaned-body length before the output is flagged for review |
| `Embedding:BatchSize` | Max articles to embed in one manual run |
| `Embedding:LookbackHours` | Article lookback window for embedding selection |
| `Embedding:MaxInputCharacters` | Max cleaned-text input length used for embedding generation |
| `Pipeline:CronSchedule` | Timer trigger CRON (default: `0 0 5,17 * * *`) |
| `GitHub:Token` | For triggering frontend rebuild |
| `GitHub:RepoOwner` | Repository owner |
| `GitHub:RepoName` | Repository name |

Local development can use SQL Server Developer 2025 with Integrated Security, for example: `Server=localhost\MSSQLSERVER;Database=Srodkowy;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=Srodkowy.Functions;`.

In Azure, the backend uses the same logical keys, but configuration is supplied through Function App settings, with secrets coming from Key Vault references and `Database:ConnectionString` using managed identity authentication.
