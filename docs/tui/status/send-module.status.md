# Send Module Status

## Scope

Request send flow, optional dry-run preview, response viewer, copy/beautify/export/save actions.

## Current status

Go response-view package is present and the shell can hand off requests to it.

## Completed work

- Mapped existing send screen behavior and shortcuts.
- Defined JSON-only send strategy.
- Defined large-output handling requirements.
- Confirmed shared JSON stderr envelope helper in CLI source.
- Added the Go send view surface for request/response rendering and pane focus.
- Wired the root shell to enter the send screen on request send actions.

## Work in progress

- None.

## Blockers

- None beyond CLI binary resolution and general project scaffolding.
- Response mutation/export/save actions still need shell-side completion.

## Files touched

- `docs/tui/send-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/views/send/view.go`

## Important decisions

- Do not use non-JSON send output.
- Do not run dry-run automatically before every send.

## Next steps

- Wire CLI send responses into the response viewer.
- Complete copy/export/save actions and dry-run handling.

## Resume notes

- The send view already renders the response shell; the remaining work is transport and action wiring.
