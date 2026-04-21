# App Shell Module

## Purpose

Define the Bubble Tea root application, top-level navigation, shared state, refresh rules, and common key handling for `straumr-tui`.

## Responsibilities

- Bootstrap the session by resolving the CLI binary and initial workspace context.
- Start on `Requests` when a current workspace exists; otherwise start on `Workspaces`.
- Maintain the active screen, prior screen context, focused pane, and overlay stack.
- Preserve the established shell composition:
  - top-left help strip
  - top-right `STRAUMR` banner
  - framed main content panels
  - bottom summary/status affordances
- Preserve familiar navigation patterns:
  - `j/k`, `g/G`, `/`, `:`
  - `Enter`/`o` open
  - `i` inspect
  - screen-local action keys such as `c`, `d`, `e`, `y`, `s`
- Own cache invalidation after successful mutations.
- Route background command results back into screen state without blocking rendering.

## Inputs

- Startup environment:
  - resolved `straumr` executable path
  - terminal size
  - theme information
- CLI results from `cli-client`
- User key events

## Outputs

- Bubble Tea commands for subprocess execution
- Screen state transitions
- Overlay open/close events
- Status and error notifications

## CLI interaction

- Startup uses:
  - `straumr list workspace --json`
  - `straumr list request --json --workspace <ws>` when current workspace can be resolved
- Navigation itself does not call the CLI.
- Screen transitions may request fresh data through `cli-client`.

## Boundaries

- Does not decode CLI JSON directly; that belongs to `cli-client`.
- Does not know entity-specific mutation details; those belong to the module docs below.
- Does not implement storage/business logic.
- Should consume semantic styles from `visual-system.md` rather than hardcoding its own colors or frame treatment.

## Proposed Go structure

- `internal/app/root_model.go`
- `internal/app/navigation.go`
- `internal/app/messages.go`
- `internal/state/session.go`

Use a single root `tea.Model` with child screen models and modal overlays. Prefer explicit message types over implicit shared mutable state.

## References

- Visual contract: `docs/tui/visual-system.md`, `README.md`, `docs/themes.md`
- Existing shell/navigation: `src/Straumr.Console.Tui/Screens/Base/ModelScreen.cs`
- Screen engine: `src/Straumr.Console.Tui/Infrastructure/ScreenEngine.cs`
- Navigation context: `src/Straumr.Console.Tui/Infrastructure/ScreenNavigationContext.cs`
- CLI contract: `docs/agents-doc.md`, `docs/command-reference.md`
