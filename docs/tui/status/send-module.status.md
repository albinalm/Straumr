# Send Module Status

## Scope

Request send flow, optional dry-run preview, response viewer, copy/beautify/export/save actions.

## Current status

In progress. The send screen is wired to real send and dry-run CLI calls.

## Completed work

- Mapped existing send screen behavior and shortcuts.
- Defined JSON-only send strategy.
- Defined large-output handling requirements.
- Confirmed shared JSON stderr envelope helper in CLI source.
- Added the Go send view surface for request/response rendering and pane focus.
- Wired the root shell to enter the send screen on request send actions.
- Wired `send --json` results into the response viewer.
- Wired `send --dry-run --json` into the send screen as a preview path.

## Work in progress

- Completing save/export/copy actions from the send screen.

## Blockers

- None beyond the remaining shell-side action wiring.

## Files touched

- `docs/tui/send-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/views/send/view.go`

## Important decisions

- Do not use non-JSON send output.
- Do not run dry-run automatically before every send.

## Next steps

- Complete copy/export/save actions.
- Add clearer dry-run/send affordances in the rendered view.

## Resume notes

- Transport is already live for both send and dry-run.
- The remaining send work is action completion and view polish.
