# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI view scaffolding and shell integration in progress

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| App shell | In progress | Root Bubble Tea model, overlay flows, workspace/secret CRUD, workspace import/export, auth create/edit with picker-backed choice fields, entity inspect overlays, request inspect plus richer request quick flows, and send save/export/copy handoff are in place |
| CLI client | Implemented | Subprocess execution, JSON decode, stderr envelope parsing, cache, and typed wrappers for workspace/request/auth/secret/send are in place |
| Workspace | In progress | View package is wired and create/rename/copy/delete plus import/export/inspect now execute through the shell |
| Request | In progress | View/editor package exposes structured drafts and the shell now drives inspect/create/edit/copy/delete plus auth/body/header/param quick-flow fields via JSON-safe CLI calls, with select overlays now used for auth and body-type choice |
| Auth | Implemented | View/editor package exposes type-aware mutation drafts; shell create/edit/copy/delete/inspect now execute through JSON-safe CLI calls, and shared select overlays now drive auth type/grant/PKCE/body-type/extraction-source choices |
| Secret | Implemented | View package is wired and create/edit/copy/delete/inspect now execute through the shell |
| Send | In progress | Response-view package is wired to real send, dry-run, save-body, export, copy-pane/template, and view-level beautify/revert behavior; save/export now use the shared path picker and the send view polish is improving |
| Dialogs/pickers | In progress | Overlay and picker packages are present with structured result types, and the shared path picker is now mounted by the shell for send save/export flows |

## Open decisions

- Add a CLI-safe large-body input contract for request payloads to avoid command-line length limits.
- Decide which remaining request mutation paths should be submitted through dialogs versus direct inline forms.

## Blockers

- No current blocking issues in the owned Go view packages.
- Large inline payload handling remains a future contract concern for request editing.

## Recommended implementation order

1. Refine request/entity inspect detail presentation and remaining overlay polish.
2. Reuse shared path/select overlays in any remaining ad hoc shell prompts.
3. Tighten cache invalidation and refresh rules around successful mutations.

## Files touched

- `docs/tui/README.md`
- `docs/tui/app-shell.md`
- `docs/tui/cli-client.md`
- `docs/tui/cli-contract-changes.md`
- `docs/tui/workspace-module.md`
- `docs/tui/request-module.md`
- `docs/tui/auth-module.md`
- `docs/tui/secret-module.md`
- `docs/tui/send-module.md`
- `docs/tui/dialogs-and-pickers.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `docs/tui/status/*.md`
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
- `src/straumr-tui/go.mod`
- `src/straumr-tui/go.sum`
- `src/straumr-tui/cmd/straumr-tui/main.go`
- `src/straumr-tui/internal/app/**`
- `src/straumr-tui/internal/cache/**`
- `src/straumr-tui/internal/cli/**`
- `src/straumr-tui/internal/state/**`
- `src/straumr-tui/internal/ui/**`
- `src/straumr-tui/internal/views/**`
