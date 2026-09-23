# Command reference

A map of the whole command tree. `straumr <command> --help` is authoritative for the version you have installed; this
page is for finding your way around.

## Global notes

- Running `straumr` with no arguments opens the terminal UI. A CLI-only build (`-p:IncludeTui=false`) prints help
  instead.
- `straumr about` prints a banner, the version, and the project description.
- Global flags are handled before command parsing:
    - `--version` prints the version and exits.
    - `--no-color` disables ANSI colour and terminal links.
    - `--agent-help` prints an automation guide written for AI agents and scripts.
- Command nouns have short aliases:
    - `workspace` -> `ws`
    - `request` -> `rq`
    - `auth` -> `au`
    - `variable` -> `vr`
    - `secret` -> `sc`

## Command tree

### `list`

```text
straumr list workspace|ws [--json] [--filter <str>]
straumr list request|rq [--json] [--filter <str>] [-w|--workspace <name-or-id>]
straumr list auth|au [--json] [--filter <str>] [-w|--workspace <name-or-id>]
straumr list variable|vr [--json] [--filter <str>] [-w|--workspace <name-or-id>]
straumr list secret|sc [--json] [--filter <str>]
```

Common patterns:

- `--json` emits arrays of DTOs.
- `--filter` matches name substring or ID prefix.
- `--workspace` targets a workspace without changing global state (request and auth only).

### `create`

```text
straumr create workspace|ws <Name> [-o|--output <DIR>] [-j|--json]
straumr create request|rq [Name] [Url] [request options] [-j|--json] [-w|--workspace <name-or-id>]
straumr create auth|au [Name] [auth options] [-j|--json] [-w|--workspace <name-or-id>]
straumr create variable|vr [Name] [Value] [-w|--workspace <name-or-id>]
straumr create secret|sc [Name] [Value]
```

`create workspace` needs either `--output` or `paths.workspaces` in `settings.toml`; without both it fails.

Request options:

- `-m|--method`
- `-H|--header` (repeatable, `"Name: Value"` format)
- `-P|--param` (repeatable, `"key=value"` format)
- `-d|--data`
- `-t|--type` (body type: `json`, `xml`, `text`, `form`, `multipart`, `raw`)
- `-a|--auth`
- `-e|--editor`

Request names may contain spaces but cannot contain a double quote (`"`). Core enforces this invariant for create, copy,
and save/edit operations.

Auth options for non-interactive creation (requires `--type`):

- `-t|--type <type>` — `bearer`, `basic`, `oauth2`, `oauth2-client-credentials`, `oauth2-authorization-code`,
  `oauth2-password`, `custom`
- `-s|--secret <value>` — token for bearer auth
- `--prefix <prefix>` — header prefix for bearer (default: `Bearer`)
- `-u|--username <user>` — for basic auth or OAuth2 password grant
- `-p|--password <pass>` — for basic auth or OAuth2 password grant
- `-g|--grant <grant>` — OAuth2 grant type when `--type` is `oauth2`: `client-credentials`, `authorization-code`,
  `password`
- `--token-url <url>` — OAuth2 token endpoint URL
- `--client-id <id>` — OAuth2 client ID
- `--client-secret <secret>` — OAuth2 client secret
- `--scope <scope>` — OAuth2 scope
- `--authorization-url <url>` — OAuth2 authorization URL (authorization code grant)
- `--redirect-uri <uri>` — OAuth2 redirect URI (default: `http://localhost:8765/callback`)
- `--pkce <mode>` — PKCE mode: `S256`, `plain`, `disabled`
- `--custom-url <url>` — custom auth request URL
- `--custom-method <method>` — custom auth request method (default: `POST`)
- `--custom-header <header>` — custom auth request header in `"Name: Value"` format (repeatable)
- `--custom-param <param>` — custom auth request param in `"key=value"` format (repeatable)
- `--custom-body <body>` — custom auth request body content
- `--custom-body-type <type>` — custom auth body type: `json`, `xml`, `text`, `form`, `multipart`, `raw` (default:
  `json`)
- `--extraction-source <source>` — custom auth extraction source: `jsonpath`, `header`, `regex`
- `--extraction-expression <expr>` — custom auth extraction expression
- `--apply-header-name <name>` — custom auth header name to apply (default: `Authorization`)
- `--apply-header-template <template>` — custom auth header value template with `{{value}}` placeholder (default:
  `Bearer {{value}}`)
- `--no-auto-renew` — disable auto-renewal

When `--json` is passed, `create` outputs the new object as a JSON DTO instead of a human-readable confirmation.

### `delete`

```text
straumr delete workspace|ws <Name or ID> [-j|--json]
straumr delete request|rq <Name or ID> [-j|--json] [-w|--workspace <name-or-id>]
straumr delete auth|au <Name or ID> [-j|--json] [-w|--workspace <name-or-id>]
straumr delete variable|vr <Name or ID> [-w|--workspace <name-or-id>]
straumr delete secret|sc <Name or ID>
```

`--json` on delete suppresses the human-readable confirmation; errors are still emitted as JSON to stderr. Exit code is
the signal of success.

### `edit`

