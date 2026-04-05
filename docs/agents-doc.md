# Straumr For AI Agents

This document is an agent-focused operating guide for Straumr. It is optimized for scripted usage, machine parsing, and low-ambiguity workflows.

## Core Rules

- Prefer `--json` whenever a command supports it.
- Prefer IDs over names after discovery to avoid ambiguity.
- Use `--filter` to narrow lists before selecting an object.
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

If needed, activate one:

```sh
straumr use workspace <workspace-id-or-name>
```

### 2. Discover objects with filtering

Use list commands with `--json` and `--filter`:

```sh
straumr list request --json --filter users
straumr list auth --json --filter prod
straumr list secret --json --filter token
```

Filter behavior:

- name: case-insensitive substring match
- ID: case-insensitive prefix match

### 3. Resolve to a single ID

After listing, select one object ID and prefer that ID in later commands:

```sh
straumr get request <id> --json
straumr get auth <id> --json
straumr get workspace <id> --json
straumr get secret <id> --json
```

`get ... --json` returns the raw persisted JSON file, not a summarized DTO.

### 4. Validate before sending

Use dry-run JSON to inspect the exact resolved request shape:

```sh
straumr send <request-id> --dry-run --json
```

This is the safest preflight command for agents because it resolves secrets and shows the outgoing method, URI, headers, params, and body without making the network call.

### 5. Send with a machine-readable envelope

```sh
straumr send <request-id> --json
```

Add flags only when needed:

- `--fail` to convert HTTP `>= 400` into exit code `22`
- `--location` to follow redirects
- `--insecure` to ignore TLS validation
- `--output <file>` only when a file is specifically needed

## Machine-Readable Commands

### Best commands for parsing

- `straumr list workspace --json`
- `straumr list request --json`
- `straumr list auth --json`
- `straumr list secret --json`
- `straumr get <type> <id> --json`
- `straumr send <request-id> --dry-run --json`
- `straumr send <request-id> --json`

### Avoid for parsing

- default `list`
- default `get`
- default `send`

Those render Spectre.Console tables and panels intended for humans.

## JSON Shapes

CLI-generated JSON uses PascalCase property names.

Raw persisted files returned by `get ... --json` also use PascalCase property names from the storage model, such as `Id`, `Name`, `Method`, `BodyType`, and `Bodies`.

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

### Error envelope

Commands that emit JSON on failure use this shape:

```json
{
  "Error": {
    "Message": "..."
  }
}
```

## Persisted JSON Notes

When editing or generating raw request JSON for `create ... --editor` or `edit ... --editor`, note these serialization details:

- `Method` is an object: `{ "Method": "GET" }`
- `BodyType` is stored as a numeric enum value
- `Bodies` uses enum-name keys such as `Json`, `Xml`, `Text`, `Form`, `Multipart`, `Raw`
- request and auth IDs must remain stable during edit

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
- `1`: common failures such as missing workspace, missing entry, invalid input, or send failure in JSON mode
- `22`: `send --fail` and HTTP status is `>= 400`
- `-1`: generic or unhandled error path in many non-JSON flows

For robust scripting, prefer:

- `list --json`
- `get --json`
- `send --json`

## Safe Patterns

### Discover a workspace, activate it, inspect a request, then send it

```sh
straumr list workspace --json --filter demo
straumr use workspace <workspace-id>
straumr list request --json --filter users
straumr get request <request-id> --json
straumr send <request-id> --dry-run --json
straumr send <request-id> --json --fail
```

### Inspect auth linkage before sending

```sh
straumr get request <request-id> --json
straumr get auth <auth-id> --json
straumr send <request-id> --dry-run --json
```

### Locate a secret without exposing human-formatted output

```sh
straumr list secret --json --filter token
straumr get secret <secret-id> --json
```

## Agent Recommendations

- Always begin with workspace discovery if workspace state is not already known.
- Prefer `list ... --json --filter ...` over full list scans.
- Prefer exact IDs after discovery.
- Use dry-run before send when changing or validating requests.
- Expect warnings to exist outside the main JSON result in non-JSON modes; avoid non-JSON modes for automation.
- Do not assume request and auth commands work without an active workspace.
- Do not parse default console tables or panels.
