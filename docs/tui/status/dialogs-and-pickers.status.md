# Dialogs And Pickers Status

## Scope

Shared overlays, forms, selectors, confirmations, key/value editor, and redesigned path/file pickers.

## Current status

In progress. Go overlay helpers are present with structured result types, and the shared path picker is now mounted by the shell for send save/export flows.

## Completed work

- Defined shared overlay role.
- Defined file-picker redesign direction.
- Defined keyboard consistency requirements.
- Added overlay open/close helpers plus structured selection/input/path result types.
- Added the Go dialog surfaces for selects, confirms, inputs, key/value editing, and path picking.
- Expanded the shared path picker to handle typed path editing, focus between path/quick locations/browsable entries, and clearer help text.
- Wired the shell to use the shared path picker for send save/export flows.

## Work in progress

- Reusing the shared picker in more shell flows and tightening picker ergonomics after first integration.

## Blockers

- None beyond remaining shell adoption work.

## Files touched

- `docs/tui/dialogs-and-pickers.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `src/straumr-tui/internal/views/dialogs/**`

## Important decisions

- Path dialogs should not behave like mini file managers.
- Destructive delete should not live inside save/open dialogs.

## Next steps

- Replace any remaining ad hoc path/file prompts with the shared picker primitives.
- Decide whether the picker needs active-workspace quick locations from shell state.

## Resume notes

- This module is now the shared overlay surface the shell can mount for workspace, request, auth, secret, and send flows.
- The shared path picker is already in use for send save/export; the next pass should extend that reuse rather than creating new one-off path prompts.
