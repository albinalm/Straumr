# Workspace Module Status

## Scope

Workspace list, activate, create, copy, import, export, delete, inspect, and workspace-entry navigation.

## Current status

CLI contract ready. Go implementation not started.

## Completed work

- Mapped existing workspace screen behavior.
- Defined required CLI calls and state transitions.
- Implemented the structured workspace rename path needed for strict TUI support.
- Added workspace service update support for inline rename.

## Work in progress

- None.

## Blockers

- Dotnet build/test verification is pending outside the sandbox.

## Files touched

- `docs/tui/workspace-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`
- `src/Straumr.Core/Services/Interfaces/IStraumrWorkspaceService.cs`
- `src/Straumr.Core/Services/StraumrWorkspaceService.cs`

## Important decisions

- Preserve workspace-centric startup and navigation.
- Keep damaged/missing workspaces visible and actionable.
- Restrict the structured update scope to rename; preserve the existing editor flow for raw manifest editing.

## Next steps

- Verify the CLI changes with a user-run dotnet build/test pass.
- Implement workspace list screen and active-workspace handoff.

## Resume notes

- `edit workspace --name <new-name> --json` is now the TUI-safe update path. Without `--name`, the editor-backed flow still exists.
