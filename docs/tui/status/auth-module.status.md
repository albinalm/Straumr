# Auth Module Status

## Scope

Auth list, inspect, create, edit, copy, and delete for workspace-local auth definitions.

## Current status

Completed for the current TUI scope. The Go view package exposes type-aware draft extraction and the shell now wires create/edit/copy/delete/inspect through JSON-safe CLI calls.

## Completed work

- Mapped current auth screen and auth editor responsibilities.
- Defined read/create/copy/delete CLI usage.
- Implemented additive inline `edit auth` support reusing the create-flag surface.
- Kept the existing interactive/editor-backed flows for non-inline usage.
- Added auth draft extraction per type and explicit editor open/close helpers in the Go view package.
- Added the structured auth list/edit surface in the Go shell.
- Added shell-friendly auth editor accessors such as `OpenCreate`, `OpenEdit`, `EditorActive`, `EditorMode`, and `MutationDraft.ConfigType()`.
- Wired auth inspect through `get auth --json` into a read-only details overlay.
- Replaced auth choice-like text prompts with shared select overlays for auth type, OAuth2 grant, PKCE, custom body type, and extraction source.

## Work in progress

- Minor auth overlay polish remains, but the main create/edit shell flow is wired for the current scope.

## Files touched

- `docs/tui/auth-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Auth/AuthCreateCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineConfigBuilder.cs`
- `src/Straumr.Console.Cli/Commands/Auth/AuthInlineSettingsBase.cs`
- `src/straumr-tui/internal/app/auth_flows.go`
- `src/straumr-tui/internal/views/auth/**`

## Important decisions

- Go TUI should collect/edit auth data, but never perform auth runtime logic itself.
- Auth inline edit should reuse the create command's flag vocabulary instead of inventing a second schema.

## Next steps

- Keep auth aligned with shared overlay primitives as broader shell cleanup continues.

## Resume notes

- `edit auth --json` is safe for subprocess use when inline flags are present, and the Go view exposes a type-aware draft payload plus a shell-facing config discriminator.
- Auth inspect is live through the JSON-safe get path.
- Auth create/edit now uses picker-backed choice fields for type/grant/PKCE/body type/extraction source.
