# Secret Module Status

## Scope

Global secret list, inspect, create, edit, copy, and delete with masking rules.

## Current status

CLI contract ready. Go implementation not started.

## Completed work

- Mapped current secret screen behavior.
- Defined masking rules and expected flows.
- Implemented `create secret --json`.
- Implemented inline `edit secret --json`.
- Implemented `delete secret --json` with a small success object.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.

## Files touched

- `docs/tui/secret-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`
- `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`
- `src/Straumr.Console.Cli/Infrastructure/CliJsonContext.cs`
- `src/Straumr.Console.Cli/Models/SecretDeleteResult.cs`

## Important decisions

- Secret values stay masked by default in list/detail UI.
- Keep inline mutation limited to name/value and reuse existing editor mode for raw-file editing.

## Next steps

- Verify the CLI changes with a user-run dotnet build/test pass.
- Implement the secret screen and dialogs against the structured contract.

## Resume notes

- `create secret --json` requires both arguments and does not prompt. `edit secret` keeps the editor flow when no inline flags are supplied.
