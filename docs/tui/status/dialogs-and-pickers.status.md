# Dialogs And Pickers Status

## Scope

Shared overlays, forms, selectors, confirmations, key/value editor, and redesigned path/file pickers.

## Current status

Go overlay helpers are present with structured result types.

## Completed work

- Defined shared overlay role.
- Defined file-picker redesign direction.
- Defined keyboard consistency requirements.
- Added overlay open/close helpers plus structured selection/input/path result types.
- Added the Go dialog surfaces for selects, confirms, inputs, key/value editing, and path picking.

## Work in progress

- None.

## Blockers

- None beyond overall implementation start.
- Shell-level presentation and callback wiring still needs to be completed.

## Files touched

- `docs/tui/dialogs-and-pickers.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/views/dialogs/**`

## Important decisions

- Path dialogs should not behave like mini file managers.
- Destructive delete should not live inside save/open dialogs.

## Next steps

- Connect the shell to the new overlay result types.
- Replace any remaining ad hoc path/file prompts with the shared picker primitives.

## Resume notes

- This module is now the shared overlay surface the shell can mount for workspace, request, auth, secret, and send flows.
