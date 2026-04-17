# TUI Implementation Status

## Current phase

- Phase 1: design complete
- Phase 2: CLI contract completion implemented
- Phase 3: Go TUI implementation not started

## Module status

| Module | Status | Notes |
| --- | --- | --- |
| App shell | Designed | Bubble Tea root model, navigation, refresh rules defined |
| CLI client | Ready for Go implementation | Scoped CLI contract gaps closed; subprocess, JSON decode, stderr envelope parsing, cache rules defined |
| Workspace | Ready for Go implementation | Structured rename path is now available |
| Request | Designed | Full list/edit/send handoff flow defined |
| Auth | Ready for Go implementation | Structured inline edit contract is now available |
| Secret | Ready for Go implementation | Structured create/edit/delete contract is now available |
| Send | Designed | JSON-only response viewer defined |
| Dialogs/pickers | Designed | New picker direction defined |

## Open decisions

- Add a CLI-safe large-body input contract for request/auth payloads to avoid command-line length limits.
- Decide how `straumr-tui` resolves the `straumr` binary in dev and packaged builds.

## Blockers

- Dotnet build/test verification has not been run in this session.
- Large inline payload handling is still an open contract concern for future TUI/editor work.

## Recommended implementation order

1. Verify the CLI changes with a user-run dotnet build/test pass.
2. Scaffold `straumr-tui` root app, keymap, and subprocess client.
3. Implement workspace and request list screens plus caching/invalidation.
4. Implement send view.
5. Implement request editor.
6. Implement auth and secret modules.
7. Implement shared dialogs/path pickers last-mile polish.

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
