# System Architecture

*Last Updated: 2026-03-09*
*Confidence Score: 85%*

---

## High-Level Landscape

- **Architecture Type**: Clean Architecture — strict three-layer layered monolith (.NET 10)
- **Primary Tech Stack**: ASP.NET Core 10, Blazor Server, Entity Framework Core 10, Semantic Kernel 1.70, MudBlazor 8, SQLite / SQL Server, ModelContextProtocol.AspNetCore 0.6
- **Entry Points**:
  - Blazor Server UI — browser navigation (`/`, `/login`, `/projects`, `/settings`, `/mcp-config`)
  - MCP HTTP endpoint — AI agents (`POST /mcp`) secured by Bearer token

---

## System Topology

### Components

1. **Mjm.LocalDocs.Core** (`src/Mjm.LocalDocs.Core/`)
   - Purpose: Domain rules, models, and interfaces — the stable inner ring
   - Pattern: Pure domain layer; zero external package dependencies (only `Microsoft.Extensions.*` abstractions + `Microsoft.Extensions.VectorData.Abstractions`)
   - Sub-folders:
     - `Abstractions/` — all interfaces
     - `Models/` — immutable domain models (`Project`, `Document`, `DocumentChunk`, `SearchResult`, `ApiToken`, option models)
     - `Services/` — `DocumentService`, `ApiTokenService`
     - `Configuration/` — `LocalDocsOptions` and nested config classes/enums
     - `DependencyInjection/` — `AddLocalDocsCoreServices()`

2. **Mjm.LocalDocs.Infrastructure** (`src/Mjm.LocalDocs.Infrastructure/`)
   - Purpose: Concrete implementations of every Core interface; pluggable via strategy
   - Pattern: Infrastructure ring; references Core only; never referenced by Core
   - Sub-folders:
     - `Documents/` — `PlainTextDocumentReader`, `MarkdownDocumentReader`, `PdfDocumentReader` (PdfPig), `WordDocumentReader` (NPOI), `CompositeDocumentReader`, `SimpleDocumentProcessor`
     - `Embeddings/` — `FakeEmbeddingService`, `SemanticKernelEmbeddingService` (wraps `IEmbeddingGenerator` from `Microsoft.Extensions.AI`)
     - `FileStorage/` — `DatabaseDocumentFileStorage`, `FileSystemDocumentFileStorage`, `AzureBlobDocumentFileStorage`
     - `Persistence/` — `LocalDocsDbContext` (EF Core), `SqliteVectorStore` (sqlite-vec), `SqlServerVectorStore`, `DesignTimeDbContextFactory`
     - `Persistence/Repositories/` — `EfCoreProjectRepository`, `EfCoreDocumentRepository`, `EfCoreApiTokenRepository`
     - `Persistence/Entities/` — EF entity classes (separate from domain models)
     - `VectorStore/` — `InMemoryVectorStore`, `InMemoryProjectRepository`, `InMemoryDocumentRepository`, `InMemoryApiTokenRepository`
     - `VectorStore/Hnsw/` — `HnswVectorStore` (file-based approximate nearest neighbor)
     - `DependencyInjection/` — `AddLocalDocsInfrastructure()`, `AddLocalDocsFakeInfrastructure()`, `AddLocalDocsSqliteInfrastructure()`

3. **Mjm.LocalDocs.Server** (`src/Mjm.LocalDocs.Server/`)
   - Purpose: Composition root — ASP.NET Core host, Blazor UI, MCP tools
   - Pattern: Outer ring; references both Core and Infrastructure; hosts all DI wiring in `Program.cs`
   - Sub-folders:
     - `McpTools/` — one sealed class per tool group (see MCP Tools section)
     - `Middleware/` — `McpAuthenticationMiddleware`
     - `Components/Pages/` — Blazor pages: `Home`, `Login`, `NotFound`, `Error`, `McpConfig`, `Projects/`, `Settings/`
     - `Components/Layout/` — layout components
     - `Components/Shared/` — shared Blazor components

