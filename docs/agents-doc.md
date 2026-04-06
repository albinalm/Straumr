# Straumr For AI Agents

This document is an agent-focused operating guide for Straumr. It is optimized for scripted usage, machine parsing, and low-ambiguity workflows.

## Core Rules

- Prefer `--json` whenever a command supports it.
- Prefer IDs over names after discovery to avoid ambiguity.
- Use `--filter` to narrow lists before selecting an object.
- Use `--workspace <name-or-id>` to target a workspace without mutating global state — never run `use workspace` in scripts.
- Treat workspaces as required context for most request and auth operations.
- Use `send --dry-run --json` before `send --json` when validating what Straumr will dispatch.
- Expect human-oriented output unless `--json` is explicitly requested.

## Mental Model

Straumr manages four object types:

- `workspace`: isolated container for requests and auth definitions
- `request`: saved HTTP request in the active workspace
- `auth`: reusable auth definition in the active workspace
- `secret`: global secret store shared across workspaces

Most `request` and `auth` commands require an active workspace.

## Recommended Agent Workflow

### 1. Confirm workspace context

List available workspaces:

```sh
straumr list workspace --json
```

Target a specific workspace for subsequent commands without changing global state:

```sh
straumr list request --json --workspace <workspace-id-or-name>
```

If you must set a persistent workspace (interactive sessions only), use:

```sh
straumr use workspace <workspace-id-or-name>
```

### 2. Bootstrap config if missing

If `DefaultWorkspacePath` is not set, `create workspace` (without `-o`) will fail. Detect and fix this programmatically:

```sh
straumr config workspace-path --json
# returns { "DefaultWorkspacePath": null } if not configured

straumr config workspace-path /path/to/workspaces --json
# returns { "DefaultWorkspacePath": "/path/to/workspaces" }
```

### 3. Create objects non-interactively

#### Workspace

```sh
straumr create workspace my-ws --json
```

Returns:

```json
{
  "Id": "...",
  "Name": "my-ws",
  "Path": "/path/to/workspaces/my-ws"
}
```

#### Request

```sh
straumr create request get-users https://api.example.com/users \
  --method GET \
  --header "Accept: application/json" \
  --param "page=1" \
  --workspace my-ws \
  --json
```

Returns:

```json
{
  "Id": "...",
  "Name": "get-users",
  "Method": "GET",
  "Uri": "https://api.example.com/users"
}
```

#### Auth (bearer or basic)

```sh
straumr create auth prod-key -t bearer -s mytoken --workspace my-ws --json
straumr create auth prod-basic -t basic -u user -p pass --workspace my-ws --json
```

Returns:

```json
{
  "Id": "...",
  "Name": "prod-key",
  "Type": "Bearer"
}
```

### 4. Discover objects with filtering

Use list commands with `--json` and `--filter`:

```sh
straumr list request --json --filter users --workspace my-ws
straumr list auth --json --filter prod --workspace my-ws
straumr list secret --json --filter token
```

Filter behavior:

- name: case-insensitive substring match
- ID: case-insensitive prefix match

### 5. Resolve to a single ID

After listing, select one object ID and prefer that ID in later commands:

```sh
straumr get request <id> --json --workspace my-ws
straumr get auth <id> --json --workspace my-ws
straumr get workspace <id> --json
straumr get secret <id> --json
```

### 6. Edit requests inline

Update specific fields without entering the interactive TUI:

```sh
straumr edit request <id> --url https://api.example.com/v2/users
straumr edit request <id> --method POST --data '{"name":"Ada"}' --type json
straumr edit request <id> --header "X-Tenant: acme" --param "v=2"
straumr edit request <id> --auth <auth-id-or-name>
straumr edit request <id> --auth none
```

### 7. Validate before sending

Use dry-run JSON to inspect the exact resolved request shape:

```sh
straumr send <request-id> --dry-run --json --workspace my-ws
```

This resolves secrets and shows the outgoing method, URI, headers, params, and body without making the network call.

### 8. Send with a machine-readable envelope

```sh
straumr send <request-id> --json --workspace my-ws
```

Inject one-off headers or params without permanently editing the saved request:

```sh
straumr send <request-id> --json --header "X-Debug: true" --param "trace=1"
```

Add flags only when needed:

- `--fail` to convert HTTP `>= 400` into exit code `22`
- `--location` to follow redirects
- `--insecure` to ignore TLS validation
- `--output <file>` only when a file is specifically needed

## Machine-Readable Commands

### Best commands for parsing

Delete commands accept `--json` to suppress human-readable output and route any error to the JSON envelope on stderr. Rely on exit code (`0` = deleted) rather than parsing stdout.

```sh
straumr delete request <id> --json --workspace <ws>
straumr delete auth <id> --json --workspace <ws>
straumr delete workspace <id> --json
```

`edit request` in inline mode accepts `--json` and emits `{Id, Name, Method, Uri}` on success:

