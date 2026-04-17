# Secret Module

## Purpose

Implement global secret management with safe display rules and subprocess-backed CRUD operations.

## Responsibilities

- Show the global secret list with status and timestamps.
- Support:
  - create
  - edit
  - copy
  - delete
  - inspect
- Keep secret values masked in list views and masked by default in detail/edit overlays.

## Inputs

- Secret list DTOs
- Secret detail DTOs
- User actions from secret screen and overlays

## Outputs

- Updated secret state
- Mutation subprocess requests
- Notifications for success/failure

## CLI interaction

Read:

- `list secret --json`
- `get secret <id> --json`

Mutations available now:

- `create secret <name> <value> --json`
- `edit secret [--name] [--value] --json`
- `copy secret <id> <new-name> --json`
- `delete secret <id> --json`

Structured contract notes:

- `create secret --json` requires both name and value and does not prompt.
- `edit secret --json` returns `SecretListItem` on stdout when `--name` and/or `--value` is supplied.
- `delete secret --json` returns a small `{Id, Name}` success object.
- When no inline edit flags are present, the existing editor-backed secret edit flow remains available.

## Boundaries

- Owns secret screen rendering and masking rules.
- Does not resolve secrets inside requests/auths; that remains backend behavior.
- Does not store secret values outside the current UI session.

## UX notes

- Keep the screen global rather than workspace-scoped.
- Prefer masking and explicit reveal over always-visible values.
- Make copy and rename flows quick, because secret duplication is a common safe operation.

## References

- Existing TUI screen: `src/Straumr.Console.Tui/Screens/SecretsScreen.cs`
- Secret workflow docs: `docs/user-workflows.md`
- Data model docs: `docs/data-model.md`
- Proposed contract delta: `docs/tui/cli-contract-changes.md`
- Verified command implementations:
  - `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`