```text
straumr edit workspace|ws <Name or ID> [-j|--json]
straumr edit request|rq <Name or ID> [inline options] [-e|--editor] [-j|--json] [-w|--workspace <name-or-id>]
straumr edit auth|au <Name or ID> [-e|--editor] [-j|--json] [-w|--workspace <name-or-id>]
straumr edit variable|vr <Name or ID> [-j|--json] [-w|--workspace <name-or-id>]
straumr edit secret|sc <Name or ID> [-j|--json]
```

Notes:

- workspace edit is editor-only; `--json` emits `{Id, Name, Path}` on success
- secret and variable edit are editor-only; `--json` emits `{Id, Name, Status}` on success
- auth edit supports interactive and editor modes; `--json` implies `--editor` and emits `{Id, Name, Type}` on success
- request edit supports interactive, editor, and inline modes; `--json` emits `{Id, Name, Method, Uri}` on success;
  implies `--editor` when no inline flags are set

In all edit commands, `--json` routes errors to the JSON envelope on stderr.

Request inline edit options (presence of any triggers non-interactive mode):

- `-n|--name`
- `-u|--url`
- `-m|--method`
- `-H|--header` (repeatable, `"Name: Value"` format)
- `-P|--param` (repeatable, `"key=value"` format)
- `-d|--data`
- `-t|--type` (body type: `json`, `xml`, `text`, `form`, `multipart`, `raw`, `none`)
- `-a|--auth` (auth name or ID; use `none` to remove auth)
- `-j|--json` — output the updated request as `{Id, Name, Method, Uri}`; implies `--editor` when no inline flags are
  set; errors emitted as JSON to stderr

### `get`

```text
straumr get workspace|ws <Name or ID> [--json]
straumr get request|rq <Name or ID> [--json] [-w|--workspace <name-or-id>]
straumr get auth|au <Name or ID> [--json] [-w|--workspace <name-or-id>]
straumr get variable|vr <Name or ID> [--json] [-w|--workspace <name-or-id>]
straumr get secret|sc <Name or ID> [--json]
```

Behavior split:

- `--json` prints a normalized DTO for the selected object
- default output is a formatted Spectre panel with summary fields

Note: `get request --json` returns a normalized DTO (PascalCase, `Method` as string, `BodyType` as enum name) — not the
raw persisted file. Use `--editor` with `edit request` to access the raw file format.

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
straumr copy variable|vr <Identifier> <NewName> [-j|--json] [-w|--workspace <name-or-id>]
straumr copy secret|sc <Identifier> <NewName> [-j|--json]
```

`copy workspace --json` emits `{Id, Name, Path}`. `copy request --json` emits `{Id, Name, Method, Uri}`.
`copy auth --json` emits `{Id, Name, Type}`. `copy secret --json` and `copy variable --json` emit `{Id, Name, Status}`
(status is always `Valid` for the newly created entry).

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
straumr config workspace-path [-j|--json]
```

Behavior:

- prints the configured default workspace path, and where to set it
- `--json` outputs `{ "DefaultWorkspacePath": "..." }` (value is `null` if not set)
- passing a path is refused with exit code `1` and a pointer to `settings.toml`; the value is set there, under
  `[paths]` as `workspaces = "..."`, not by this command

### `autocomplete`

```text
straumr autocomplete install [-s|--shell zsh|bash|pwsh] [-p|--profile <FILE>] [-a|--alias <name>...]
```

Hidden:

```text
straumr autocomplete query <shell-generated query string>
```

### `about`

```text
straumr about
```

Prints the banner, the version, and the project description.

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

`--header` and `--param` on `send` are transient: they apply to this invocation only and do not modify the saved
request.

## Output modes

### List commands

`list` commands return summarized DTOs in JSON mode. They do not dump the underlying persisted files.

### Get commands

`get request --json` prints a normalized DTO with these characteristics:

- `Method` is a plain string (e.g. `"GET"`)
- `BodyType` is the enum name (e.g. `"Json"`, `"None"`)
- `Body` is the active body content string for the current `BodyType`, not the full `Bodies` map

`get workspace/secret --json` prints the model deserialized from the persisted file via the service layer. The shape
matches the on-disk format.

`get auth --json` returns the full auth model. The `Config` field includes an `AuthType` discriminator (`Bearer`,
`Basic`, `OAuth2`, `Custom`) and the fields for that type.

### Send JSON envelope

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

`Body` is an inlined JSON object when the response `Content-Type` contains `json`. For non-JSON responses, `Body` is a
JSON string.

On failure in JSON mode, all commands write an error envelope to stderr:

```json
{
  "Contents": {
    "Message": "..."
  }
}
```

Read the error message from `Contents.Message`.

### Create JSON output

`create workspace|request|auth --json` emits a DTO for the created object: `{Id, Name, Path}` for a workspace,
`{Id, Name, Method, Uri}` for a request, `{Id, Name, Type}` for an auth.

### Config JSON output

`config workspace-path --json` emits:

```json
{
  "DefaultWorkspacePath": "/path/to/workspaces"
}
```

## Exit codes

Observed and explicit behaviors in the code:

- `0`: success
- `1`: user-facing failures — missing workspace, missing entry, invalid input, or transport failure
- `22`: `send --fail` with HTTP status `>= 400`
- `-1`: unhandled or generic exception path

Scripting against Straumr is cleanest with `send --json`, `list --json`, and `get --json` where exit `1` covers all
expected failure cases.