4. **Mjm.LocalDocs.Tests** (`tests/Mjm.LocalDocs.Tests/`)
   - Purpose: xUnit unit tests; mirrors `src/` structure
   - Pattern: Tests reference Core and Infrastructure only; never Server
   - Test folders: `Documents/`, `FileStorage/`, `Services/`, `VectorStore/`

### Dependency Graph

```
Mjm.LocalDocs.Server
    ├── Mjm.LocalDocs.Core
    └── Mjm.LocalDocs.Infrastructure
            └── Mjm.LocalDocs.Core

Mjm.LocalDocs.Tests
    ├── Mjm.LocalDocs.Core
    └── Mjm.LocalDocs.Infrastructure
```

Core has **zero** references to Infrastructure or Server. Infrastructure has **zero** references to Server.

### Data Flow

#### Semantic Search

```
AI Agent → POST /mcp (Bearer token)
    → McpAuthenticationMiddleware (validates hashed token via ApiTokenService)
    → SearchDocsTool.SearchDocsAsync
    → DocumentService.SearchAsync
    → IEmbeddingService.GenerateEmbeddingAsync   ← Fake | OpenAI | AzureOpenAI | Ollama
    → IVectorStore.SearchAsync                   ← InMemory | SQLite | HNSW | SQL Server
    → IDocumentRepository.GetChunksByIdsAsync    ← EF Core
    → SearchResult[]
    → Formatted Markdown string response
```

#### Add Document

```
AI Agent → POST /mcp
    → AddDocumentTool.AddDocumentAsync
    → CompositeDocumentReader.ReadAsync          ← .txt | .md | .pdf | .docx
    → DocumentService.AddDocumentAsync
    → IDocumentFileStorage.SaveFileAsync         ← Database | FileSystem | AzureBlob
    → IDocumentRepository.AddDocumentAsync       ← EF Core
    → IDocumentProcessor.ProcessAsync            ← sliding window chunking
    → IEmbeddingService.GenerateEmbeddingsAsync
    → IVectorStore.UpsertBatchAsync
```

#### Blazor UI

```
Browser → Blazor Server (Cookie auth)
    → MudBlazor components
    → IProjectRepository / DocumentService / ApiTokenService (Scoped DI)
    → EF Core → SQLite / SQL Server
```

---

## MCP Tools

All tool classes are decorated with `[McpServerToolType]`. Registered in `Program.cs` via `builder.Services.AddMcpServer().WithTools<T>()`.

| Class | Tool Name(s) | Description |
|-------|-------------|-------------|
| `SearchDocsTool` | `search_docs` | Semantic search with optional project filter and limit |
| `AddDocumentTool` | `add_document` | Ingest .txt/.md/.pdf/.docx with chunking + embedding |
| `DocumentTools` | `get_document`, `delete_document`, `list_documents` | Document CRUD |
| `UpdateDocumentTool` | `update_document` | Replace document content (creates new version, supersedes old) |
| `ProjectTools` | `create_project`, `get_project`, `delete_project` | Project management |
| `ListProjectsTool` | `list_projects` | List all projects |
| `ListCollectionsTool` | *(file present, not wired in Program.cs)* | — |

Error handling: all tool methods wrap logic in `try/catch` and return `"Error: {ex.Message}"` — exceptions never propagate to the MCP framework.

---

## Patterns & Standards

### Storage Strategy (pluggable via `LocalDocs:Storage:Provider`)

| Provider | `IDocumentRepository` | `IVectorStore` | When to Use |
|----------|----------------------|----------------|-------------|
| `InMemory` | `InMemoryDocumentRepository` | `InMemoryVectorStore` | Dev / tests |
| `Sqlite` | `EfCoreDocumentRepository` | `SqliteVectorStore` (sqlite-vec, brute-force cosine) | Default production |
| `SqliteHnsw` | `EfCoreDocumentRepository` | `HnswVectorStore` (file-based ANN index) | High-performance local |
| `SqlServer` | `EfCoreDocumentRepository` | `SqlServerVectorStore` (native SQL Server vectors) | Enterprise production |

