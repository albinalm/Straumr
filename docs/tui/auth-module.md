# Auth Module

## Purpose

Implement auth browsing and editing without reimplementing auth behavior, token logic, or custom-auth execution.

## Responsibilities

- Show workspace-local auths with type, auto-renew state, and timestamps.
- Support:
  - create
  - edit
  - copy
  - delete
  - inspect
- Present auth-type-specific forms for:
  - bearer
  - basic
  - OAuth 2.0
  - custom
- Keep the UI responsible only for collecting/editing data; runtime auth behavior remains in the CLI/core stack.

## Inputs

- Active workspace
- Auth list DTOs
- Auth details DTOs

## Outputs

- Updated auth state
- Request editor auth choices
- Mutation subprocess requests

## CLI interaction

Read:

- `list auth --json --workspace <ws>`
- `get auth <id> --json --workspace <ws>`

Mutations available now:

- `create auth ... --json --workspace <ws>`
- `edit auth ... --json --workspace <ws>`
- `copy auth <id> <new-name> --json --workspace <ws>`
- `delete auth <id> --json --workspace <ws>`

Structured edit contract:

- `edit auth` now supports inline mutation using the same config flag surface as `create auth`, plus `--name`, `--auto-renew`, and `--no-auto-renew`.
- `--json` returns `AuthListItem` on stdout.
- When no inline flags are present, the existing interactive/editor-backed flows remain available.

## Boundaries

- Owns auth screen rendering and auth form state.
- Does not fetch tokens, refresh tokens, or execute custom-auth requests itself.
- Does not store secrets outside normal CLI-auth fields.

## UX notes

- Preserve the existing high-level flow of “choose type, edit fields, save”.
- Improve readability by using per-type sections instead of deeply nested menu loops.
- Hide secret values by default in the form while still allowing deliberate reveal/edit actions.

## References

- Existing TUI screen: `src/Straumr.Console.Tui/Screens/AuthsScreen.cs`
- Existing auth editor: `src/Straumr.Console.Tui/Services/AuthEditor.cs`
- Auth workflow docs: `docs/user-workflows.md`
- CLI contract docs: `docs/agents-doc.md`, `docs/command-reference.md`
- Verified command implementation: `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
- Contract completion detail: `docs/tui/cli-contract-changes.md`
