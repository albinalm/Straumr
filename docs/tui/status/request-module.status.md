# Request Module Status

## Scope

Request list, inspect, create, edit, copy, delete, and send-view handoff.

## Current status

In progress. The view package exposes structured draft/result APIs, and the shell now drives create/edit/copy/delete through the JSON-safe request commands.

## Completed work

- Mapped current request screen and editor behavior.
- Defined required request/auth CLI calls.
- Verified request edit inline flags in command code.
- Identified large-body input as a contract concern.
- Added request draft extraction and explicit editor open/close helpers in the Go view package.
- Added request list rendering and send handoff hooks in the Go shell.
- Added quick create/edit request flows in the shell for name, URL, and method using `get/create/edit request --json`.
- Wired request copy/delete through the shell to the typed CLI client.

## Work in progress

- Extending the request edit flow to cover auth selection, body editing, and header/param overlays.

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

- Connect auth selection and body editing overlays to the request editor flow.
- Add header/param editing overlays that feed the structured request draft.

## Resume notes

- Request create/edit now works as a quick flow for name, URL, and method.
- Richer request editing still needs shell-side overlays for the remaining draft fields.
