# Workspace Module Status

## Scope

Workspace list, activate, create, copy, import, export, delete, inspect, and workspace-entry navigation.

## Current status

In progress. The Go view package is wired into the root shell, and workspace create/rename/copy/delete plus inspect/import/export now execute through the shell.

## Completed work

- Mapped existing workspace screen behavior.
- Defined required CLI calls and state transitions.
- Implemented the structured workspace rename path needed for strict TUI support.
- Added workspace service update support for inline rename.
- Added the Go workspace list/navigation view and shell routing for workspace selection.
- Wired workspace create/rename/copy/delete through the typed CLI client and shell overlays.
- Wired workspace inspect through `get workspace --json` into the shared read-only text viewer overlay.
- Wired workspace import/export through the shell, using the shared path picker for path collection.

## Work in progress

- Refining import/export ergonomics and broader workspace overlay polish.

## Blockers

- None beyond remaining shell polish and refresh tightening.

## Files touched

- `docs/tui/workspace-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`
- `src/Straumr.Core/Services/Interfaces/IStraumrWorkspaceService.cs`
- `src/Straumr.Core/Services/StraumrWorkspaceService.cs`
- `src/straumr-tui/internal/views/workspace/view.go`

## Important decisions

- Preserve workspace-centric startup and navigation.
- Keep damaged/missing workspaces visible and actionable.
- Restrict the structured update scope to rename; preserve the existing editor flow for raw manifest editing.

## Next steps

- Improve import/export ergonomics as the shared picker grows.
- Keep cache invalidation and refresh rules tight around workspace mutations.

## Resume notes

- `edit workspace --name <new-name> --json` is the TUI-safe rename path.
- Workspace inspect/import/export are live in the shell, with inspect now reusing the shared text viewer overlay, and the next pass is about polish rather than basic wiring.