```sh
straumr edit request <id> --url https://api.example.com/v2/users --json --workspace <ws>
```



- `straumr config workspace-path --json`
- `straumr list workspace --json`
- `straumr create workspace <name> --json`
- `straumr copy workspace <name> <new-name> --json`
- `straumr import workspace <path> --json`
- `straumr export workspace <name-or-id> <dir> --json`
- `straumr list request --json [--workspace <ws>]`
- `straumr create request <name> <url> [flags] --json [--workspace <ws>]`
- `straumr copy request <name-or-id> <new-name> --json [--workspace <ws>]`
- `straumr get request <id> --json [--workspace <ws>]`
- `straumr list auth --json [--workspace <ws>]`
- `straumr create auth <name> --type bearer|basic [flags] --json [--workspace <ws>]`
- `straumr copy auth <name-or-id> <new-name> --json [--workspace <ws>]`
- `straumr get auth <id> --json [--workspace <ws>]`
- `straumr list secret --json`
- `straumr get secret <id> --json`
- `straumr send <request-id> --dry-run --json [--workspace <ws>]`
- `straumr send <request-id> --json [--workspace <ws>]`

### Avoid for parsing

- default `list`
- default `get`
- default `send`
- default `create`

Those render Spectre.Console tables and panels intended for humans.

## JSON Shapes

CLI-generated JSON uses PascalCase property names and `UnsafeRelaxedJsonEscaping` (no `\u0022` or `\u002F` escaping).

### `config workspace-path --json`

```json
{
  "DefaultWorkspacePath": "/path/to/workspaces"
}
```

`DefaultWorkspacePath` is `null` if not yet configured.

### `create workspace --json`

```json
{
  "Id": "8f6c7c80-2f8e-4a7f-92e3-a2e4c8f6d123",
  "Name": "my-ws",
  "Path": "/path/to/workspaces/my-ws"
}
```

### `list workspace --json`

```json
[
  {
    "Id": "8f6c7c80-2f8e-4a7f-92e3-a2e4c8f6d123",
    "Name": "demo",
    "Path": "/path/to/workspace/demo/<workspace-id>.straumr",
    "IsCurrent": true,
    "Requests": 3,
    "Status": "Valid",
    "LastAccessed": "2026-04-05T13:10:20"
  }
]
```

### `create request --json`

```json
{
  "Id": "865c9c5d-ef63-49ec-8a07-c73d84f9cd86",
  "Name": "get-users",
  "Method": "GET",
  "Uri": "https://api.example.com/users"
}
```

### `list request --json`

```json
[
  {
    "Id": "865c9c5d-ef63-49ec-8a07-c73d84f9cd86",
    "Name": "get-users",
    "Method": "GET",
    "Uri": "https://api.example.com/users",
    "Status": "Valid",
    "LastAccessed": "2026-04-05T13:10:29"
  }
]
```

### `get request --json`

Returns a normalized DTO (not the raw persisted file):

```json
{
  "Id": "865c9c5d-ef63-49ec-8a07-c73d84f9cd86",
  "Name": "get-users",
  "Method": "GET",
  "Uri": "https://api.example.com/users",
  "BodyType": "None",
  "Headers": {
    "Accept": "application/json"
  },
  "Params": {
    "page": "1"
  },
  "Body": null,
  "AuthId": "2c5967cd-71e6-4311-9150-fde7845c8cf0",
  "LastAccessed": "2026-04-05T13:10:29",
  "Modified": "2026-04-05T13:10:29"
}
```

Key differences from the persisted file:

- `Method` is a plain string, not an object
- `BodyType` is the enum name (`None`, `Json`, `Xml`, `Text`, `FormUrlEncoded`, `MultipartForm`, `Raw`), not an integer
- `Body` is the active body content for the current `BodyType`, not the full `Bodies` map

### `create auth --json`

```json
{
  "Id": "2c5967cd-71e6-4311-9150-fde7845c8cf0",
  "Name": "prod-key",
  "Type": "Bearer"
}
```

### `list auth --json`

```json
[
  {
    "Id": "2c5967cd-71e6-4311-9150-fde7845c8cf0",
    "Name": "prod-oauth",
    "Type": "OAuth 2.0"
  }
]
```

### `get auth --json`

Returns the full persisted auth model:

```json
{
  "Id": "2c5967cd-71e6-4311-9150-fde7845c8cf0",
  "Name": "prod-key",
  "Config": {
    "AuthType": "Bearer",
    "Token": "mytoken",
    "Prefix": "Bearer"
  },
  "AutoRenewAuth": true,
  "Modified": "2026-04-06T10:00:00+00:00",
  "LastAccessed": "2026-04-06T10:00:00+00:00"
}
```

`Config.AuthType` discriminator values: `Bearer`, `Basic`, `OAuth2`, `Custom`. The `Config` object shape varies by type.

### `list secret --json`

```json
[
  {
    "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
    "Name": "api-token",
    "Status": "Valid"
  }
]
```

### `send --dry-run --json`

