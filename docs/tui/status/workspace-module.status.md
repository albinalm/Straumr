# Workspace Module Status

## Scope

Workspace list, activate, create, copy, import, export, delete, inspect, and workspace-entry navigation.

## Current status

Go view package is present and wired into the root shell.

## Completed work

- Mapped existing workspace screen behavior.
- Defined required CLI calls and state transitions.
- Implemented the structured workspace rename path needed for strict TUI support.
- Added workspace service update support for inline rename.
- Added the Go workspace list/navigation view and shell routing for workspace selection.

## Work in progress

- None.

## Blockers

- Mutation and refresh wiring still need to be tightened in the shell.

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

- Wire workspace mutation submission and post-mutation refresh through the shell.

## Resume notes

- `edit workspace --name <new-name> --json` is the TUI-safe update path. The Go shell already knows how to enter the workspace screen.
