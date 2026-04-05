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
straumr list workspace|ws
straumr list request|rq
straumr list auth|au
straumr list secret|sc
```

Common patterns:

- `--json` emits arrays of DTOs, not raw saved JSON files.
- `--filter` matches name substring or ID prefix.

### `create`

```text
straumr create workspace|ws <Name> [--output <DIR>]
straumr create request|rq [Name] [Url] [request options]
straumr create auth|au [Name]
straumr create secret|sc [Name] [Value]
```

Request-only options:

- `--method`
- `--header`
- `--param`
- `--data`
- `--type`
- `--auth`
- `--editor`

### `delete`

```text
straumr delete workspace|ws <Name or ID>
straumr delete request|rq <Name or ID>
straumr delete auth|au <Name or ID>
straumr delete secret|sc <Name or ID>
```

### `edit`

```text
straumr edit workspace|ws <Name or ID>
straumr edit request|rq <Name or ID> [--editor]
straumr edit auth|au <Name or ID> [--editor]
straumr edit secret|sc <Name or ID>
```

Notes:

- workspace edit is editor-only
- secret edit is editor-only
- request and auth edit support interactive and editor modes

### `get`

```text
straumr get workspace|ws <Name or ID> [--json]
straumr get request|rq <Name or ID> [--json]
straumr get auth|au <Name or ID> [--json]
straumr get secret|sc <Name or ID> [--json]
```

Behavior split:

- `--json` prints the raw persisted JSON file
- default output is a formatted Spectre panel with summary fields

### `use`

```text
straumr use workspace|ws <Name or ID>
```

### `copy`

```text
straumr copy workspace|ws <Identifier> <NewName> [--output <DIR>]
```

### `import`

```text
straumr import workspace|ws <Path>
```

### `export`

```text
straumr export workspace|ws <Name or ID> <Output folder>
```

### `config`

```text
straumr config workspace-path [path]
```

Behavior:

- with no argument, prints the configured default workspace path
- with a path, saves that path into options

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

## Output Modes

### List Commands

`list` commands return summarized DTOs in JSON mode. They do not dump the underlying persisted files.

### Get Commands

`get ... --json` prints the exact file contents for the selected object.

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
  "Body": "{\"ok\":true}"
}
```

On send-time failures in JSON mode, Straumr writes an error envelope:

```json
{
  "Error": {
    "Message": "..."
  }
}
```

## Exit Codes

Observed and explicit behaviors in the code:

- `0`: success
- `1`: common user-facing failures such as missing workspace, missing entry, invalid input, or request transport failure in JSON mode
- `22`: `send --fail` with HTTP status `>= 400`
- `-1`: unhandled or generic error paths in many non-JSON commands

Because several commands return `-1` for generic exceptions, scripting against Straumr is cleanest with `send --json`, `list --json`, and `get --json` where possible.