`IDocumentRepository` and `IVectorStore` are always registered separately — relational metadata and vector embeddings are kept in different stores.

### Embedding Strategy (pluggable via `LocalDocs:Embeddings:Provider`)

| Provider | Class | Notes |
|----------|-------|-------|
| `Fake` | `FakeEmbeddingService` | Returns deterministic random vectors; dev/test only |
| `OpenAI` | `SemanticKernelEmbeddingService` | Wraps `OpenAIClient`; requires API key |
| `AzureOpenAI` | `SemanticKernelEmbeddingService` | Requires endpoint + API key |
| `Ollama` | `SemanticKernelEmbeddingService` | Local LLM (default model: `nomic-embed-text`) |
| `LlamaCpp` | `LlamaCppEmbeddingService` | Local llama.cpp via `llama-server.exe`; fully offline, native `/embedding` API |

Default vector dimension: 1536. Configurable via `LocalDocs:Embeddings:Dimension`. For `LlamaCpp` with nano models, use 768.

### File Storage Strategy (pluggable via `LocalDocs:FileStorage:Provider`)

| Provider | Class | Notes |
|----------|-------|-------|
| `Database` | `DatabaseDocumentFileStorage` | Binary stored inline in `Documents.FileContent` column |
| `FileSystem` | `FileSystemDocumentFileStorage` | Stored under configurable `BasePath` |
| `AzureBlob` | `AzureBlobDocumentFileStorage` | Azure Storage SDK |

### Document Chunking

- `SimpleDocumentProcessor` — sliding window over extracted text
- `MaxChunkSize`: 3000 chars (default), `OverlapSize`: 300 chars (default)
- Chunk IDs: `{documentId}_chunk_{index}` (enables prefix-based deletion)

### Communication / Transport

- MCP: HTTP Streaming (`/mcp`) via `ModelContextProtocol.AspNetCore`
- Blazor UI: WebSocket (SignalR, server-side Blazor)
- No inter-service communication — this is a monolith

### Repository Pattern

All data access goes through interfaces:
- `IProjectRepository` — project CRUD
- `IDocumentRepository` — document + chunk metadata CRUD
- `IVectorStore` — embedding upsert / cosine search
- `IApiTokenRepository` — token lifecycle

EF entities live in `Persistence/Entities/`; domain models live in `Core/Models/`. Mapping is done via private `MapToModel()` methods inside repository implementations — no AutoMapper.

All EF read queries use `.AsNoTracking()`.

### Authentication & Security

**Blazor UI**: Cookie authentication (`CookieAuthenticationDefaults`), username/password from `LocalDocs:Authentication` config section. Login via `/login`, expiry 7 days sliding.

**MCP Endpoint**: Bearer token middleware (`McpAuthenticationMiddleware`) runs before MCP routing. Tokens are generated with `RandomNumberGenerator.GetBytes(32)`, stored as SHA-256 hashes (only the prefix is stored in plaintext for display). Tokens can expire and be revoked. Authentication can be disabled via `LocalDocs:Mcp:RequireAuthentication = false`.

### Logging

Serilog with dual sinks: Console + rolling File (`Logs/localdocs-{date}.log`, 5-day retention). Configured entirely from `appsettings.json`. `ModelContextProtocol` logged at `Debug` level; `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` at `Warning`.

### Error Handling

- MCP tools: catch all exceptions → return `"Error: {message}"` string
- Services: `ArgumentException` for invalid input (with `nameof`), `InvalidOperationException` for domain violations
- "Not found": return `null` or `false` — no exceptions
- Empty collections: return `[]`

### Configuration

All settings under `"LocalDocs"` section in `appsettings.json`. Options classes:
- `LocalDocsOptions` → `EmbeddingsOptions`, `StorageOptions`, `ChunkingOptions`, `FileStorageOptions`
- `AuthenticationOptions` (section `"Authentication"`)
- `McpOptions` (section `"Mcp"`)
- `ServerOptions` (section `"Server"`, e.g., `UseHttps`)

