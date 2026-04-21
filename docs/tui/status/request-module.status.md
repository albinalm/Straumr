# Request Module Status

## Scope

Request list, inspect, create, edit, copy, delete, and send-view handoff.

## Current status

In progress. The view package exposes structured draft/result APIs, and the shell now drives inspect/create/edit/copy/delete plus auth/body/header/param quick-flow fields through the JSON-safe request commands.

## Completed work

- Mapped current request screen and editor behavior.
- Defined required request/auth CLI calls.
- Verified request edit inline flags in command code.
- Identified large-body input as a contract concern.
- Added request draft extraction and explicit editor open/close helpers in the Go view package.
- Added request list rendering and send handoff hooks in the Go shell.
- Wired request inspect through `get request --json` into the shared scrollable text viewer overlay with sectioned headers, params, auth ID, timestamps, and body preview.
- Replaced request auth selection with a CLI-backed picker using `list auth --json --workspace <ws>`.
- Replaced request method entry with a shared select overlay plus custom-method fallback.
- Replaced request body-type entry with a shared select overlay for the supported body modes.
- Replaced the plain request-body prompt with a dedicated multiline body overlay and file-load path for larger payloads.
- Replaced the request header/param add-edit path with a shared pair editor overlay instead of separate key and value prompts.
- Added quick create/edit request flows in the shell for name, URL, method, auth, body/body type, headers, and params using `get/create/edit request --json`.
- Wired request copy/delete through the shell to the typed CLI client.
- Wired the embedded request editor submit path through the same JSON-safe request mutation commands instead of leaving it as a shell placeholder.
- Polished the request view/editor rendering so the editor groups fields more clearly.

## Work in progress

- Refining request detail presentation and remaining structured-overlay ergonomics around the shared inspect viewer and list/detail layout.

## Blockers

- Very large request bodies still ultimately depend on the current CLI `--data` contract even though the TUI body authoring path is better now.

## Files touched

- `docs/tui/request-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/app/**`
- `src/straumr-tui/internal/views/request/**`

## Important decisions

- Keep request editing inside the TUI instead of defaulting to external editor mode.
- Delegate actual request execution to the send module.

## Next steps

- Refine the request detail presentation and overlay UX now that inspect and auth/body-type pickers are wired.
- Decide whether remaining request fields should stay sequential or move to a more structured form overlay.

## Resume notes

- Request create/edit now works as a quick flow for name, URL, method, auth, body/body type, headers, and params.
- The embedded request editor submit path no longer dead-ends if the shell mounts it.
- Request inspect is live through the JSON-safe get path and now renders inside the shared read-only text viewer overlay.
- Request method/auth/body type now use shared selectors, request body editing has a dedicated overlay plus file import, request headers/params use a shared pair editor overlay, and the remaining gap is mostly detail polish.
