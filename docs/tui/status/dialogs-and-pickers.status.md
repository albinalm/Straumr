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
- Mounted the shared select overlay in the shell for request auth and body-type choice.
- Mounted the shared select overlay in the shell for auth create/edit type, grant, PKCE, custom body-type, and extraction-source choice.
- Mounted the shared select overlay in the shell for request method choice, with a custom-method fallback.
- Wired the shell to use the shared path picker for send save/export flows.
- Extended shared path-picker usage to workspace import/export flows.
- Reused the shared key/value overlay for custom-auth headers and params, not just request headers and params.
- Added a shared multiline body overlay and reused the shared path picker to load request bodies from files.

## Work in progress

- Reusing the shared picker in more shell flows and tightening picker ergonomics after first integration.
- Tightening overlay ergonomics after first integration and reusing the shared picker/select surfaces in the remaining ad hoc shell prompts.

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
- Replace any remaining ad hoc option-choice prompts with the shared select primitives.
- Decide whether the multiline body overlay should grow save/export affordances or stay focused on load/edit/accept.
- Decide whether the picker needs active-workspace quick locations from shell state.

## Resume notes

- This module is now the shared overlay surface the shell can mount for workspace, request, auth, secret, and send flows.
- The shared path picker is already in use for send save/export, and the shared select overlay is now in use for request auth/body-type choice.
- The shared path picker is already in use for send save/export and workspace import/export, and the shared select overlay is now in use for request auth/body-type choice.
- The shared select overlay now also covers the auth create/edit choice fields.
- Request bodies now use a dedicated multiline overlay, with the shared path picker available for file-backed load.
- The next pass should extend that reuse rather than creating new one-off prompts.
