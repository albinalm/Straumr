# CLI Client Status

## Scope

Subprocess execution, stdout/stderr capture, JSON decode, exit-code handling, cache invalidation.

## Current status

Completed for the current TUI scope. Go subprocess client, typed wrappers, and cache layer are in place.

## Completed work

- Defined JSON-only subprocess contract.
- Defined stderr envelope parsing and error normalization.
- Defined cache/invalidation role.
- Implemented the scoped CLI delta set needed to unblock strict TUI usage.
- Updated the machine-readable docs to match the implemented contract.
- Added the Go `straumr` client wrapper, executor, error parsing, list-result caching, typed startup helpers, and the initial request/send/auth/secret integration surface.

## Work in progress

- None for the current scope.

## Files touched

- `docs/tui/cli-client.md`
- `docs/tui/cli-contract-changes.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `docs/agents-doc.md`
- `docs/command-reference.md`
- `docs/user-workflows.md`
- `src/Straumr.Console.Cli/Commands/Auth/AuthCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineConfigBuilder.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineSettingsBase.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`
- `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`
- `src/Straumr.Console.Cli/Infrastructure/CliJsonContext.cs`
- `src/Straumr.Console.Cli/Models/SecretDeleteResult.cs`
- `src/Straumr.Core/Services/Interfaces/IStraumrWorkspaceService.cs`
- `src/Straumr.Core/Services/StraumrWorkspaceService.cs`
- `src/straumr-tui/go.mod`
- `src/straumr-tui/go.sum`
- `src/straumr-tui/cmd/straumr-tui/main.go`
- `src/straumr-tui/internal/cache/store.go`
- `src/straumr-tui/internal/cli/client.go`
- `src/straumr-tui/internal/cli/errors.go`
- `src/straumr-tui/internal/cli/exec.go`
- `src/straumr-tui/internal/cli/types.go`
- `src/straumr-tui/internal/views/**`

## Important decisions

- No embedded backend process.
- No parsing of non-JSON output.
- Large outputs should spill to temp files when needed.
- Missing contract pieces should be fixed with additive structured CLI paths rather than TUI-side workarounds.
- Keep the existing human editor/prompt flows available while adding machine-safe paths.
- The current Go view packages now expose structured draft/result APIs for the shell to consume.

## Next steps

- None for the current TUI work.

## Resume notes

- The Go subprocess layer is in place and verified; the remaining work is shell-level polish and richer request editing.
