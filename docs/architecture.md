# Architecture

Straumr is a two-project .NET solution:

- `Straumr.Cli`: Spectre.Console command-line interface and command orchestration
- `Straumr.Core`: storage, domain models, HTTP execution, auth, and secret resolution

## High-Level Runtime Flow

On startup, `Program.cs`:

1. handles the global `--no-color` switch
2. creates the service collection
3. instantiates `StraumrFileService`
4. loads persisted options through `StraumrOptionsService`
5. registers the workspace, request, auth, and secret services
6. configures the Spectre command tree
7. runs the command app

All stateful behavior flows through the service interfaces in `Straumr.Core.Services.Interfaces`.

## Solution Structure

### `Straumr.Cli`

Primary responsibilities:

- command registration
- interactive prompt UX
- human-readable and JSON output formatting
- editor launching and temp-file workflows
- shell completion installation and query handling

Key areas:

- `Program.cs`: DI setup and command tree
- `Commands/*`: one command class per CLI action
- `Helpers/*`: request/auth prompt helpers and formatting helpers
- `Console/*`: escape-aware prompt wrapper
- `Infrastructure/*`: Spectre type registrar and JSON DTO context
- `Models/*`: CLI-specific result DTOs for JSON output

### `Straumr.Core`

Primary responsibilities:

- model definitions
- JSON serialization setup
- persistence
- workspace registry and import/export
- request loading, send-time resolution, and HTTP dispatch
- auth token fetch/refresh/custom-value extraction
- secret storage and lookup

Key areas:

- `Models/*`: persisted domain objects
- `Services/*`: application behavior
- `Extensions/*`: HTTP request construction and misc helpers
- `Configuration/StraumrJsonContext.cs`: source-generated JSON metadata
- `Enums/*`: persisted enums and error categories
- `Exceptions/StraumrException.cs`: domain exception wrapper

## Service Responsibilities

### `StraumrFileService`

Low-level file persistence helper.

Responsibilities:

- serialize and deserialize JSON with source-generated metadata
- ensure parent directories exist
- update `Modified` on write
- update `LastAccessed` on read or explicit stamp

This service is intentionally generic. It does not understand workspace semantics.

### `StraumrOptionsService`

Owns `~/.straumr/options.json`.

Responsibilities:

- create the `~/.straumr` directory on first run
- load options at process start
- persist option mutations

### `StraumrWorkspaceService`

Owns the workspace registry and workspace package operations.

Responsibilities:

- create, activate, copy, delete workspaces
- resolve workspaces by ID or name
- import/export `.straumrpak`
- prepare/apply editor-based workspace edits

Notable implementation detail:

- import/export is zip-based and uses a `.pak` metadata file for stable workspace identity and display name

### `StraumrRequestService`

Owns request CRUD and request sending.

Responsibilities:

- create, update, delete, and load requests in the current workspace
- resolve secret placeholders in request and auth fields
- build and send `HttpRequestMessage`
- manage custom `HttpClientHandler` options for insecure TLS and redirect following
- stamp `LastAccessed` values for touched entities

This is the main orchestration service at send time.

### `StraumrAuthService`

Owns auth CRUD and runtime auth material generation.

Responsibilities:

- create, update, delete, and list workspace auth definitions
- fetch OAuth tokens for supported grant types
- refresh expired OAuth tokens
- execute custom auth bootstrap requests
- cache custom auth extracted values

Authorization Code flow uses the local system browser and an `HttpListener` callback endpoint.

### `StraumrSecretService`

Owns global secret CRUD and lookup.

Responsibilities:

- store secrets under the global secret root
- resolve by ID or name
- enforce global secret-name uniqueness
- support temp-file editing

## Command-Layer Pattern

Most command classes follow one of three patterns:

- thin wrapper around a service call
- interactive menu command backed by helper state objects
- editor-backed temp-file edit command

Examples:

- `WorkspaceCreateCommand`: thin wrapper
- `RequestCreateCommand`: thin wrapper plus interactive/editor/inline modes
- `AuthEditCommand`: interactive or editor-backed editing
- `SecretEditCommand`: editor-backed editing only

## Request Send Pipeline

`RequestSendCommand` is mostly presentation logic. `StraumrRequestService.SendAsync` handles the actual pipeline:

1. load linked auth, if any
2. resolve request secrets
3. resolve auth secrets
4. auto-refresh OAuth or custom auth value if configured
5. create an `HttpClient` with optional custom handler
6. send the request and collect duration/content/header metadata
7. retry once for custom auth on `401` when auto-renew is enabled
8. stamp access times

Presentation then branches into:

- plain body output
- pretty Spectre panels/tables
- verbose request/response dumps
- JSON envelope mode

## Prompt and Editor UX

Interactive prompts use `EscapeCancellableConsole` and `PromptHelpers`.

Capabilities:

- escape-to-go-back semantics
- search within selection prompts
- temporary informational tables/messages that are cleared after acknowledgement
- `$EDITOR` integration for editing large bodies and JSON files

This keeps the interactive flow light without introducing a full-screen TUI framework.

## Serialization Strategy

Straumr uses `System.Text.Json` source generation through `StraumrJsonContext`.

Reasons:

- lower runtime reflection needs
- compatibility with Native AOT publishing
- centralized control over serialized domain types

Auth config polymorphism is implemented with:

- `JsonPolymorphic`
- `JsonDerivedType`
- discriminator field `authType`

## Native AOT Considerations

The CLI project is configured for:

- single-file publish
- self-contained publish
- Native AOT

`Program.cs` includes explicit suppression/comments for Spectre dynamic-code requirements, and the project includes `MyRoots.xml` for trimming roots.

## Current Design Boundaries

The codebase is intentionally local-file-centric:

- no remote sync
- no encrypted secret vault
- no background token service
- no separate auth/request metadata store

Everything meaningful is persisted as JSON on disk and reloaded per command invocation.
