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
- Replaced the plain request-body prompt with a dedicated multiline body overlay and file-load path.
- Replaced request and custom-auth metadata add/edit prompts with a shared pair editor overlay.
- Consolidated request/workspace/auth/secret inspect into the same reusable scrollable read-only text viewer overlay with sectioned detail formatting.
- Wired the embedded request editor submit path through the same JSON-safe request mutation commands instead of a placeholder shell message.
- Wired request inspect through `get request --json` into the shared read-only text viewer overlay.
- Replaced request auth and body-type free-form text steps with shared select overlays, with auth choices loaded from `list auth --json`.
- Replaced auth create/edit choice-like text steps with shared select overlays for auth type, OAuth2 grant, PKCE, custom body type, and extraction source.
- Wired custom-auth headers and params through the shared key/value overlay before the custom body/extraction steps.
- Wired workspace/auth/secret inspect through their respective `get ... --json` commands into the shared read-only text viewer overlay.
- Wired workspace import/export through the shell, with export/import path collection using the shared path picker.
- Wired send refresh, dry-run, save-body, export, clipboard copy, and view-level beautify/revert behavior into the send screen.
- Stopped shadowing send beautify/revert with a generic shell status message so the send view owns that presentation behavior end-to-end.
- Corrected request/send on-screen key hints where they had drifted from the actual shell keymap.
- Replaced the send save/export raw path text input with the shared path-picker overlay and shell-managed filesystem browsing.
- Tightened post-mutation refresh behavior so create/edit/copy flows reselect the affected item after refresh and request edits no longer leave stale active-request state behind.
- Added the visual shell header with screen tabs, workspace/request context, and the screenshot-aligned top-right `STRAUMR` banner through the new theme system.

## Work in progress

- General shell cleanup and visual polish now that the theme system is active.
- Further inspect/detail presentation polish now that the shell has a reusable viewer instead of per-entity one-off detail handling.

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

- Tighten the shell/header spacing and narrow-width behavior against live manual screenshots.
- Keep tightening shell cleanup around mutation follow-up behavior now that selection preservation is in place.
- Reuse shared overlays anywhere the shell still falls back to text entry for obviously finite choices or path selection.

## Resume notes

- The shell already owns the mutation round-trip for workspace, auth, secret, and the request quick flows including auth/body/header/param.
- Workspace/auth/secret/request inspect now round-trip through their JSON-safe get paths.
- Request method/auth/body-type and auth create/edit choice fields now use select overlays, request bodies use a dedicated multiline overlay with file import, request/custom-auth metadata entries use a shared pair editor overlay, workspace/request/auth/secret inspect all use the same scrollable text viewer, workspace import/export plus send save/export mount the shared path picker, and mutation refresh now preserves the affected selection instead of dropping the user back onto whichever row was previously focused.
- The shell also now owns the top-level themed header/tab/banner presentation, so future visual work should extend that path instead of styling around it in individual screens.
- The next shell-critical slice is narrower visual polish and any remaining overlay reuse.
