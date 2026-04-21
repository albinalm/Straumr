# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI view scaffolding and shell integration in progress

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| Visual system | In progress | Theme loader/defaults, semantic Lip Gloss styles, screenshot-aligned shell header/banner framing, and first-pass visual styling for lists, overlays, and send panes are now implemented; broader polish and remaining semantic cleanup are still ongoing |
| App shell | In progress | Root Bubble Tea model, overlay flows, workspace/secret CRUD, workspace import/export, auth create/edit with picker-backed choice fields plus custom headers/params, reusable read-only inspect viewer overlays, selection-preserving mutation refresh, request inspect plus richer request quick flows including body overlay/file import and shared pair editing for metadata, send save/export/copy handoff, and the new shell header/tab/banner presentation are in place |
| CLI client | Implemented | Subprocess execution, JSON decode, stderr envelope parsing, cache, flexible CLI timestamp parsing, and typed wrappers for workspace/request/auth/secret/send are in place |
| Workspace | In progress | View package is wired and create/rename/copy/delete plus import/export/inspect now execute through the shell |
| Request | In progress | View/editor package exposes structured drafts and the shell now drives inspect/create/edit/copy/delete plus auth/body/header/param quick-flow fields via JSON-safe CLI calls, with select overlays used for method/auth/body-type choice, a dedicated body overlay for larger inline content plus file import, shared pair editing for headers/params, and a reusable scrollable inspect viewer |
| Auth | Implemented | View/editor package exposes type-aware mutation drafts; shell create/edit/copy/delete/inspect now execute through JSON-safe CLI calls, shared select overlays drive auth type/grant/PKCE/body-type/extraction-source choices, and custom headers/params now use the same shared pair-edit flow as request metadata |
| Secret | Implemented | View package is wired and create/edit/copy/delete/inspect now execute through the shell |
| Send | In progress | Response-view package is wired to real send, dry-run, save-body, export, copy-pane/template, and view-level beautify/revert behavior; save/export now use the shared path picker and the send view polish is improving |
| Dialogs/pickers | In progress | Overlay and picker packages are present with structured result types, including the shared path picker, pair editor, multiline body editor, reusable read-only text viewer, and a first-pass themed overlay presentation now mounted by the shell |

## Open decisions

- Whether the Go app should preserve the current `theme.json` shape exactly or allow additive Go-only fields while keeping the shared schema backward compatible.
- Add a CLI-safe large-body input contract for request payloads to avoid command-line length limits.
- Decide which remaining request mutation paths should be submitted through dialogs versus direct inline forms.

## Blockers

- No current blocking issues in the owned Go view packages.
- Large inline payload handling remains a future contract concern for request editing.

## Recommended implementation order

1. Refine the visual-system rollout: tighten semantic style coverage, remove duplication between theme and dialog-local styling, and tune narrow-terminal behavior against the screenshot baseline.
2. Refine remaining request/entity detail presentation polish on top of the shared inspect viewer and structured-overlay UX.
3. Decide whether the remaining request/auth metadata flows need a richer table/form editor beyond the current shared pair overlay.
4. Continue shell cleanup by replacing any remaining ad hoc prompts with shared overlays.

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
