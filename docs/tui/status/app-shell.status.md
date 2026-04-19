# App Shell Status

## Scope

Bubble Tea root model, navigation, shared screen state, refresh rules, global key routing.

## Current status

In progress. Root Bubble Tea shell is in place, the main screens are wired, and several real mutation flows now round-trip through the CLI.

## Completed work

- Defined root-model responsibilities and boundaries.
- Mapped startup behavior to existing TUI behavior.
- Mapped shared keybindings and screen transitions.
- Implemented the Go root model, startup binary resolution, shell routing, and workspace/request/auth/secret/send screen switching.
- Added the session/state primitives, key translation helpers, and overlay hooks that the feature views consume.
- Wired workspace create/rename/copy/delete through overlay-backed shell flows.
- Wired secret create/edit/copy/delete through overlay-backed shell flows.
- Wired request create/edit quick flows plus request copy/delete through overlay-backed shell flows.
- Wired send refresh and dry-run into the send screen.

## Work in progress

- Completing auth create/edit submission and the richer request edit overlays.

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

- Finish auth create/edit shell flows using the type-aware auth mutation drafts.
- Extend request editing beyond name/url/method into auth/body/header/param overlays.
- Complete send save/export actions.

## Resume notes

- The shell already owns the mutation round-trip for workspace, secret, and the quick request flows.
- The next shell-critical slice is auth create/edit, then richer request editing.
