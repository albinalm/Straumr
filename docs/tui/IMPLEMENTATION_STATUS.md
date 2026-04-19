# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI view scaffolding and shell integration in progress

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| App shell | In progress | Root Bubble Tea model, overlay flows, workspace/secret CRUD, request copy/delete/create/edit quick flows, and send handoff are in place |
| CLI client | Implemented | Subprocess execution, JSON decode, stderr envelope parsing, cache, and typed wrappers for workspace/request/auth/secret/send are in place |
| Workspace | In progress | View package is wired and create/rename/copy/delete now execute through the shell |
| Request | In progress | View/editor package exposes structured drafts and the shell now drives create/edit/copy/delete via JSON-safe CLI calls |
| Auth | In progress | View/editor package exposes type-aware mutation drafts; shell copy/delete are wired, create/edit are still pending |
| Secret | In progress | View package is wired and create/edit/copy/delete now execute through the shell |
| Send | In progress | Response-view package is wired to real send and dry-run flows |
| Dialogs/pickers | Scaffolded | Overlay and picker packages are present with structured result types |

## Open decisions

- Add a CLI-safe large-body input contract for request/auth payloads to avoid command-line length limits.
- Decide which remaining request/auth mutation paths should be submitted through dialogs versus direct inline forms.

## Blockers

- No current blocking issues in the owned Go view packages.
- Large inline payload handling remains a future contract concern for request/auth editing.

## Recommended implementation order

1. Finish auth create/edit submission through the shell, using the type-aware draft surface.
2. Extend request editing beyond the current quick-flow fields into auth/body/header/param overlays.
3. Complete send response/export/save actions against the send view.
4. Refine dialogs and path picker interactions.
5. Tighten cache invalidation and refresh rules around successful mutations.

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
