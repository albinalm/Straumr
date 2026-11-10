# Using Straumr

Straumr is one program with two faces. Run `straumr` on its own and you get the [terminal UI](terminal-ui.md); run `straumr send users` or `straumr list workspace` and you get the CLI. They read and write the same files, so you can build a request in the UI and send it from a script five minutes later.

## The four things Straumr keeps

- A **workspace** is a folder of requests and the auth definitions they share. One per API, usually.
- A **request** is a URL, a method, headers, query parameters, a body, and optionally an auth to apply.
- An **auth** belongs to a workspace: bearer, basic, OAuth 2.0, or a custom bootstrap request.
- A **variable** belongs to a workspace, and is how the same request points at staging or production. Write `{{name}}` anywhere in a request or auth and it is substituted at send time.
- A **secret** is global, and is how a token stays out of a request file. Write `{{secret:name}}` anywhere in a request or auth and it is substituted at send time. A variable name may not start with `secret:`, so the two never collide. Custom auth is the one exception to `{{name}}`: `{{value}}` in its header template is the fetched token, and is left alone even if a variable of that name exists.

## Getting started

Tell Straumr where to keep workspaces, in `~/.straumr/settings.toml`:

```toml
[paths]
workspaces = "~/api-workspaces"
```

Then create one and make it active:

```sh
straumr create workspace my-api
straumr use workspace my-api
```

Without that setting, pass `-o <dir>` to `create workspace` and say where each one goes.

Now a request:

```sh
straumr create request users https://api.example.com/users --method GET
straumr send users --pretty
```

Every command takes `--help`, which is the authoritative list of options for the version you have installed. [The command reference](command-reference.md) is the map of the whole tree.

## Requests

`-H`/`--header` and `-P`/`--param` can be repeated. A body goes in `--data`, and `--type` says what it is: `json`, `xml`, `text`, `form`, `multipart`, or `raw`.

```sh
straumr create variable host https://api.example.com
straumr create secret api-token sk_live_...
straumr create request create-user https://api.example.com/users \
  --method POST --type json \
  --header "Authorization: Bearer {{secret:api-token}}" \
  --data '{"name":"Ada"}'
```

Before you send something for real, `--dry-run` resolves references in the URL without touching the network. Headers, parameters, and the body are shown as stored. Missing references and request-backed values that need fetching remain as placeholders and produce warnings, including in JSON output.

```sh
straumr send create-user --dry-run
straumr send create-user --header "X-Trace: test" --fail
```

`--header` and `--param` on `send` apply to that one invocation; the saved request is untouched. `--fail` turns an HTTP 4xx or 5xx into a failing exit code, `--location` follows redirects, and `--insecure` skips TLS validation — worth having for a development box with a self-signed certificate, and worth not having anywhere else.

## Authentication

`straumr create auth <name>` walks you through it. For scripts, pass `--type` and the fields that type needs; `straumr create auth --help` lists them.

```sh
straumr create auth service-token --type bearer --secret "{{secret:api-token}}"
straumr edit request users --auth service-token
```

Attach an auth to a request and it is applied on every send.

- **Bearer** puts a token in an `Authorization` header, with a prefix you can change. A 401 returns without retrying.
- **Basic** encodes a username and password. A 401 resolves any credential references again and retries the request once.
- **OAuth 2.0** handles client credentials, authorization code, and resource-owner password grants, and refreshes an expired token before sending. With auto-renew on, a 401 tries the refresh token, then a fresh grant if refresh is rejected, and retries the request once. The authorization code grant opens your browser and listens on its configured redirect URI for the callback.
- **Custom** fetches a value with a request of its own — pulling it out of the response with a JSONPath expression, a header, or a regex — and applies it as a header you define. With auto-renew on, a 401 makes it fetch a fresh value and retry once.

## Scripting

`--json` on `list`, `get`, `create`, `copy`, and `send` gives you machine-readable output; `send --json` wraps the response as `{Status, Reason, Version, DurationMs, Headers, Body}`. Failures write `{"Contents":{"Message":"..."}}` to stderr and return a nonzero exit code.

Prefer `--workspace <name-or-id>` over `use workspace` in a script: it targets a workspace for one command instead of changing state that another shell is also reading. And prefer IDs over names once you have discovered them — a rename does not move an ID.

`straumr --agent-help` prints a short operating guide meant for AI agents and automation.

## Sharing a workspace

```sh
straumr export workspace my-api ./exports
straumr import workspace ./exports/my-api.straumrpak
```

