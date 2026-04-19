# Request Module Status

## Scope

Request list, inspect, create, edit, copy, delete, and send-view handoff.

## Current status

In progress. The view package exposes structured draft/result APIs, and the shell now drives create/edit/copy/delete plus auth/body quick-flow fields through the JSON-safe request commands.

## Completed work

- Mapped current request screen and editor behavior.
- Defined required request/auth CLI calls.
- Verified request edit inline flags in command code.
- Identified large-body input as a contract concern.
- Added request draft extraction and explicit editor open/close helpers in the Go view package.
- Added request list rendering and send handoff hooks in the Go shell.
- Added quick create/edit request flows in the shell for name, URL, method, auth, and body/body type using `get/create/edit request --json`.
- Wired request copy/delete through the shell to the typed CLI client.

## Work in progress

- Extending the request edit flow to cover header/param overlays and richer inspect/detail handling.

## Blockers

- Large inline payloads need a safe CLI input path before final implementation.

## Files touched

- `docs/tui/request-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/app/**`
- `src/straumr-tui/internal/views/request/**`

## Important decisions

- Keep request editing inside the TUI instead of defaulting to external editor mode.
- Delegate actual request execution to the send module.

## Next steps

- Add header/param editing overlays that feed the structured request draft.
- Improve request inspect/detail handling around the seeded draft flow.

## Resume notes

- Request create/edit now works as a quick flow for name, URL, method, auth, and body/body type.
- The remaining request gap is header/param editing plus richer detail polish.
