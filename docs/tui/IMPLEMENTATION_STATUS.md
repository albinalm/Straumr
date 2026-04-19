# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI foundation scaffold started

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| App shell | Started | Bubble Tea root model, navigation, refresh rules, and startup routing scaffolded |
| CLI client | Started | Subprocess execution, JSON decode, stderr envelope parsing, and cache layer scaffolded |
| Workspace | View package present | Structured rename path is available; shell integration pending |
| Request | View package present | List/editor views exist; shell integration pending |
| Auth | View package in parallel | Structured inline edit contract is available |
| Secret | View package in parallel | Structured create/edit/delete contract is available |
| Send | View package in parallel | JSON-only response viewer planned |
| Dialogs/pickers | View package in parallel | Shared picker direction planned |

## Open decisions

- Add a CLI-safe large-body input contract for request/auth payloads to avoid command-line length limits.
- Decide how `straumr-tui` resolves the `straumr` binary in dev and packaged builds.

## Blockers

- Parallel view packages are still being completed.
- Large inline payload handling is still an open contract concern for future TUI/editor work.

## Recommended implementation order

1. Integrate the root shell with the completed workspace/request view packages.
2. Finish the typed client wrappers for request/send/auth/secret commands.
3. Integrate send response rendering.
4. Integrate auth and secret screens.
5. Wire shared dialogs/path pickers.

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
