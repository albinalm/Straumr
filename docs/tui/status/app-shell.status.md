# App Shell Status

## Scope

Bubble Tea root model, navigation, shared screen state, refresh rules, global key routing.

## Current status

In progress. Root Bubble Tea shell is in place, the main screens are wired, and most real mutation flows now round-trip through the CLI.

## Completed work

- Defined root-model responsibilities and boundaries.
- Mapped startup behavior to existing TUI behavior.
- Mapped shared keybindings and screen transitions.
- Implemented the Go root model, startup binary resolution, shell routing, and workspace/request/auth/secret/send screen switching.
- Added the session/state primitives, key translation helpers, and overlay hooks that the feature views consume.
- Wired workspace create/rename/copy/delete through overlay-backed shell flows.
- Wired auth create/edit/copy/delete through overlay-backed shell flows.
- Wired secret create/edit/copy/delete through overlay-backed shell flows.
- Wired request create/edit quick flows plus request copy/delete through overlay-backed shell flows, including auth/body/header/param round-tripping.
- Wired request inspect through `get request --json` into a read-only details overlay.
- Wired send refresh, dry-run, save-body, export, clipboard copy, and view-level beautify/revert behavior into the send screen.
- Replaced the send save/export raw path text input with the shared path-picker overlay and shell-managed filesystem browsing.

## Work in progress

- General overlay polish and broader shared picker reuse outside the send screen.

## Blockers

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
- `src/straumr-tui/internal/views/**`

## Important decisions

- Single root Bubble Tea model with child screens and modal overlays.
- Navigation owns context only; entity modules own their own data flows.

## Next steps

- Reuse the shared path picker in more shell flows that still fall back to simple text entry.
- Tighten cache invalidation and refresh rules around successful mutations.

## Resume notes

- The shell already owns the mutation round-trip for workspace, auth, secret, and the request quick flows including auth/body/header/param.
- Request inspect now round-trips through `get request --json`, and send save/export now mount the shared path picker.
- The next shell-critical slice is broader picker reuse and general shell cleanup.
