# Architecture

Straumr is a five-project .NET solution:

- `Straumr.Console.App`: host executable. Builds the integration catalog, wires DI, resolves which integration to run, and invokes it.
- `Straumr.Console.Cli`: Spectre.Console command-line interface and command orchestration.
- `Straumr.Console.Tui`: intentionally blank XenoAtom.Terminal.UI host ready for the TUI rewrite.
- `Straumr.Console.Shared`: cross-integration plumbing — integration abstractions, request editor state, and interactive console interface.
- `Straumr.Core`: storage, domain models, HTTP execution, auth, and secret resolution.

The CLI and TUI are implemented as peer _console integrations_. Both are registered at startup; one of them is picked per invocation based on the arguments.

## High-Level Runtime Flow

On startup, `Straumr.Console.App/Program.cs`:

1. builds a `ConsoleIntegrationCatalog` and installs the CLI and TUI integration installers
2. creates a single `ServiceCollection` and lets every integration register its own services into it
3. builds the shared `ServiceProvider`
4. calls `ConsoleIntegrationResolver.Resolve(integrations, args)` to pick which integration handles this invocation
5. invokes `integration.RunAsync(provider, remainingArgs, cancellationToken)`

`ConsoleIntegrationResolver` selects an integration in this order:

1. if `args[0]` matches an integration name or alias, that integration is selected and the token is consumed
2. otherwise, if `args[0]` matches a known command noun of some integration (populated at registration time by the CLI), that integration is selected and all args are passed through
3. otherwise the default integration is used — currently the TUI

In practice, `straumr` with no arguments launches the TUI, while `straumr list request --json` and any other command-noun invocation is routed to the CLI.

The host's TUI reference is conditional. Passing `-p:IncludeTui=false` at build or publish time excludes `Straumr.Console.Tui` and its transitive dependencies. In that configuration the CLI is the only registered integration, so a bare invocation is also handled by the CLI.

The CLI loads persisted options through `StraumrOptionsService` before it runs. The placeholder TUI has no application services or stateful behavior yet.

## Solution Structure

### `Straumr.Console.App`

Primary responsibilities:

- process entry point (`Main`)
- integration catalog construction and DI composition
- integration resolution and dispatch

Key areas:

- `Program.cs`: integration catalog, DI setup, resolver call, integration dispatch
- `Straumr.Console.App.csproj`: publish flags (`PublishSingleFile`, `SelfContained`, `PublishAot`), icon, and CLI/host trimmer roots
- `IncludeTui`: MSBuild property that defaults to `true`; when `false`, both the TUI project reference and its registration code are omitted

### `Straumr.Console.Shared`

Primary responsibilities:

- integration abstraction and resolver
- framework-neutral request-editor state available to interactive frontends
- `IInteractiveConsole` abstraction for interactive prompts

Key areas:

- `Integrations/IConsoleIntegration.cs`: contract every integration must implement
- `Integrations/ConsoleIntegrationCatalog.cs`: fluent registration of integration installers
- `Integrations/ConsoleIntegrationResolver.cs`: name/alias/command matching
- `Models/RequestEditorState.cs`: shared request-edit snapshot used by interactive flows

### `Straumr.Console.Cli`

Primary responsibilities:

- command registration via Spectre.Console.Cli
- interactive prompt UX for editing inside CLI commands
- human-readable and JSON output formatting
- editor launching and temp-file workflows
- shell completion installation and query handling

Key areas:

- `Integration/CliConsoleIntegration.cs`: the `IConsoleIntegration` implementation; owns `CommandApp`, registers command types, and configures the command tree
- `Infrastructure/StraumrCommandRegistry.cs` and `StraumrConfiguratorExtensions.cs`: discover the registered command nouns so the resolver can route to the CLI by command name
- `Infrastructure/StraumrTypeRegistrar.cs`: bridges Spectre's type registrar onto the shared service provider
- `Commands/*`: one command class per CLI action (including the new `SecretCopyCommand`)
- `Helpers/*`: request/auth prompt helpers and formatting helpers
- `Console/*`: escape-aware prompt wrapper and `CliInteractiveConsole` implementation of `IInteractiveConsole`
- `Infrastructure/CliJsonContext.cs`: JSON DTO source-generated context
- `Models/*`: CLI-specific result DTOs for JSON output
- `CliRoots.xml`: trimmer root descriptor preserving the command assembly under AOT

### `Straumr.Console.Tui`

This project is the clean foundation for the TUI rewrite. It references `XenoAtom.Terminal.UI` and currently contains only `Integration/TuiConsoleIntegration.cs`, which launches an empty `VStack` in the framework's full-screen host. It deliberately registers no services and implements no application behavior yet.

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

Straumr currently exposes CLI interactive prompts backed by `Straumr.Core`. The TUI integration is an empty rewrite host.

### CLI interactive prompts (inside a Spectre command)

Used by `create`/`edit` flows when a command drops into an interactive sub-flow.

Capabilities:

- escape-to-go-back semantics via `EscapeCancellableConsole`
- search within selection prompts
- temporary informational tables/messages that are cleared after acknowledgement
- `$EDITOR` integration for editing large bodies and JSON files
- `CliInteractiveConsole` implements `IInteractiveConsole` so shared editor logic can be driven from the CLI

### TUI (`Straumr.Console.Tui`)

Running `straumr` with no arguments selects the TUI integration and starts an empty XenoAtom.Terminal.UI full-screen application. The default framework exit gesture is `Ctrl+Q`. Screens, navigation, theming, editors, and domain-service registrations will be introduced as part of the rewrite.

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

The host project (`Straumr.Console.App`) is configured for:

- single-file publish
- self-contained publish
- Native AOT

Trimming preservation is layered:

- `Straumr.Console.App/MyRoots.xml` covers the host
- `Straumr.Console.Cli/CliRoots.xml` preserves Spectre command types

`Straumr.Console.App.csproj` includes the CLI descriptor alongside the host descriptor. `CliConsoleIntegration` carries explicit `UnconditionalSuppressMessage` attributes for Spectre.Console.Cli's dynamic-code paths. XenoAtom.Terminal.UI is Native AOT-oriented and does not require the removed TUI descriptor.

## Current Design Boundaries

The codebase is intentionally local-file-centric:

- no remote sync
- no encrypted secret vault
- no background token service
- no separate auth/request metadata store

Everything meaningful is persisted as JSON on disk and reloaded per command invocation.
