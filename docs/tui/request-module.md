# Request Module

## Purpose

Implement the request list, request editing, request inspection, and request copy/delete flows for the currently selected workspace.

## Responsibilities

- Show the request list with method, host, body type, auth summary, and last-accessed metadata.
- Support:
  - create
  - edit
  - copy
  - delete
  - inspect
  - open send view
- Provide an internal request editor that mirrors the current TUI workflow:
  - name
  - URL
  - method
  - params
  - headers
  - body
  - linked auth

## Inputs

- Active workspace
- Request list DTOs
- Request details DTOs
- Auth list DTOs for auth selection

## Outputs

- Updated request list/detail state
- Mutation subprocess requests
- Send-screen navigation requests

## CLI interaction

Read:

- `list request --json --workspace <ws>`
- `get request <id> --json --workspace <ws>`
- `list auth --json --workspace <ws>` for auth picker

Mutations:

- `create request <name> <url> [flags] --json --workspace <ws>`
- `edit request <id> [inline flags] --json --workspace <ws>`
- `copy request <id> <new-name> --json --workspace <ws>`
- `delete request <id> --json --workspace <ws>`

Send handoff:

- push request ID and workspace into the send module

Contract concern:

- Inline `--data` is sufficient for small/medium bodies, but very large payloads can exceed OS argument limits. A CLI file/stdin-based body input contract is recommended before final implementation.

## Boundaries

- Owns request CRUD UI and request-specific editor state.
- Delegates response execution/viewing to `send-module`.
- Delegates subprocess execution to `cli-client`.

## UX notes

- Keep the current keyboard-first list behavior and familiar summary line.
- Improve clarity in the editor by using structured overlays instead of repeated nested menus where possible.
- Keep auth selection stateless by resolving auths from the active workspace on demand.

## References

- Existing TUI screen: `src/Straumr.Console.Tui/Screens/RequestsScreen.cs`
- Existing request editor: `src/Straumr.Console.Tui/Services/RequestEditor.cs`
- Existing body editor: `src/Straumr.Console.Tui/Services/BodyEditor.cs`
- Request CLI contract: `docs/agents-doc.md`, `docs/command-reference.md`
- Verified command implementations:
  - `src/Straumr.Console.Cli/Commands/Request/RequestCreateCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Request/RequestEditCommand.cs`
