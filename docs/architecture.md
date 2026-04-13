# Architecture

Straumr is a five-project .NET solution:

- `Straumr.Console.App`: host executable. Builds the integration catalog, wires DI, resolves which integration to run, and invokes it.
- `Straumr.Console.Cli`: Spectre.Console command-line interface and command orchestration.
- `Straumr.Console.Tui`: Terminal.Gui-based interactive terminal UI built on a screen engine and reusable components.
- `Straumr.Console.Shared`: cross-integration plumbing — integration abstractions, theme loading, request editor state, interactive console interface.
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

Each integration is responsible for loading persisted options through `StraumrOptionsService` before it runs. All stateful behavior flows through the service interfaces in `Straumr.Core.Services.Interfaces`.

## Solution Structure

### `Straumr.Console.App`

Primary responsibilities:

- process entry point (`Main`)
- integration catalog construction and DI composition
- integration resolution and dispatch

Key areas:

- `Program.cs`: integration catalog, DI setup, resolver call, integration dispatch
- `Straumr.Console.App.csproj`: publish flags (`PublishSingleFile`, `SelfContained`, `PublishAot`), icon, and trimmer roots aggregated from each integration's `*Roots.xml`

### `Straumr.Console.Shared`

Primary responsibilities:

- integration abstraction and resolver
- theme loading and theme options
- shared request-editor state used by both CLI and TUI editing flows
- `IInteractiveConsole` abstraction for interactive prompts

Key areas:

- `Integrations/IConsoleIntegration.cs`: contract every integration must implement
- `Integrations/ConsoleIntegrationCatalog.cs`: fluent registration of integration installers
- `Integrations/ConsoleIntegrationResolver.cs`: name/alias/command matching
- `Helpers/ThemeLoader.cs`: reads `theme.json` from the options directory
- `Theme/StraumrTheme.cs`: semantic palette (surface, primary, method colors, etc.)
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

Primary responsibilities:

- interactive, full-screen TUI using `Terminal.Gui` v2
- screen-based navigation for workspaces, requests, auths, secrets, and request sending
- reusable prompt components (selection, form, table, text input, message, details, key/value editor)
- TUI-side implementations of the request, auth, and body editors

Key areas:

- `Integration/TuiConsoleIntegration.cs`: the `IConsoleIntegration` implementation; registers TUI services and boots into either `WorkspacesScreen` or `RequestsScreen` depending on whether a current workspace is already set
- `TuiApp.cs`: thin wrapper around `Terminal.Gui`'s `Application` lifecycle, owning the main `Window`, the active scheme, and the key-event plumbing
- `Infrastructure/ScreenEngine.cs` and `ScreenNavigationContext.cs`: screen stack, resolution via DI, navigation primitives
- `Infrastructure/TuiAppResolver.cs` and `TuiApplicationContext.cs`: per-run `TuiApp` lifetime management
- `Screens/*`: one screen class per object type (`WorkspacesScreen`, `RequestsScreen`, `AuthsScreen`, `SecretsScreen`, `SendScreen`) plus shared `ModelScreen`/`Screen` base classes and prompt screens that wrap prompt components in screen form
- `Components/*`: Terminal.Gui view building blocks organized by role (`Bars/`, `Branding/`, `ListViews/`, `Panels/`, `Prompts/`, `Text/`, `TextFields/`)
- `Services/*`: `RequestEditor`, `AuthEditor`, `BodyEditor`, `WorkspaceGuard`, and `TuiOperationExecutor` — TUI-specific orchestration behind the screens
- `Helpers/*`: color resolution, HTTP method markup, send-result formatting, and key bindings
- `Console/TuiInteractiveConsole.cs`: the TUI implementation of `IInteractiveConsole`
- `TuiRoots.xml`: trimmer root descriptor for the TUI assembly under AOT

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

Straumr exposes two interactive surfaces, both backed by the same `Straumr.Core` services.

### CLI interactive prompts (inside a Spectre command)

Used by `create`/`edit` flows when a command drops into an interactive sub-flow.

Capabilities:

- escape-to-go-back semantics via `EscapeCancellableConsole`
- search within selection prompts
- temporary informational tables/messages that are cleared after acknowledgement
- `$EDITOR` integration for editing large bodies and JSON files
- `CliInteractiveConsole` implements `IInteractiveConsole` so shared editor logic can be driven from the CLI

### TUI (`Straumr.Console.Tui`)

Used when the user launches the interactive terminal UI.

Capabilities:

- Terminal.Gui v2 application built around `TuiApp` and a `ScreenEngine` screen stack
- themed via `StraumrTheme`, loaded from `theme.json` in the options directory (falls back to defaults if missing or invalid)
- per-screen key handling and prompt-screen dispatch through `ScreenNavigationContext`
- reusable prompt components for selection, form, table, text input, message, details, and key/value editing
- dedicated editors for requests, auths, and bodies under `Services/` that drive the screens without duplicating core logic
- `TuiInteractiveConsole` implements `IInteractiveConsole` so shared editor state works the same way it does for the CLI

## TUI Navigation Flow

When `TuiConsoleIntegration.RunAsync` executes:

1. options are loaded through `StraumrOptionsService`
2. `StraumrTheme` is resolved from DI (loaded eagerly via `ThemeLoader`)
3. a `ScreenEngine` is constructed around the service provider, theme, and `TuiAppResolver`
4. the engine starts on `RequestsScreen` if a current workspace is already active, otherwise on `WorkspacesScreen`
5. screens push prompts or additional screens through `ScreenNavigationContext`, and the engine shuts down the `TuiApp` when navigation completes

Trimmer preservation is handled through `TuiRoots.xml` (aggregated into the host csproj alongside `CliRoots.xml`).

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
- `Straumr.Console.Tui/TuiRoots.xml` preserves Terminal.Gui view/driver types

`Straumr.Console.App.csproj` aggregates each integration's `*Roots.xml` as `TrimmerRootDescriptor` items, so the host picks them up automatically. `CliConsoleIntegration` and `TuiApp` carry explicit `UnconditionalSuppressMessage` attributes for the dynamic-code paths that Spectre.Console.Cli and Terminal.Gui require.

## Current Design Boundaries

The codebase is intentionally local-file-centric:

- no remote sync
- no encrypted secret vault
- no background token service
- no separate auth/request metadata store

Everything meaningful is persisted as JSON on disk and reloaded per command invocation.