---

## Database Schema

Managed by EF Core with Code-First migrations (table `__EFMigrationsHistory`). Supports both SQLite and SQL Server via provider-agnostic `LocalDocsDbContext`.

### Tables (via EF Core)

| Table | Key Columns | Notes |
|-------|-------------|-------|
| `Projects` | `Id` (PK), `Name` (unique), `Description`, `CreatedAt` | Cascade-deletes Documents |
| `Documents` | `Id` (PK), `ProjectId` (FK), `FileName`, `FileExtension`, `FileContent`, `FileStorageLocation`, `ExtractedText`, `ContentHash`, `VersionNumber`, `ParentDocumentId`, `IsSuperseded` | `FileContent` null when using external file storage |
| `DocumentChunks` | `Id` (PK, format `{docId}_chunk_{idx}`), `DocumentId` (FK), `Content`, `Title`, `CreatedAt` | |
| `ApiTokens` | `Id` (PK), `Name`, `TokenHash`, `TokenPrefix`, `CreatedAt`, `ExpiresAt`, `LastUsedAt`, `IsRevoked` | Plain-text token never persisted |

### Vector Storage (separate from EF)

| Store | Table / File | Notes |
|-------|-------------|-------|
| `SqliteVectorStore` | `chunk_embeddings` (sqlite-vec virtual table) | Brute-force cosine similarity |
| `HnswVectorStore` | `.hnsw` index file on disk | Approximate nearest neighbor |
| `SqlServerVectorStore` | `chunk_embeddings` table | Native SQL Server vector functions |
| `InMemoryVectorStore` | In-process `Dictionary<>` | Dev/test only, not persisted |

---

## Boundaries & Constraints

### NEVER (Anti-Patterns)

- ❌ Never reference `Mjm.LocalDocs.Infrastructure` from `Mjm.LocalDocs.Core`
- ❌ Never reference `Mjm.LocalDocs.Server` from `Core` or `Infrastructure`
- ❌ Never add external NuGet packages to `Mjm.LocalDocs.Core` (only `Microsoft.Extensions.*` abstractions allowed)
- ❌ Never use `.Result` or `.Wait()` on async methods
- ❌ Never bypass `DocumentService` to access `IDocumentRepository` or `IVectorStore` directly from MCP tools
- ❌ Never use AutoMapper — use manual `MapToModel()` methods
- ❌ Never skip `.AsNoTracking()` on EF read-only queries
- ❌ Never let exceptions propagate out of MCP tool methods

### ALWAYS (Required Patterns)

- ✅ All async methods have the `Async` suffix and accept `CancellationToken cancellationToken = default`
- ✅ `IReadOnlyList<T>` as return type from service/repository methods
- ✅ All implementation classes are `sealed`
- ✅ Domain model properties use `required` + `init` (EF entities use `get; set;`)
- ✅ Chunk IDs formatted as `{documentId}_chunk_{index}` to enable prefix-based bulk deletion
- ✅ File-scoped namespaces throughout
- ✅ `Nullable` enabled — guard with `is null` / `string.IsNullOrEmpty()`
- ✅ Register infrastructure via `DependencyInjection/` extension methods

---

## File Organization

### Directory Structure

