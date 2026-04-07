# User Workflows

This document describes how Straumr is meant to be used from the terminal, including the state it expects to exist before commands work.

## Mental Model

Straumr has four primary object types:

- Workspaces: isolated collections of requests and auth definitions.
- Requests: saved HTTP requests that belong to the current workspace.
- Auths: reusable auth definitions that belong to the current workspace.
- Secrets: global sensitive values that can be referenced from requests and auth definitions.

Straumr is workspace-centric. Most `request` and `auth` commands require an active workspace before they can run. Commands that operate on requests or auths accept a `-w|--workspace <name-or-id>` flag to target a workspace for that invocation without changing the globally active one.

## First-Time Setup

Before creating a workspace without `-o`, set the default workspace root:

```sh
straumr config workspace-path /path/to/workspaces
```

That path is persisted in `~/.straumr/options.json`. Add `--json` to get a machine-readable result:

```sh
straumr config workspace-path ~/api-workspaces --json
# { "DefaultWorkspacePath": "/home/user/api-workspaces" }
```

Typical bootstrap flow:

```sh
straumr config workspace-path ~/api-workspaces
straumr create workspace myapi
straumr use workspace myapi
```

If no active workspace exists, request and auth commands fail with a missing-workspace error.

## Workspace Lifecycle

### Create

Create a workspace in the configured default path:

```sh
straumr create workspace myapi
straumr create workspace myapi --json   # outputs { "Id", "Name", "Path" }
```

Or override the target directory:

```sh
straumr create workspace myapi --output /tmp/workspaces
```

Creating a workspace writes a `.straumr` manifest file into a folder derived from the workspace name.

### Activate

Switch the current workspace:

```sh
straumr use workspace myapi
```

Activation updates `CurrentWorkspace` in the global options file and stamps the workspace `LastAccessed` timestamp.

### Inspect

Use the list and get commands to inspect workspaces:

```sh
straumr list workspace
straumr get workspace myapi
straumr get workspace myapi --json
```

`list workspace` shows registry status such as valid, corrupt, or missing. `get workspace --json` prints the raw workspace manifest.

### Edit

Workspace editing is file-based only:

```sh
EDITOR=nvim straumr edit workspace myapi
```

Straumr copies the manifest to a temp file, launches `$EDITOR`, and replaces the original if the editor exits cleanly.

### Copy, Export, Import, Delete

```sh
straumr copy workspace myapi myapi-staging
straumr export workspace myapi ./exports
straumr import workspace ./exports/myapi.straumrpak
straumr delete workspace myapi-staging
```

Important behavior:

- Copy duplicates the workspace manifest and all non-`.straumr` files in the workspace directory.
- Export creates a `.straumrpak` zip archive.
- Import expects a `.pak` metadata file plus exactly one workspace folder in the archive.
- Delete removes the entire workspace directory recursively and removes its registry entry from options.

## Request Workflow

Straumr supports three request-authoring modes.

### Interactive TUI Mode

Default `create request` opens a menu-driven prompt flow:

```sh
straumr create request get-users
```

From there you can set:

- name
- absolute URL
- HTTP method
- query params
- headers
- body type and content
- linked auth

Prompt UX details:

- menus support search
- `Escape` backs out of the current prompt
- body editing for freeform content uses `$EDITOR`

### Editor Mode

```sh
straumr create request get-users --editor
straumr edit request get-users --editor
```

This opens a temp JSON file and expects valid request JSON on save. Request IDs may not be changed during edit.

### Inline Mode

```sh
straumr create request get-users https://api.example.com/users \
  --method GET \
  --header "Accept: application/json" \
  --param "page=1"
```

Inline mode is enabled when a URL argument is present. It validates:

- headers as `Name: Value`
- params as `key=value`
- body type as one of `json`, `xml`, `text`, `form`, `multipart`, `raw`

Inline requests default to `GET`, and if `--data` is provided without `--type`, the body type defaults to `json`.

