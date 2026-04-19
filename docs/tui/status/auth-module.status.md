# Auth Module Status

## Scope

Auth list, inspect, create, edit, copy, and delete for workspace-local auth definitions.

## Current status

Go view package is present with type-aware draft extraction and inline edit support.

## Completed work

- Mapped current auth screen and auth editor responsibilities.
- Defined read/create/copy/delete CLI usage.
- Implemented additive inline `edit auth` support reusing the create-flag surface.
- Kept the existing interactive/editor-backed flows for non-inline usage.
- Added auth draft extraction per type and explicit editor open/close helpers in the Go view package.
- Added the structured auth list/edit surface in the Go shell.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.
- Large auth payload input still has command-line length risk in future TUI/editor work.
- Shell-level mutation submission still needs to consume the structured draft payloads.

## Files touched

- `docs/tui/auth-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Auth/AuthCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineConfigBuilder.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineSettingsBase.cs`
- `src/straumr-tui/internal/views/auth/**`

## Important decisions

- Go TUI should collect/edit auth data, but never perform auth runtime logic itself.
- Auth inline edit should reuse the create command's flag vocabulary instead of inventing a second schema.

## Next steps

- Wire auth draft submission into the shell mutation path.
- Add shell-side auth picker and edit-overlay handling.

## Resume notes

- `edit auth --json` is safe for subprocess use when inline flags are present, and the Go view now exposes a type-aware draft payload.