```
mjm.local.docs/
├── src/
│   ├── Mjm.LocalDocs.Core/           # Domain models, interfaces (inner ring)
│   │   ├── Abstractions/             # IDocumentRepository, IVectorStore, IEmbeddingService,
│   │   │                             # IDocumentFileStorage, IDocumentProcessor, IDocumentReader,
│   │   │                             # IProjectRepository, IApiTokenRepository
│   │   ├── Configuration/            # LocalDocsOptions + nested config classes/enums
│   │   ├── Models/                   # Document, DocumentChunk, Project, SearchResult,
│   │   │                             # ApiToken, AuthenticationOptions, McpOptions, ServerOptions
│   │   ├── Services/                 # DocumentService, ApiTokenService
│   │   └── DependencyInjection/      # AddLocalDocsCoreServices()
│   │
│   ├── Mjm.LocalDocs.Infrastructure/ # Implementations (middle ring)
│   │   ├── Documents/                # Document readers (txt, md, pdf, docx) + processor
│   │   ├── Embeddings/               # FakeEmbeddingService, SemanticKernelEmbeddingService
│   │   ├── FileStorage/              # Database, FileSystem, AzureBlob providers
│   │   ├── Persistence/              # EF Core DbContext, migrations, Sqlite/SqlServer vector stores
│   │   │   ├── Entities/             # EF entity classes (mapped to/from domain models)
│   │   │   ├── Migrations/           # EF Core migrations
│   │   │   └── Repositories/        # EfCore implementations of IProjectRepository etc.
│   │   ├── VectorStore/              # InMemory implementations
│   │   │   └── Hnsw/                 # HnswVectorStore
│   │   └── DependencyInjection/      # AddLocalDocsInfrastructure() + variants
│   │
│   └── Mjm.LocalDocs.Server/         # Composition root (outer ring)
│       ├── Components/               # Blazor pages and layout (MudBlazor)
│       │   └── Pages/                # Home, Login, Projects/, Settings/, McpConfig
│       ├── McpTools/                 # One class per MCP tool group
│       ├── Middleware/               # McpAuthenticationMiddleware
│       └── Program.cs                # All DI wiring, pipeline, startup
│
└── tests/
    └── Mjm.LocalDocs.Tests/          # xUnit unit tests (mirrors src/)
        ├── Documents/
        ├── FileStorage/
        ├── Services/
        └── VectorStore/
```

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Interfaces | `I` prefix | `IDocumentRepository` |
| Async methods | `Async` suffix | `SearchAsync` |
| Private fields | `_camelCase` | `_repository`, `_sut` |
| Parameters / locals | `camelCase` | `queryEmbedding` |
| Properties / types | `PascalCase` | `DocumentId`, `DocumentChunk` |
| Config section constant | `const string SectionName` | `"LocalDocs"` |
| Test SUT variable | `_sut` | |
| Test helper factories | `CreateTest{Entity}()` | `CreateTestDocument()` |
| Test methods | `Method_Scenario_Expected` | `SearchAsync_WithNoResults_ReturnsEmptyList` |

---

## Change History

### 2026-03-09 — Added LlamaCpp Embedding Provider

- Added `LlamaCpp` provider to `EmbeddingProvider` enum (Core)
- Created `LlamaCppEmbeddingService` — launches `llama-server.exe`, manages process lifecycle, implements native `/embedding` API (2026-03-09: Fixed to use correct endpoint)
- Updated DI configuration to support LlamaCpp provider selection
- Updated appsettings.json to use LlamaCpp with `v5-nano-retrieval-Q8_0.gguf` model (768 dimensions)
- Enables fully offline, local embedding generation with zero external dependencies
- Confidence: 90%

### 2026-03-09 — Initial Architecture Extraction

- Analyzed full solution: 3 projects + 1 test project
- Mapped Clean Architecture boundaries (Core → Infrastructure → Server)
- Documented all pluggable strategies: storage, embedding, file storage providers
- Documented MCP tool registration and Bearer token auth flow
- Confidence: 85%

---

## Confidence Report

- **Score**: 85%
- **Achieved**:
  - ✅ Entry points mapped (MCP `/mcp`, Blazor UI)
  - ✅ Data persistence patterns identified (EF Core + separate vector store)
  - ✅ Dependency graph validated (strict one-way references)
  - ✅ All pluggable strategies documented
- **Known Unknowns**:
  - No CI/CD pipeline files found (no `.yml`, `Dockerfile`, or `docker-compose` in workspace). Deployment intent is partially described in `STARTUP.md` and `appsettings.SqlServer.json` but not fully explored.
  - `ListCollectionsTool.cs` exists in `McpTools/` but is not wired into `Program.cs` — its intended purpose is undocumented.
  - Blazor page internals (component state management, form validation) were not read in depth.
  - HNSW index configuration details not fully explored.
