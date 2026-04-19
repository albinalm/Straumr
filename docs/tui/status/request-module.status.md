# Request Module Status

## Scope

Request list, inspect, create, edit, copy, delete, and send-view handoff.

## Current status

Go view package is present with structured draft/result APIs; shell mutation wiring is in progress.

## Completed work

- Mapped current request screen and editor behavior.
- Defined required request/auth CLI calls.
- Verified request edit inline flags in command code.
- Identified large-body input as a contract concern.
- Added request draft extraction and explicit editor open/close helpers in the Go view package.
- Added request list rendering and send handoff hooks in the Go shell.

## Work in progress

- None.

## Blockers

- Large inline payloads need a safe CLI input path before final implementation.
- The shell still needs the final request submission/mutation plumbing.

## Files touched

- `docs/tui/request-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/views/request/**`

## Important decisions

- Keep request editing inside the TUI instead of defaulting to external editor mode.
- Delegate actual request execution to the send module.

## Next steps

- Wire request draft submission into the shell mutation path.
- Connect auth selection and body editing overlays to the request editor.

## Resume notes

- The request view is no longer a placeholder; it already exposes structured draft data for the shell to consume.
