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

Before you send something for real, `--dry-run` shows you the request with its variables, secrets, and auth resolved, without touching the network. A reference that does not exist is left as written and reported as a warning, so you can see exactly what would have gone out.

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
