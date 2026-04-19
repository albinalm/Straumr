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
- Replaced request method entry with a shared select overlay plus custom-method fallback.
- Wired the embedded request editor submit path through the same JSON-safe request mutation commands instead of a placeholder shell message.
- Wired request inspect through `get request --json` into a read-only details overlay.
- Replaced request auth and body-type free-form text steps with shared select overlays, with auth choices loaded from `list auth --json`.
- Replaced auth create/edit choice-like text steps with shared select overlays for auth type, OAuth2 grant, PKCE, custom body type, and extraction source.
- Wired custom-auth headers and params through the shared key/value overlay before the custom body/extraction steps.
- Wired workspace/auth/secret inspect through their respective `get ... --json` commands into read-only detail overlays.
- Wired workspace import/export through the shell, with export/import path collection using the shared path picker.
- Wired send refresh, dry-run, save-body, export, clipboard copy, and view-level beautify/revert behavior into the send screen.
- Stopped shadowing send beautify/revert with a generic shell status message so the send view owns that presentation behavior end-to-end.
- Replaced the send save/export raw path text input with the shared path-picker overlay and shell-managed filesystem browsing.

## Work in progress

- General overlay polish and broader shared picker/select reuse outside the send screen, request editor, and the now-picker-backed auth create/edit flow.

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
- Reuse the shared select overlay in any remaining shell prompts that still fall back to simple text entry for finite choices.
- Tighten cache invalidation and refresh rules around successful mutations.

## Resume notes

- The shell already owns the mutation round-trip for workspace, auth, secret, and the request quick flows including auth/body/header/param.
- Workspace/auth/secret/request inspect now round-trip through their JSON-safe get paths.
- Request method/auth/body-type and auth create/edit choice fields now use select overlays, custom auth headers/params use the shared key/value overlay, and workspace import/export plus send save/export mount the shared path picker.
- The next shell-critical slice is broader overlay reuse and general shell cleanup.