Add `--json` to capture the new request ID without a follow-up list:

```sh
straumr create request get-users https://api.example.com/users --method GET --json
# { "Id": "...", "Name": "get-users", "Method": "GET", "Uri": "..." }
```

### Inline Edit Mode

Update specific fields of an existing request without entering the interactive TUI:

```sh
straumr edit request get-users --url https://api.example.com/v2/users
straumr edit request get-users --method POST --data '{"name":"Ada"}' --type json
straumr edit request get-users --header "X-Tenant: acme" --param "v=2"
straumr edit request get-users --auth prod-key
straumr edit request get-users --auth none   # removes linked auth
```

Inline edit mode is triggered when any of `--url`, `--method`, `--header`, `--param`, `--data`, `--type`, or `--auth` is present. Inline and `--editor` are mutually exclusive.

### Request Bodies

Supported body types:

- `json`
- `xml`
- `text`
- `form`
- `multipart`
- `raw`

Interactive mode keeps one serialized body string per `BodyType`. For form and multipart bodies, Straumr stores a URL-encoded key/value string internally.

Multipart specifics:

- values beginning with `@` are treated as file paths when the request is sent
- missing multipart files raise an error before the HTTP request is dispatched

### Getting and Listing Requests

```sh
straumr list request
straumr list request --filter users
straumr get request get-users
straumr get request get-users --json
```

`get request` resolves secrets in the displayed URL and prints warnings if secrets cannot be found.

## Auth Workflow

Auth definitions are reusable and workspace-local. A request stores only an `AuthId`.

### Supported Auth Types

- Bearer
- Basic
- OAuth 2.0
- Custom

### Create and Edit

```sh
straumr create auth my-auth
straumr edit auth my-auth
straumr edit auth my-auth --editor
```

Interactive auth editing lets you:

- choose the auth type
- configure auth-specific fields
- fetch an OAuth token or custom auth value immediately
- toggle auto-renew behavior

Editor mode writes raw auth JSON to a temp file. Auth IDs may not be changed during edit.

### Non-Interactive Auth Creation

For scripts and agents, pass `--type` to bypass the TUI. All four auth types are supported inline.

**Bearer:**

```sh
straumr create auth prod-key -t bearer -s mytoken --json
straumr create auth prod-key -t bearer -s mytoken --prefix "Token" --json
```

**Basic:**

```sh
straumr create auth staging-basic -t basic -u user -p pass --json
```

**OAuth 2.0:**

```sh
# Client credentials (default grant type)
straumr create auth api-oauth -t oauth2 -g client-credentials \
  --token-url https://auth.example.com/token \
  --client-id myapp --client-secret s3cret --scope "read write" --json

# Shorthand type variants (no -g needed)
straumr create auth api-oauth -t oauth2-client-credentials \
  --token-url https://auth.example.com/token \
  --client-id myapp --client-secret s3cret --json

# Authorization code with PKCE
straumr create auth web-oauth -t oauth2-authorization-code \
  --token-url https://auth.example.com/token \
  --authorization-url https://auth.example.com/authorize \
  --client-id myapp --client-secret s3cret \
  --redirect-uri http://localhost:8765/callback \
  --pkce S256 --json

# Resource owner password
straumr create auth legacy-oauth -t oauth2-password \
  --token-url https://auth.example.com/token \
  --client-id myapp --client-secret s3cret \
  -u admin -p secret --json
```

**Custom:**

```sh
straumr create auth custom-login -t custom \
  --custom-url https://api.example.com/login \
  --custom-method POST \
  --custom-body '{"user":"admin","pass":"secret"}' \
  --custom-body-type json \
  --extraction-source jsonpath \
  --extraction-expression access_token \
  --apply-header-name Authorization \
  --apply-header-template "Bearer {{value}}" --json
```

**General:**

```sh
# Disable auto-renewal on any type
straumr create auth static-key -t bearer -s mytoken --no-auto-renew --json
```

