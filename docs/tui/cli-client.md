# CLI Client Module

## Purpose

Provide a single subprocess gateway to the `straumr` CLI and expose typed request/response helpers for the Go TUI.

## Responsibilities

- Resolve and execute the `straumr` binary with discrete subprocess calls.
- Build argument lists without shell parsing.
- Capture stdout and stderr separately.
- Decode JSON stdout only for commands explicitly invoked with `--json`.
- Decode structured stderr envelopes from `Contents.Message`.
- Normalize exit-code handling for success, user errors, and `send --fail`.
- Cache list results and invalidate them on successful mutation.
- Spill large stdout/stderr payloads to temp files when they exceed an in-memory threshold.

## Inputs

- Command name plus argument slice
- Context/cancellation
- Optional workspace ID/name for stateless calls

## Outputs

- Typed DTOs for list/get/create/copy/edit/send responses
- Structured command errors:
  - exit code
  - parsed CLI error message when available
  - raw stderr fallback text

## CLI interaction

Read/list:

- `config workspace-path --json`
- `list workspace --json`
- `list request --json --workspace <ws>`
- `list auth --json --workspace <ws>`
- `list secret --json`
- `get request --json --workspace <ws>`
- `get auth --json --workspace <ws>`
- `get secret --json`
- `get workspace --json`

Mutations:

- `create/copy/delete ... --json`
- `create secret <name> <value> --json`
- `edit request ... --json --workspace <ws>`
- `edit auth ... --json --workspace <ws>`
- `edit workspace --name <name> --json`
- `edit secret [--name] [--value] --json`
- `send <id> --json --workspace <ws>`
- `send <id> --dry-run --json --workspace <ws>` only on explicit preview/validation flows

Current contract notes:

- The scoped CRUD gaps for `auth`, `workspace`, and `secret` are implemented with additive structured paths.
- `delete workspace`, `delete request`, and `delete auth` still accept `--json` without a stdout success object; callers must use exit code for success there.
- Large request/auth payloads still need a file-or-stdin-safe mutation path to avoid command-length limits.

The exact scoped contract completion is documented in `docs/tui/cli-contract-changes.md`.

## Boundaries

- Does not own screen state.
- Does not own keybindings or layout.
- Does not translate DTOs into presentation strings beyond minimal normalization.

## Proposed Go structure

- `internal/cli/client.go`
- `internal/cli/exec.go`
- `internal/cli/errors.go`
- `internal/cli/types.go`
- `internal/cache/store.go`

## References

- Agent contract: `docs/agents-doc.md`
- Command tree: `docs/command-reference.md`
- Existing CLI integration: `src/Straumr.Console.Cli/Integration/CliConsoleIntegration.cs`
- Error envelope type: `src/Straumr.Console.Cli/Models/CliErrorMessage.cs`
- Proposed delta spec: `docs/tui/cli-contract-changes.md`
- Command implementations:
  - `src/Straumr.Console.Cli/Commands/Request/RequestEditCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`
  - `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`
