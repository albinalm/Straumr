# Dialogs And Pickers Status

## Scope

Shared overlays, forms, selectors, confirmations, key/value editor, and redesigned path/file pickers.

## Current status

In progress. Go overlay helpers are present with structured result types, themed rendering, and the shell now mounts the shared path picker plus the reusable text viewer for real flows.

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
- Added a shared pair editor overlay for request/auth metadata entries so key and value can be edited in one step.
- Added a shared read-only text viewer overlay for scrollable inspect/detail presentation across workspace, request, auth, and secret screens.
- Added first-pass themed dialog rendering so inputs, selectors, pickers, confirms, pair editors, and text viewers no longer appear as raw terminal text.

## Work in progress

- Reusing the shared picker in more shell flows and tightening picker ergonomics after first integration.
- Reducing visual duplication between dialog-local styling helpers and the shared theme package as the semantic style API matures.

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
- Decide whether the shared pair editor is sufficient long term or if a richer table/form editor is still warranted.
- Decide whether the picker needs active-workspace quick locations from shell state.
- Fold any obviously reusable dialog style roles back into `internal/ui/theme` rather than growing a second long-term style system under `internal/views/dialogs`.

## Resume notes

- This module is now the shared overlay surface the shell can mount for workspace, request, auth, secret, and send flows.
- The shared path picker is already in use for send save/export, and the shared select overlay is now in use for request auth/body-type choice.
- The shared path picker is already in use for send save/export and workspace import/export, and the shared select overlay is now in use for request auth/body-type choice.
- The shared select overlay now also covers the auth create/edit choice fields.
- Request bodies now use a dedicated multiline overlay, with the shared path picker available for file-backed load.
- Request and custom-auth metadata entries now use a shared pair editor overlay.
- Workspace/request/auth/secret inspect now reuse the same read-only text viewer overlay instead of entity-specific detail prompts.
- The next pass should extend that reuse rather than creating new one-off prompts.
