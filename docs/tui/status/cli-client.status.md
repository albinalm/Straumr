# CLI Client Status

## Scope

Subprocess execution, stdout/stderr capture, JSON decode, exit-code handling, cache invalidation.

## Current status

CLI prework completed. Go subprocess client scaffold is in place.

## Completed work

- Defined JSON-only subprocess contract.
- Defined stderr envelope parsing and error normalization.
- Defined cache/invalidation role.
- Implemented the scoped CLI delta set needed to unblock strict TUI usage.
- Updated the machine-readable docs to match the implemented contract.
- Added the Go `straumr` client wrapper, executor, error parsing, list-result caching, and typed startup helpers.

## Work in progress

- Extending the typed client with the remaining request/send/mutation helpers as the TUI modules land.

## Blockers

- The parallel auth/secret/send/dialog view packages are not yet build-complete.
- Large inline payload handling is still an open design concern for future request/auth editing.

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

## Important decisions

- No embedded backend process.
- No parsing of non-JSON output.
- Large outputs should spill to temp files when needed.
- Missing contract pieces should be fixed with additive structured CLI paths rather than TUI-side workarounds.
- Keep the existing human editor/prompt flows available while adding machine-safe paths.

## Next steps

- Finish the remaining typed wrappers for request/auth/secret/send flows.
- Integrate the shell against the remaining feature view packages.

## Resume notes

- The Go subprocess layer is in place and verified for the foundation-owned packages; next work is feature-module integration.
