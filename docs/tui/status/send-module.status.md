# Send Module Status

## Scope

Request send flow, optional dry-run preview, response viewer, copy/beautify/export/save actions.

## Current status

In progress. The send screen is wired to real send, dry-run, save-body, export, clipboard copy, and beautify/revert presentation behavior.

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
- Swapped send save/export target entry from raw text input to the shared path-picker overlay.
- Wired copy-pane and copy-template into shell-side clipboard actions.
- Beautify/revert now changes the rendered body presentation and pretty-prints valid JSON responses.

## Work in progress

- Improving overall send-view polish and affordances around the now-shared export/save picker flow.

## Blockers

- None beyond remaining view polish.

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
- Refine the remaining send-view polish and ergonomics.

## Resume notes

- Transport is already live for send, dry-run, save-body, export, and copy.
- Send save/export now use the shared picker rather than ad hoc path text entry.
- Beautify/revert is now a real presentation feature; the remaining send work is polish.
