# Auth Module Status

## Scope

Auth list, inspect, create, edit, copy, and delete for workspace-local auth definitions.

## Current status

CLI contract ready. Go implementation not started.

## Completed work

- Mapped current auth screen and auth editor responsibilities.
- Defined read/create/copy/delete CLI usage.
- Implemented additive inline `edit auth` support reusing the create-flag surface.
- Kept the existing interactive/editor-backed flows for non-inline usage.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.
- Large auth payload input still has command-line length risk in future TUI/editor work.

## Files touched

- `docs/tui/auth-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Auth/AuthCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineConfigBuilder.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineSettingsBase.cs`

## Important decisions

- Go TUI should collect/edit auth data, but never perform auth runtime logic itself.
- Auth inline edit should reuse the create command's flag vocabulary instead of inventing a second schema.

## Next steps

- Verify the CLI change with a user-run dotnet build/test pass.
- Implement auth forms against the structured CLI contract.

## Resume notes

- `edit auth --json` is now safe for subprocess use when inline flags are present; `--json` with no inline flags still routes to the editor-backed path by design.
