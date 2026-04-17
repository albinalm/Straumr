# CLI Contract Completion

This document records the scoped CLI contract completion implemented before `straumr-tui` starts. The goal is to keep the TUI backend contract strict:

- JSON on stdout
- structured JSON error envelopes on stderr
- exit codes for status
- no editor dependency in the TUI path

The changes are intentionally minimal and additive. Existing prompt/editor workflows remain available for humans where they already existed.

## 1. `edit auth`

### Implemented behavior

- `straumr edit auth <id>` still opens the interactive prompt flow.
- `straumr edit auth <id> --editor` still opens the auth JSON in `$EDITOR`.
- `straumr edit auth <id> [inline flags] --json` now performs structured inline mutation without an editor dependency.
- `straumr edit auth <id> --json` with no inline flags still falls back to the existing editor-backed path.

Source of truth:

- `src/Straumr.Console.Cli/Commands/Auth/AuthEditCommand.cs`

### CLI syntax

```text
straumr edit auth <identifier> [--name <name>] [auth config flags] [--auto-renew|--no-auto-renew] --json [-w|--workspace <ws>]
```

Auth config flags should match the existing `create auth` contract:

- `--type`
- `--secret`
- `--prefix`
- `--username`
- `--password`
- `--grant`
- `--token-url`
- `--client-id`
- `--client-secret`
- `--scope`
- `--authorization-url`
- `--redirect-uri`
- `--pkce`
- `--custom-url`
- `--custom-method`
- `--custom-header`
- `--custom-param`
- `--custom-body`
- `--custom-body-type`
- `--extraction-source`
- `--extraction-expression`
- `--apply-header-name`
- `--apply-header-template`

Example:

```text
straumr edit auth prod-key --name prod-key-v2 --type bearer --secret mytoken --prefix Bearer --auto-renew --json --workspace demo
```

### JSON success shape

Reuse the existing auth result DTO:

```json
{
  "Id": "2c5967cd-71e6-4311-9150-fde7845c8cf0",
  "Name": "prod-key-v2",
  "Type": "Bearer"
}
```

### Failure behavior

- Exit `1` for invalid input, workspace missing, auth not found, or invalid auth configuration.
- Write the structured error envelope to stderr:

```json
{
  "Contents": {
    "Message": "..."
  }
}
```

- Preserve existing non-JSON behavior for prompt/editor usage.

### Additive and backward-compatible

- Yes.
- The new inline path is additive.
- Existing prompt mode and explicit `--editor` usage can remain unchanged.

## 2. `edit workspace`

### Implemented behavior

- `straumr edit workspace <id> --name <new-name> --json` now performs a structured inline rename.
- `straumr edit workspace <id>` without `--name` keeps the existing temp-file editor flow.

Source of truth:

- `src/Straumr.Console.Cli/Commands/Workspace/WorkspaceEditCommand.cs`

### CLI syntax

```text
straumr edit workspace <identifier> --name <new-name> --json
```

Example:

```text
straumr edit workspace demo --name demo-staging --json
```

### JSON success shape

Reuse the existing workspace result DTO:

```json
{
  "Id": "8f6c7c80-2f8e-4a7f-92e3-a2e4c8f6d123",
  "Name": "demo-staging",
  "Path": "/path/to/workspaces/demo/8f6c7c80-2f8e-4a7f-92e3-a2e4c8f6d123.straumr"
}
```

Notes:

- The minimal change only requires updating the persisted workspace name.
- Path migration/rename can remain a separate future enhancement.

### Failure behavior

- Exit `1` for workspace not found, duplicate workspace name, or invalid input.
- Write the structured error envelope to stderr.

### Additive and backward-compatible

- Yes.
- Existing editor-based behavior can remain when `--name` is not used.

## 3. `edit secret`

### Implemented behavior

- `straumr edit secret <id> [--name <new-name>] [--value <new-value>] --json` now performs structured inline mutation.
- `straumr edit secret <id>` without inline flags keeps the existing editor-backed flow.

Source of truth:

- `src/Straumr.Console.Cli/Commands/Secret/SecretEditCommand.cs`

### CLI syntax

```text
straumr edit secret <identifier> [--name <new-name>] [--value <new-value>] --json
```

Example:

```text
straumr edit secret api-token --value supersecret --json
```

### JSON success shape

Reuse the existing secret result DTO:

```json
{
  "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
  "Name": "api-token",
  "Status": "Valid"
}
```

### Failure behavior

- Exit `1` for secret not found, duplicate secret name, or invalid input.
- Write the structured error envelope to stderr.

### Additive and backward-compatible

- Yes.
- Inline mode is additive; current editor mode can remain as the fallback when no inline flags are used.

## 4. `create secret --json`

### Implemented behavior

- `straumr create secret [Name] [Value]` creates the secret.
- If arguments are omitted, it still prompts interactively in non-JSON mode.
- `straumr create secret <name> <value> --json` is now available.

Source of truth:

- `src/Straumr.Console.Cli/Commands/Secret/SecretCreateCommand.cs`

### CLI syntax

```text
straumr create secret <name> <value> --json
```

Example:

```text
straumr create secret api-token supersecret --json
```

### JSON success shape

Reuse the existing secret result DTO:

```json
{
  "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
  "Name": "api-token",
  "Status": "Valid"
}
```

### Failure behavior

- Exit `1` for missing name/value in JSON mode, duplicate name, or invalid input.
- Write the structured error envelope to stderr.

### Additive and backward-compatible

- Yes.
- Existing prompt behavior can remain for non-JSON usage.

## 5. `delete secret --json`

### Implemented behavior

- `straumr delete secret <id>` still deletes the secret with human-readable output in default mode.
- `straumr delete secret <id> --json` now emits a JSON success object on stdout.

Source of truth:

- `src/Straumr.Console.Cli/Commands/Secret/SecretDeleteCommand.cs`

### CLI syntax

```text
straumr delete secret <identifier> --json
```

Example:

```text
straumr delete secret api-token --json
```

### JSON success shape

Use a minimal delete result:

```json
{
  "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
  "Name": "api-token"
}
```

### Failure behavior

- Exit `1` for secret not found.
- Write the structured error envelope to stderr.

### Additive and backward-compatible

- Yes.
- Existing non-JSON behavior remains unchanged.

## Adjacent consistency note

Outside the scoped completion above, current `delete workspace`, `delete request`, and `delete auth` still accept `--json` but only suppress human-readable output; they do not emit JSON success objects on stdout.

That note remains documented for consistency, but it is intentionally out of scope for this phase.
