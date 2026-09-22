# Architecture

Straumr is a .NET 10 solution: a host, two peer terminal frontends, and a core everything else sits on.

| Project | Responsibility |
| --- | --- |
| `Straumr.Console.App` | Builds DI, registers integrations, and dispatches arguments. |
| `Straumr.Console.Cli` | Spectre.Console commands, CLI prompts, editor orchestration, and CLI output. |
| `Straumr.Console.Tui` | XenoAtom.Terminal.UI application, screens, components, themes, and TUI interaction. |
| `Straumr.Console.Shared` | Integration contracts and frontend-neutral editing abstractions. |
| `Straumr.Core` | Models, JSONC persistence, settings/state, workspace/request/auth/secret services, and HTTP execution. |

## Dispatch

`ConsoleIntegrationCatalog` collects the integrations the build includes; `ConsoleIntegrationResolver` picks one per invocation, in order: an explicit integration name or alias (`cli`, `console`, `tui`, `ui`), then an integration that claims the first argument as a command noun, then the integration marked default. The TUI is the default and is `OnlyRunOnEntrypoint`, so a bare `straumr` opens it and any registered noun routes to the CLI.

`IncludeTui=false` drops the TUI project and its `INCLUDE_TUI` define, leaving a CLI-only host. New functionality must not make the CLI depend on TUI registration, and the two variants build to separate output trees so switching between them never needs a clean.

## Where behavior lives

Core services are the boundary for persistent data and application behavior. Frontends may format, prompt, and hold interaction state; they do not duplicate request sending, secret resolution, auth, or storage. `StraumrRequestService` performs send-time resolution and HTTP dispatch, `StraumrAuthService` owns OAuth and custom auth, and workspace import/export is zip-based `.straumrpak` handling in `StraumrWorkspaceService`.

Keybindings are Core, not TUI: `StraumrKeybinds` holds the action table and `StraumrKeybindPresets` the preset overlays, because the CLI's interactive prompts bind against the same actions. A new action needs a default entry and a row in `docs/keybinds.md`, which `settings.toml` points users to.

## On disk

| Path | Contents |
| --- | --- |
| `~/.straumr/settings.toml` | User settings; written from `StraumrSettings.Template` on first run. |
| `~/.straumr/state.json` | Active workspace, pane layouts, and the global secret index. |
| `~/.straumr/secrets/` | One JSONC file per secret. Plaintext by design. |
| `~/.straumr/themes/` | Installed and exported themes. |
| `<workspace root>/<name>/` | One folder per workspace; `<id>.straumr` files inside. |

Resources are JSONC and are meant to stay hand-editable. `IStraumrFileService` preserves comments across writes, so a rewrite must go through it. Requests and auths share a workspace directory and the `.straumr` extension, which is why the workspace manifest's membership sets are meaningful rather than redundant. `ReadStraumrModelAsync` updates `LastAccessed`; `PeekStraumrModelAsync` is the non-mutating read.

## Native AOT

The host publishes self-contained, single-file, AOT-compiled. That constrains two things. Serialization is source-generated: persisted models belong in `StraumrJsonContext`, CLI DTOs in `CliJsonContext`, and a newly serialised type needs an entry in one of them. Reflection is not: Spectre command and settings types are preserved through `Straumr.Console.Cli/CliRoots.xml`, so a change to CLI registration must keep that descriptor in step.