### OAuth 2.0 Behavior

Straumr implements these grant types:

- Client Credentials
- Authorization Code
- Resource Owner Password

Token behavior:

- if a stored token exists and is not expired, Straumr reuses it
- if the token is expired and has a refresh token, Straumr refreshes it
- otherwise Straumr fetches a fresh token

Authorization Code flow details:

- Straumr builds the authorization URL from the saved config
- it starts a local `HttpListener` on the redirect URI host/port
- it opens the authorization URL in the system browser
- it validates the `state` value
- it exchanges the code for a token and stores it back into the auth JSON

PKCE is supported for Authorization Code.

### Custom Auth Behavior

Custom auth runs a separate HTTP request to obtain a value, then injects that value into a header on the real request.

Extraction sources:

- JSON path-like traversal
- response header
- regex

Caching behavior:

- the extracted value is cached in `CachedValue`
- if the real request returns `401 Unauthorized` and auto-renew is enabled, Straumr clears the cache, re-fetches the value, and retries once

## Secret Workflow

Secrets are global across all workspaces.

Create and manage them with:

```sh
straumr create secret api-token supersecret
straumr list secret
straumr get secret api-token
straumr edit secret api-token
straumr delete secret api-token
```

Secret edit is editor-only. There is no interactive secret TUI.

Secrets are referenced as:

```text
{{secret:api-token}}
```

You can use that placeholder in:

- request URLs
- request headers
- request params
- request bodies
- bearer/basic/oauth/custom auth fields

Missing secrets do not abort request construction immediately. Straumr leaves the placeholder unresolved and records a warning.

## Sending Requests

Basic flow:

```sh
straumr send get-users
```

Useful variants:

```sh
straumr send get-users --pretty
straumr send get-users --json
straumr send get-users --dry-run
straumr send get-users --fail
straumr send get-users --output response.txt
```

### Per-Send Header and Param Overrides

Inject headers or query params for a single send without modifying the saved request:

```sh
straumr send get-users --header "X-Request-Id: abc123" --param "debug=true"
straumr send post-order --header "Idempotency-Key: $(uuidgen)"
```

Multiple `--header` and `--param` flags are supported and applied on top of the saved request values.

### Send-Time Resolution Order

At send time Straumr:

1. Loads the saved request from the current workspace.
2. Loads the linked auth, if any.
3. Resolves `{{secret:name}}` placeholders in request fields.
4. Resolves secrets inside auth config fields.
5. Refreshes or fetches auth material if needed.
6. Builds the outgoing `HttpRequestMessage`.
7. Sends the HTTP request and records metrics.
8. Updates `LastAccessed` timestamps for the workspace, request, auth, and any secrets that were used.

### Output Modes

- default: response body only
- `--include`: response headers, then body
- `--response-headers`: response headers only
- `--response-status`: numeric status code only
- `--json`: machine-readable envelope
- `--pretty`: Spectre.Console summary/body panels
- `-v`: verbose request and response metadata
- `--dry-run`: show resolved request without sending it

`--beautify` pretty-prints JSON or XML response bodies when possible.

### Error and Exit Behavior

- transport exceptions return exit code `1` in JSON mode and `1` or `-1` in normal command paths depending on failure source
- `--fail` maps HTTP `4xx` and `5xx` responses to exit code `22`
- missing current workspace generally returns exit code `1`

## Shell Completion

Install completion into the user profile for `zsh`, `bash`, or `pwsh`:

```sh
straumr autocomplete install
straumr autocomplete install --shell zsh
straumr autocomplete install --shell bash --alias srm
```

Notes:

- completion is appended to the shell profile with begin/end marker comments
- reinstall updates the existing Straumr block in place
- hidden command `autocomplete query` powers the completion scripts
- completion prefers full command nouns; aliases are recognized for command execution, but completion data is centered on the canonical verbs and nouns
