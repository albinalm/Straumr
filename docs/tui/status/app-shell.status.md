# App Shell Status

## Scope

Bubble Tea root model, navigation, shared screen state, refresh rules, global key routing.

## Current status

Started. Root Bubble Tea scaffold is in place.

## Completed work

- Defined root-model responsibilities and boundaries.
- Mapped startup behavior to existing TUI behavior.
- Mapped shared keybindings and screen transitions.
- Implemented the Go root model, startup binary resolution, shell routing, and workspace/request screen switching.
- Added the session/state primitives and key translation helpers that the feature views consume.

## Work in progress

- Integrating the root shell with the remaining auth/secret/send feature packages as they land.

## Blockers

- Parallel feature view packages are still landing in separate work.
- Large-body request editing remains an open future concern.

## Files touched

- `docs/tui/app-shell.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/cmd/straumr-tui/main.go`
- `src/straumr-tui/internal/app/bootstrap.go`
- `src/straumr-tui/internal/app/commands.go`
- `src/straumr-tui/internal/app/messages.go`
- `src/straumr-tui/internal/app/navigation.go`
- `src/straumr-tui/internal/app/root_model.go`
- `src/straumr-tui/internal/state/session.go`
- `src/straumr-tui/internal/ui/keymap.go`
- `src/straumr-tui/internal/ui/layout.go`

## Important decisions

- Single root Bubble Tea model with child screens and modal overlays.
- Navigation owns context only; entity modules own their own data flows.

## Next steps

- Wire the remaining view packages into the shell and extend the refresh/mutation command handlers.

## Resume notes

- The root shell is functional for workspaces and requests; next work is feature integration.
