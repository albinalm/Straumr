# Command Reference

This is a code-verified summary of the command tree configured in `Program.cs`.

## Global Notes

- Running `straumr` with no arguments prints a banner and version panel.
- Global `--no-color` is handled before Spectre command parsing and disables ANSI and link output.
- Command nouns have short aliases:
  - `workspace` -> `ws`
  - `request` -> `rq`
  - `auth` -> `au`
  - `secret` -> `sc`

## Command Tree

### `list`

```text
straumr list workspace|ws [--json] [--filter <str>]
straumr list request|rq [--json] [--filter <str>] [-w|--workspace <name-or-id>]
straumr list auth|au [--json] [--filter <str>] [-w|--workspace <name-or-id>]
straumr list secret|sc [--json] [--filter <str>]
```

Common patterns:

- `--json` emits arrays of DTOs.
- `--filter` matches name substring or ID prefix.
- `--workspace` targets a workspace without changing global state (request and auth only).

### `create`

```text
straumr create workspace|ws <Name> [--output <DIR>] [-j|--json]
straumr create request|rq [Name] [Url] [request options] [-j|--json] [-w|--workspace <name-or-id>]
straumr create auth|au [Name] [auth options] [-j|--json] [-w|--workspace <name-or-id>]
straumr create secret|sc [Name] [Value]
```

Request options:

- `--method`
- `-H|--header` (repeatable, `"Name: Value"` format)
- `-P|--param` (repeatable, `"key=value"` format)
- `--data`
- `--type` (body type: `json`, `xml`, `text`, `form`, `multipart`, `raw`)
- `--auth`
- `-e|--editor`

Auth options for non-interactive creation (requires `--type`):

- `-t|--type bearer|basic`
- `-s|--secret <value>` — token for bearer auth
- `--prefix <prefix>` — header prefix for bearer (default: `Bearer`)
- `-u|--username <user>` — for basic auth
- `-p|--password <pass>` — for basic auth
- `--no-auto-renew` — disable auto-renewal

When `--json` is passed, `create` outputs the new object as a JSON DTO instead of a human-readable confirmation.

### `delete`

```text
straumr delete workspace|ws <Name or ID> [-j|--json]
straumr delete request|rq <Name or ID> [-j|--json] [-w|--workspace <name-or-id>]
straumr delete auth|au <Name or ID> [-j|--json] [-w|--workspace <name-or-id>]
straumr delete secret|sc <Name or ID>
```

`--json` on delete suppresses the human-readable confirmation; errors are still emitted as JSON to stderr. Exit code is the signal of success.

### `edit`

```text
straumr edit workspace|ws <Name or ID>
straumr edit request|rq <Name or ID> [inline options] [-e|--editor]
straumr edit auth|au <Name or ID> [-e|--editor]
straumr edit secret|sc <Name or ID>
```

Notes:

- workspace edit is editor-only
- secret edit is editor-only
- auth edit supports interactive and editor modes
- request edit supports interactive, editor, and inline modes

Request inline edit options (presence of any triggers non-interactive mode):

- `-u|--url`
- `-m|--method`
- `-H|--header` (repeatable, `"Name: Value"` format)
- `-P|--param` (repeatable, `"key=value"` format)
- `-d|--data`
- `-t|--type` (body type: `json`, `xml`, `text`, `form`, `multipart`, `raw`, `none`)
- `-a|--auth` (auth name or ID; use `none` to remove auth)
- `-j|--json` — output the updated request as `{Id, Name, Method, Uri}` instead of a human-readable confirmation (inline mode only)

### `get`

```text
straumr get workspace|ws <Name or ID> [--json]
straumr get request|rq <Name or ID> [--json] [-w|--workspace <name-or-id>]
straumr get auth|au <Name or ID> [--json] [-w|--workspace <name-or-id>]
straumr get secret|sc <Name or ID> [--json]
```

Behavior split:

- `--json` prints a normalized DTO for the selected object
- default output is a formatted Spectre panel with summary fields

Note: `get request --json` returns a normalized DTO (PascalCase, `Method` as string, `BodyType` as enum name) — not the raw persisted file. Use `--editor` with `edit request` to access the raw file format.

