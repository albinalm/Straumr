# CLI Client Status

## Scope

Subprocess execution, stdout/stderr capture, JSON decode, exit-code handling, cache invalidation.

## Current status

CLI prework completed. Go implementation not started.

## Completed work

- Defined JSON-only subprocess contract.
- Defined stderr envelope parsing and error normalization.
- Defined cache/invalidation role.
- Implemented the scoped CLI delta set needed to unblock strict TUI usage.
- Updated the machine-readable docs to match the implemented contract.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.
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

## Important decisions

- No embedded backend process.
- No parsing of non-JSON output.
- Large outputs should spill to temp files when needed.
- Missing contract pieces should be fixed with additive structured CLI paths rather than TUI-side workarounds.
- Keep the existing human editor/prompt flows available while adding machine-safe paths.

## Next steps

- Have the user run a dotnet build/test pass and report results.
- Start the Go client wrapper and cache layer once the CLI changes are verified.

## Resume notes

- The scoped CLI contract is implemented; `docs/tui/cli-contract-changes.md` now records the shipped behavior rather than proposed changes.
