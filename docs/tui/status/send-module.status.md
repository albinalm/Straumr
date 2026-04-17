# Send Module Status

## Scope

Request send flow, optional dry-run preview, response viewer, copy/beautify/export/save actions.

## Current status

Designed. Implementation not started.

## Completed work

- Mapped existing send screen behavior and shortcuts.
- Defined JSON-only send strategy.
- Defined large-output handling requirements.
- Confirmed shared JSON stderr envelope helper in CLI source.

## Work in progress

- None.

## Blockers

- None beyond CLI binary resolution and general project scaffolding.

## Files touched

- `docs/tui/send-module.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`

## Important decisions

- Do not use non-JSON send output.
- Do not run dry-run automatically before every send.

## Next steps

- Implement after request list selection flow exists.

## Resume notes

- Reuse shared viewport and file-save dialog primitives.
