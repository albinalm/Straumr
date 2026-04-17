# Send Module

## Purpose

Implement the request execution view and response viewer using the CLI as the transport/backend.

## Responsibilities

- Send the selected request from the active workspace.
- Show request metadata, response metadata, and response body in a two-pane viewer.
- Support:
  - send
  - optional dry-run preview
  - copy pane
  - copy full template/export
  - beautify/revert body
  - save body
  - export full response
- Handle large responses and structured CLI failures without freezing the UI.

## Inputs

- Active workspace
- Selected request ID
- Send options for the screen

## Outputs

- Parsed send result DTO
- Parsed dry-run DTO when used
- Export/save actions
- Notifications and return navigation

## CLI interaction

Primary execution:

- `send <request-id> --json --workspace <ws>`

Optional preview:

- `send <request-id> --dry-run --json --workspace <ws>`

Do not shell out to non-JSON send modes.

## Boundaries

- Owns response rendering and response export UX.
- Does not mutate saved request data except through explicit request-module actions.
- Relies on `cli-client` for large-output buffering and JSON decode.

## UX notes

- Preserve the familiar dedicated send screen and split summary/body layout.
- Keep `j/k`, `g/G`, `Tab`, copy, beautify, and save/export behaviors.
- Improve responsiveness by rendering a spinner/status immediately while the subprocess runs.
- Improve failure clarity by showing exit code plus parsed `Contents.Message`.

## References

- Existing TUI screen: `src/Straumr.Console.Tui/Screens/SendScreen.cs`
- Send contract: `docs/agents-doc.md`
- Output behavior: `docs/command-reference.md`