```json
{
  "Method": "POST",
  "Uri": "https://example.com/api/users",
  "Auth": "prod-oauth (OAuth 2.0)",
  "Headers": {
    "Accept": "application/json"
  },
  "Params": {
    "page": "1"
  },
  "BodyType": "Json",
  "Body": "{\"name\":\"Ada\"}"
}
```

### `send --json`

`Body` is an inlined JSON object when the response `Content-Type` contains `json`; otherwise it is a JSON string:

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

Non-JSON response body example:

```json
{
  "Body": "plain text response"
}
```

### Error envelope

All commands that support `--json` write this envelope to **stderr** on failure:

```json
{
  "Error": {
    "Message": "..."
  }
}
```

Plain-text error output also goes to stderr in all commands, regardless of `--json`.

## Persisted JSON Notes

When editing or generating raw request JSON for `create ... --editor` or `edit ... --editor`, note these serialization details:

- `Method` is an object: `{ "Method": "GET" }`
- `BodyType` is stored as a numeric enum value
- `Bodies` uses enum-name keys such as `Json`, `Xml`, `Text`, `Form`, `Multipart`, `Raw`
- request and auth IDs must remain stable during edit

The CLI JSON output from `get ... --json` normalizes these fields. Only editor mode sees the raw format.

Example persisted request:

```json
{
  "Uri": "https://example.com/api/{{secret:api-token}}",
  "Method": {
    "Method": "POST"
  },
  "Params": {
    "q": "test"
  },
  "Headers": {
    "X-Test": "alpha"
  },
  "BodyType": 1,
  "Bodies": {
    "Json": "{\"hello\":\"world\"}"
  },
  "AuthId": null,
  "Id": "865c9c5d-ef63-49ec-8a07-c73d84f9cd86",
  "Name": "demo-request",
  "Modified": "2026-04-05T13:10:29.9314163+00:00",
  "LastAccessed": "2026-04-05T13:10:29.914483+00:00"
}
```

## Secret Resolution

Secret placeholder format:

```text
{{secret:<name>}}
```

Resolution behavior:

- placeholders can appear in URLs, headers, params, bodies, and auth config fields
- unresolved placeholders generate warnings instead of immediate hard failure
- successful resolution updates secret access timestamps

Agents should use `send --dry-run --json` or `send --json` to validate that secret-backed requests resolve as expected.

## Exit Codes

Use these for automation:

- `0`: success
- `1`: user-facing failure — missing workspace, entity not found, invalid input, or transport failure
- `22`: `send --fail` and HTTP status is `>= 400`
- `-1`: generic or unhandled exception path

For robust scripting, prefer:

- `list --json`
- `get --json`
- `send --json`

Exit `1` covers all expected failure cases in `--json` mode. Exit `-1` should not occur under normal usage.

## Safe Patterns

### Full stateless workflow (no `use workspace`)

```sh
WS_ID=$(straumr list workspace --json --filter my-ws | jq -r '.[0].Id')
REQ_ID=$(straumr create request get-users https://api.example.com/users \
  --method GET --json --workspace "$WS_ID" | jq -r '.Id')
straumr send "$REQ_ID" --dry-run --json --workspace "$WS_ID"
straumr send "$REQ_ID" --json --fail --workspace "$WS_ID"
```

### Bootstrap workspace and auth for a new agent session

```sh
straumr config workspace-path /tmp/agent-ws --json
straumr create workspace agent-session --json
WS_ID=$(straumr list workspace --json --filter agent-session | jq -r '.[0].Id')
straumr create auth my-key -t bearer -s "$API_TOKEN" --workspace "$WS_ID" --json
```

### Send with one-off header injection

```sh
straumr send "$REQ_ID" --json --header "X-Request-Id: $(uuidgen)" --workspace "$WS_ID"
```

### Inspect auth linkage before sending

```sh
straumr get request "$REQ_ID" --json --workspace "$WS_ID"
# check AuthId, then:
straumr get auth "$AUTH_ID" --json --workspace "$WS_ID"
straumr send "$REQ_ID" --dry-run --json --workspace "$WS_ID"
```

### Locate a secret without exposing human-formatted output

```sh
straumr list secret --json --filter token
straumr get secret <secret-id> --json
```

## Agent Recommendations

- Always use `--workspace` instead of `straumr use workspace` to keep invocations stateless.
- Always begin with workspace discovery if workspace state is not already known.
- Prefer `list ... --json --filter ...` over full list scans.
- Prefer exact IDs after discovery.
- Use `create ... --json` to capture new object IDs without a follow-up list.
- Use dry-run before send when changing or validating requests.
- Use `send --header`/`--param` for transient overrides; do not permanently edit saved requests just to inject trace IDs.
- Expect warnings to exist outside the main JSON result in non-JSON modes; avoid non-JSON modes for automation.
- Do not assume request and auth commands work without an active workspace (via current or `--workspace`).
- Do not parse default console tables or panels.