### `use`

```text
straumr use workspace|ws <Name or ID>
```

Sets the global active workspace. Prefer `--workspace` in scripts to avoid mutating global state.

### `copy`

```text
straumr copy workspace|ws <Identifier> <NewName> [--output <DIR>] [-j|--json]
straumr copy request|rq <Identifier> <NewName> [-j|--json] [-w|--workspace <name-or-id>]
straumr copy auth|au <Identifier> <NewName> [-j|--json] [-w|--workspace <name-or-id>]
```

`copy workspace --json` emits `{Id, Name, Path}`. `copy request --json` emits `{Id, Name, Method, Uri}`. `copy auth --json` emits `{Id, Name, Type}`.

### `import`

```text
straumr import workspace|ws <Path> [-j|--json]
```

`--json` emits `{Id, Name, Path}` for the imported workspace.

### `export`

```text
straumr export workspace|ws <Name or ID> <Output folder> [-j|--json]
```

`--json` emits `{Path}` for the exported archive.

### `config`

```text
straumr config workspace-path [path] [-j|--json]
```

Behavior:

- with no argument, prints the configured default workspace path
- with a path, saves that path into options
- `--json` outputs `{ "DefaultWorkspacePath": "..." }` (value is `null` if not set)

### `autocomplete`

```text
straumr autocomplete install [--shell zsh|bash|pwsh] [--alias <name>...]
```

Hidden:

```text
straumr autocomplete query <shell-generated query string>
```

### `send`

```text
straumr send <Name or ID> [OPTIONS]
```

Supported options:

- `-v`
- `-p, --pretty`
- `-b, --beautify`
- `-k, --insecure`
- `-L, --location`
- `-o, --output`
- `-f, --fail`
- `-i, --include`
- `-s, --silent`
- `-j, --json`
- `-n, --dry-run`
- `--response-status`
- `--response-headers`
- `-H|--header` (repeatable) — add or override a header for this send only, `"Name: Value"` format
- `-P|--param` (repeatable) — add or override a query param for this send only, `"key=value"` format
- `-w|--workspace` — target workspace without changing global state

`--header` and `--param` on `send` are transient: they apply to this invocation only and do not modify the saved request.

## Output Modes

### List Commands

`list` commands return summarized DTOs in JSON mode. They do not dump the underlying persisted files.

### Get Commands

`get request --json` prints a normalized DTO with these characteristics:

- `Method` is a plain string (e.g. `"GET"`)
- `BodyType` is the enum name (e.g. `"Json"`, `"None"`)
- `Body` is the active body content string for the current `BodyType`, not the full `Bodies` map

`get workspace/secret --json` prints the model deserialized from the persisted file via the service layer. The shape matches the on-disk format.

`get auth --json` returns the full auth model. The `Config` field includes an `AuthType` discriminator (`Bearer`, `Basic`, `OAuth2`, `Custom`) and type-specific fields. See agents-doc.md for an example shape.

### Send JSON Envelope

`send --json` emits:

```json
{
  "Status": 200,
  "Reason": "OK",
  "Version": "1.1",
  "DurationMs": 123.4,
  "Headers": {
    "Content-Type": ["application/json"]
  },
  "Body": {"ok": true}
}
```

`Body` is an inlined JSON object when the response `Content-Type` contains `json`. For non-JSON responses, `Body` is a JSON string.

On failure in JSON mode, all commands write an error envelope to stderr:

```json
{
  "Error": {
    "Message": "..."
  }
}
```

### Create JSON Output

`create workspace|request|auth --json` emits a DTO for the created object. See agents-doc.md for the exact shapes.

### Config JSON Output

`config workspace-path --json` emits:

```json
{
  "DefaultWorkspacePath": "/path/to/workspaces"
}
```

## Exit Codes

Observed and explicit behaviors in the code:

- `0`: success
- `1`: user-facing failures — missing workspace, missing entry, invalid input, or transport failure
- `22`: `send --fail` with HTTP status `>= 400`
- `-1`: unhandled or generic exception path

Scripting against Straumr is cleanest with `send --json`, `list --json`, and `get --json` where exit `1` covers all expected failure cases.
