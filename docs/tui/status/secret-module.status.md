# Secret Module Status

## Scope

Global secret list, inspect, create, edit, copy, and delete with masking rules.

## Current status

Go view package is present with structured draft/result APIs.

## Completed work

- Mapped current secret screen behavior.
- Defined masking rules and expected flows.
- Implemented `create secret --json`.
- Implemented inline `edit secret --json`.
- Implemented `delete secret --json` with a small success object.
- Added secret draft extraction and explicit editor open/close helpers in the Go view package.
- Added delete-result and secret masking helpers for shell consumption.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.
- Shell-level mutation submission still needs to consume the structured draft payloads.

## Files touched

- `docs/tui/secret-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`
- `src/Straumr.Console.Cli/Infrastructure/CliJsonContext.cs`
- `src/Straumr.Console.Cli/Models/SecretDeleteResult.cs`
- `src/straumr-tui/internal/views/secret/**`

## Important decisions

- Secret values stay masked by default in list/detail UI.
- Keep inline mutation limited to name/value and reuse existing editor mode for raw-file editing.

## Next steps

- Wire secret draft submission into the shell mutation path.
- Connect the masking/edit overlays to the structured dialog helpers.

## Resume notes

- `create secret --json` requires both arguments and does not prompt. The Go view now exposes a structured secret draft payload.
