# Send Module Status

## Scope

Request send flow, optional dry-run preview, response viewer, copy/beautify/export/save actions.

## Current status

In progress. The send screen is wired to real send, dry-run, save-body, export, and clipboard copy actions.

## Completed work

- Mapped existing send screen behavior and shortcuts.
- Defined JSON-only send strategy.
- Defined large-output handling requirements.
- Confirmed shared JSON stderr envelope helper in CLI source.
- Added the Go send view surface for request/response rendering and pane focus.
- Wired the root shell to enter the send screen on request send actions.
- Wired `send --json` results into the response viewer.
- Wired `send --dry-run --json` into the send screen as a preview path.
- Wired save-body and export into file-backed shell actions.
- Wired copy-pane and copy-template into shell-side clipboard actions.

## Work in progress

- Polishing beautify and revert behavior from the send screen.

## Blockers

- None beyond the remaining send-pane formatting polish.

## Files touched

- `docs/tui/send-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/app/commands.go`
- `src/straumr-tui/internal/app/overlay_flows.go`
- `src/straumr-tui/internal/app/root_model.go`
- `src/straumr-tui/internal/views/send/view.go`

## Important decisions

- Do not use non-JSON send output.
- Do not run dry-run automatically before every send.

## Next steps

- Add clearer dry-run/send affordances in the rendered view.
- Improve beautify/revert formatting behavior.

## Resume notes

- Transport is already live for send, dry-run, save-body, export, and copy.
- The remaining send work is view polish and formatting behavior.
