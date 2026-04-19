# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI view scaffolding and shell integration in progress

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| App shell | In progress | Root Bubble Tea model, navigation, screen routing, and send handoff are in place |
| CLI client | In progress | Subprocess execution, JSON decode, stderr envelope parsing, cache, and typed wrappers are in place |
| Workspace | Scaffolded | View package is present and wired into the root shell; structured rename support exists |
| Request | Scaffolded | View/editor package is present with structured draft extraction; shell mutation wiring is still being completed |
| Auth | Scaffolded | View/editor package is present with type-aware draft extraction and inline edit support |
| Secret | Scaffolded | View/editor package is present with structured draft extraction and JSON-safe create/edit/delete paths |
| Send | Scaffolded | Response-view package is present and wired to request send handoff |
| Dialogs/pickers | Scaffolded | Overlay and picker packages are present with structured result types |

## Open decisions

- Add a CLI-safe large-body input contract for request/auth payloads to avoid command-line length limits.
- Decide which remaining request/auth mutation paths should be submitted through dialogs versus direct inline forms.

## Blockers

- No current blocking issues in the owned Go view packages.
- Large inline payload handling remains a future contract concern for request/auth editing.

## Recommended implementation order

1. Finish request mutation submission and request-auth selection wiring.
2. Wire auth and secret mutation submission through the shell.
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
