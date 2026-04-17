# Workspace Module

## Purpose

Implement the workspace list and workspace lifecycle flows while keeping workspace selection central to the app.

## Responsibilities

- Show the workspace list with status, counts, and current-workspace marker.
- Support:
  - activate
  - create
  - copy
  - import
  - export
  - delete
  - inspect
- Open workspace-scoped screens from a selected workspace.
- Surface damaged/missing workspace states clearly.

## Inputs

- Workspace list DTOs
- Workspace details DTOs
- User actions from the workspace screen and overlays

## Outputs

- Updated workspace list state
- Active workspace selection for the app shell
- Success/error notifications

## CLI interaction

- `list workspace --json`
- `get workspace <id> --json`
- `create workspace <name> [--output <dir>] --json`
- `copy workspace <id-or-name> <new-name> [--output <dir>] --json`
- `import workspace <path> --json`
- `export workspace <id-or-name> <dir> --json`
- `edit workspace <id-or-name> --name <new-name> --json`
- `delete workspace <id-or-name> --json`

Structured contract notes:

- `edit workspace --name <new-name> --json` provides the structured rename path needed by the Go TUI.
- Without `--name`, the existing editor-backed workflow remains available for human use.

## Boundaries

- Owns workspace-specific actions and rendering.
- Does not own request/auth list logic after a workspace is entered.
- Relies on app-shell to store the active workspace and route navigation.

## UX notes

- Preserve familiar list layout and the quick actions for activate/create/delete/copy/import/export.
- Improve the “open workspace” step by making the destination explicit:
  - `Requests`
  - `Auths`
  - `Secrets`
  - `Set active`
- Make corrupted/missing workspaces actionable instead of silently failing.

## References

- Existing TUI screen: `src/Straumr.Console.Tui/Screens/WorkspacesScreen.cs`
- Workspace model/storage: `docs/data-model.md`
- User workflow context: `docs/user-workflows.md`
- Verified command implementation: `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`
- Proposed contract delta: `docs/tui/cli-contract-changes.md`