A `.straumrpak` is a zip of the workspace folder. It carries requests, auths, and variables. It does not carry secrets — those are global and stay on your machine, which is the point of keeping them separate.

## Editing the files directly

Every resource is JSONC on disk: JSON that tolerates `//` and `/* */` comments and trailing commas. Straumr reads them, writes them, and puts your comments back where it found them.

Use `--editor` on `create` and `edit` to open the file in `$EDITOR` instead of answering prompts, or `Ctrl+E` in the terminal UI. Both need `$EDITOR` set. Keep the `Id` field as it is — that is how Straumr and its workspace manifest find the file again.

Secrets are stored in plain text under `~/.straumr/secrets`. Straumr does not encrypt them; give that directory the permissions you would give an SSH key.

## Tab completion

```sh
straumr autocomplete install --shell zsh
```

bash, zsh, and PowerShell are supported. `--alias <name>` adds completion for a shell alias you use for `straumr`.

## Values from other requests

A request-backed variable gets its value by sending another saved request. That source request can use its own auth and variables. The resulting value works in URLs, query parameters, headers, bodies, and auth fields, just like a plain variable.

```sh
straumr create variable route-session-id --from-request route-v2-session --extract jsonpath:session.sessionId --ttl 900
straumr edit request route-query --param 'sessionId={{route-session-id}}'
straumr send route-query
straumr refresh variable route-session-id
```

Creating the variable does not send its source request. The first send that needs the value fetches it; later sends reuse the cache. `--ttl` is a positive number of seconds, measured from the successful fetch. Without a TTL, the value lasts until you explicitly refresh it or change its source configuration or inputs. Changes to the source request, its auth configuration, referenced variables, or referenced secrets invalidate the cache. A copy keeps the source configuration and drops the cached value.

References use IDs internally and belong to the same workspace. When copying across workspaces, configure a request-backed variable with a source request in the destination first, then choose that existing variable when carrying request dependencies. Source requests are not automatically copied. Refreshes within a running Straumr process share a workspace lock; separate CLI processes may fetch independently.

In the TUI variable editor, choose **Source request**, the extraction source, an expression, and optionally a TTL. **Fetch** (`s` by default, action `Variable.Fetch`) or `:fetch <name or ID>` forces a refresh. The usual `:refresh` reloads the screen without fetching values. Request and auth sends use the same resolution behavior as the CLI.

Extraction rules are `jsonpath:<path>`, `header:<name>`, and `regex:<pattern>`. JSON paths are dotted property names with numeric array segments, such as `sessions.0.sessionId`. Header names ignore case. Regex uses the first capture group, or the whole match when no group is present, and has a two-second timeout. Values must be non-empty strings, numbers, or booleans; missing values, nulls, arrays, and objects fail without replacing a valid cache. Circular dependencies report the request/variable chain.

Dry-run never sends a source request, refreshes a cache, or performs a capture. It can use an existing valid cached value in the URL; otherwise it keeps the placeholder and explains why. Fetch and capture notices appear with response warnings, including the `Warnings` array in `--json` output, without including extracted values.

A 401 or 403 does not automatically refresh a request-backed variable or replay the dependent request. Refresh explicitly when a session is rejected. Existing attached-auth renewal behavior still applies.

## Capturing response values

Use capture when you deliberately send a request and want to save a value for later requests:

```sh
straumr create variable moderation-reference pending
straumr edit request intelligence-v2-moderations-create --capture moderation-reference=jsonpath:reference
straumr send intelligence-v2-moderations-create
straumr edit request intelligence-v2-moderations-status --param 'reference={{moderation-reference}}'
```

`create request <name> <url>` also accepts repeatable `--capture` options. Each rule uses `<variable-name-or-ID>=<extraction-rule>`. The target must already exist as a plain variable in the same workspace. Editing with `--capture` replaces the rule for that target; `--clear-captures` removes all rules before any new ones are added.

Captures run only after a successful explicit send (HTTP 2xx). Sending a request automatically as a variable source does not run its captures. All targets and extraction results are checked before writing values. A write failure attempts to restore earlier writes and reports any restoration failure. Capture failure means the HTTP request already completed: check its effects before sending it again.

The TUI request editor has a **Response captures** tab, edited with your `$EDITOR`. The request JSON editor exposes the same `Captures` array:

```json
[
  {
    "VariableId": "<target-variable-guid>",
    "ExtractionSource": 0,
    "Expression": "reference"
  }
]
```

`ExtractionSource` is `0` for JSON path, `1` for response header, and `2` for regex. Request copies keep capture rules; copying with dependencies remaps their target IDs.
