# Straumr Go TUI Design

This directory contains the Phase 1 design and the Phase 3 foundation scaffold for `straumr-tui`, a Go replacement for `Straumr.Console.Tui`.

## Design goals

- Replace the existing .NET TUI with a Go TUI that feels familiar to current users.
- Preserve the established Straumr visual language from the existing TUI screenshots and theme system, not just the workflows.
- Keep the existing CLI/domain model: `workspace`, `request`, `auth`, `secret`.
- Use the `straumr` CLI as the only backend through discrete subprocess calls.
- Treat CLI JSON on stdout, structured JSON errors on stderr, and exit codes as the contract.
- Avoid parsing human-oriented CLI output.
- Separate CLI execution, state management, and UI rendering.

## Core workflows to preserve

- Start in `Requests` when an active workspace exists; otherwise start in `Workspaces`.
- Navigate between `Workspaces`, `Requests`, `Auths`, and `Secrets`.
- Filter, inspect, create, edit, copy, and delete entities with VIM-style navigation.
- Send a request and inspect/export/save the response.
- Handle damaged or missing entries without crashing the session.

## Main screens and overlays

- `Workspaces` list
- `Requests` list
- `Auths` list
- `Secrets` list
- `Send` response viewer
- Shared overlays for confirm/select/form/key-value edit/file-path pick/save

## Visual baseline

The visual baseline is part of the product contract. Future agents must reference:

- `README.md`
- `docs/media/tui-workspaces.png`
- `docs/media/tui-requests.png`
- `docs/media/tui-send.png`
- `docs/themes.md`

The Go TUI should preserve:

- top-left help strip
- top-right `STRAUMR` ASCII banner
- framed panels on a dark surface
- green primary emphasis
- blue info/method accents
- boxed send/detail layouts

## Proposed Go package layout

```text
cmd/straumr-tui/
internal/app/          root Bubble Tea model, startup, navigation, focus, refresh
internal/cli/          subprocess execution, JSON decoding, stderr envelope parsing
internal/cache/        list/detail caching and invalidation
internal/state/        screen state, selection state, optimistic refresh markers
internal/views/        workspace/request/auth/secret/send/dialog views
internal/ui/           theme, layout helpers, key maps, reusable widgets
```

## Current CLI contract status

The Phase 2 CLI contract completion work now provides structured JSON-safe paths for the previously missing TUI operations:

- `edit auth` supports inline structured mutation and `--json` output.
- `edit workspace` supports inline rename via `--name`.
- `edit secret` supports inline `--name` / `--value`.
- `create secret --json` is available and non-interactive.
- `delete secret --json` emits a JSON success object.

Remaining contract notes:

- `request edit` supports inline JSON-safe mutation, but very large bodies can still hit command-length limits.
- `delete workspace`, `delete request`, and `delete auth` accept `--json` but still use exit code rather than stdout JSON for success.

Go TUI implementation status:

- The root Bubble Tea scaffold, typed CLI exec layer, cache store, and startup routing are now in place.
- Existing workspace/request view packages remain separate and are consumed by the shell without modification.
- Auth/secret/send/dialog feature packages are being integrated in parallel.

## Document index

- `app-shell.md`
- `cli-client.md`
- `cli-contract-changes.md`
- `workspace-module.md`
- `request-module.md`
- `auth-module.md`
- `secret-module.md`
- `send-module.md`
- `dialogs-and-pickers.md`
- `visual-system.md`
- `IMPLEMENTATION_STATUS.md`
