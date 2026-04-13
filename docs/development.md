# Development

This document covers the codebase from a contributor and maintainer perspective.

## Toolchain

The projects target `.NET 10.0`.

Current package highlights:

- `Spectre.Console`
- `Spectre.Console.Cli`
- `Terminal.Gui` (v2, used by the interactive TUI)
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Http`
- `Humanizer.Core`
- `MimeTypes`

## Local Build Model

The solution file is:

```text
src/Straumr.sln
```

Projects:

- `src/Straumr.Console.App/Straumr.Console.App.csproj` — host executable (the `straumr` binary)
- `src/Straumr.Console.Cli/Straumr.Console.Cli.csproj` — Spectre.Console CLI integration
- `src/Straumr.Console.Tui/Straumr.Console.Tui.csproj` — Terminal.Gui TUI integration
- `src/Straumr.Console.Shared/Straumr.Console.Shared.csproj` — shared integration/theme/editor plumbing
- `src/Straumr.Core/Straumr.Core.csproj` — storage, models, HTTP, auth, and secret services

Typical commands:

```sh
dotnet build src/Straumr.sln
dotnet run --project src/Straumr.Console.App -- --help
dotnet run --project src/Straumr.Console.App -- list workspace --json
```

Notes:

- the repository currently has no test project
- several workflows assume `$EDITOR` is set
- many commands persist into `~/.straumr`, so use a temporary `HOME` when manually testing isolated scenarios

Example isolated session:

```sh
HOME=/tmp/straumr-home DOTNET_CLI_HOME=/tmp/straumr-home \
  src/Straumr.Console.App/bin/Debug/net10.0/linux-x64/straumr --help
```

Using the built binary avoids triggering a new NuGet restore when network access is restricted.

## Important Source Files

- `src/Straumr.Console.App/Program.cs`: integration catalog, DI composition, and dispatch
- `src/Straumr.Console.Shared/Integrations/ConsoleIntegrationResolver.cs`: picks CLI vs TUI per invocation
- `src/Straumr.Console.Cli/Integration/CliConsoleIntegration.cs`: CLI service registration and Spectre command tree
- `src/Straumr.Console.Cli/Infrastructure/StraumrCommandRegistry.cs`: command-name catalog used by the resolver
- `src/Straumr.Console.Tui/Integration/TuiConsoleIntegration.cs`: TUI service registration and boot sequence
- `src/Straumr.Console.Tui/Infrastructure/ScreenEngine.cs`: TUI screen stack and DI-driven screen resolution
- `src/Straumr.Console.Tui/TuiApp.cs`: Terminal.Gui lifetime and window/scheme wiring
- `src/Straumr.Core/Services/StraumrRequestService.cs`: request orchestration and send pipeline
- `src/Straumr.Core/Services/StraumrAuthService.cs`: OAuth/custom auth implementation
- `src/Straumr.Core/Services/StraumrWorkspaceService.cs`: workspace registry and package import/export
- `src/Straumr.Core/Extensions/ModelExtensions.cs`: conversion from saved request model to `HttpRequestMessage`
- `src/Straumr.Core/Configuration/StraumrJsonContext.cs`: source-generated serialization map

## Coding Patterns In Use

### Dependency Injection

The CLI uses Microsoft DI and injects services directly into Spectre command constructors.

### Source-Generated JSON

Persisted models and CLI JSON DTOs use source-generated serializer contexts. When adding new persisted polymorphic or DTO types, update the relevant context definitions.

### Temp-File Editing

Editor-backed commands follow a consistent pattern:

1. copy or serialize the current object to a temp file
2. launch `$EDITOR`
3. validate JSON on return
4. reject ID changes for request/auth/secret edits
5. copy the temp file back into place

### Timestamp Mutation

Be careful about read paths:

- `ReadStraumrModel` mutates `LastAccessed`
- `PeekStraumrModel` does not

Most list/get flows use `Peek*` intentionally to avoid changing timestamps during inspection.

## Release Workflow

The GitHub Actions workflow is in:

```text
.github/workflows/release.yml
```

It is manually triggered with `workflow_dispatch`.

### Versioning

The workflow computes a version as:

```text
YYYY.M.D.<github-run-number>
```

### Build Matrix

Release artifacts are produced for:

- `win-x64`
- `linux-x64`

### Publish Settings

The host project (`Straumr.Console.App`) publishes with:

- `PublishSingleFile=true`
- `SelfContained=true`
- `PublishAot=true`
- `InvariantGlobalization=true`

The host aggregates trimmer root descriptors from each integration (`CliRoots.xml`, `TuiRoots.xml`) alongside its own `MyRoots.xml` so Spectre.Console.Cli and Terminal.Gui types survive trimming.

Linux packaging creates:

- `straumr-<version>-linux-x64.tar.gz`

Windows packaging creates:

- `straumr-<version>-win-x64.zip`

### Signing and Checksums

The release job:

1. downloads build artifacts
2. installs `minisign`
3. writes `MINISIGN_PRIVATE_KEY` from GitHub secrets to `minisign.key`
4. generates `sha256sums.txt`
5. generates `.minisig` files for each distributed artifact
6. creates a GitHub release tagged `v<version>`

The repository also includes `minisign.pub` for verification.

## Maintenance Notes

- Workspace import/export assumes archive integrity; malformed packages fail validation early.
- Request and auth files share the same directory and extension, so membership sets in the workspace manifest are important.
- Secret storage is global and currently unencrypted.
- The release binary can differ from local debug output if a stale publish directory is used; validate behavior against a fresh build when debugging command registration issues.
