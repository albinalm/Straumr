# Straumr agent guide

Straumr is an HTTP client that keeps requests on disk. This guide is the operating manual for an agent or script
driving it without a human at the keyboard. `straumr <command> --help` is authoritative for the installed release.

## What Straumr stores

- A **workspace** is a folder of requests, auths, and variables. Everything except secrets is scoped to one.
- A **request** is a method, URL, headers, query params, a body, and optionally an auth to apply on send.
- An **auth** is bearer, basic, OAuth 2.0, or a custom bootstrap request. It belongs to a workspace.
- A **variable** belongs to a workspace. `{{name}}` anywhere in a request or auth is substituted at send time.
- A **secret** is global, not part of any workspace. `{{secret:name}}` is substituted at send time.

Every object has a name and a GUID. Names are unique within a workspace and may be renamed; IDs never change. Every
command that takes `<Name or ID>` tries the ID first. Discover once, then use IDs. Request names cannot contain `"`.

## Do not block

Running `straumr` with no arguments opens the terminal UI and never returns. Always pass a command. An unrecognized
first argument still goes to the CLI, so a typo fails instead of hanging, but `straumr cli <args>` forces CLI parsing
if you want the guarantee.

These forms wait for a human. Use the non-interactive form on the right instead.

| Blocks                        | Why                       | Non-interactive form                                |
|-------------------------------|---------------------------|-----------------------------------------------------|
| `create request <name>`       | prompt menu               | `create request <name> <url>` — URL makes it inline |
| `create request ... --editor` | launches `$EDITOR`        | pass `--method`/`--header`/`--data` inline          |
| `create auth <name>`          | prompt menu               | `create auth <name> --type <type> ...`              |
| `create variable <name>`      | prompts for the value     | `create variable <name> <value>`                    |
| `create secret <name>`        | prompts for the value     | `create secret <name> <value>`                      |
| `edit request <id>`           | prompt menu               | `edit request <id>` with inline flags (below)       |
| `edit request <id> --json`    | implies `--editor`        | add at least one inline flag                        |
| `edit workspace\|auth\|variable\|secret` | `$EDITOR` only | see "Editing without an editor"                     |

Editor-backed commands also fail outright when `$EDITOR` is unset, and they return the editor's exit code when it is
nonzero. Do not set `$EDITOR` to something that blocks.

## Discovery

```sh
straumr list workspace --json
straumr list request --json --workspace <workspace>
straumr list auth --json --workspace <workspace>
straumr list variable --json --workspace <workspace>
straumr list secret --json
straumr get request <id> --json --workspace <workspace>
```

`--filter <text>` matches a case-insensitive name substring or an ID prefix. List entries carry a `Status` of `Valid`,
`Corrupt`, or `Missing`; a broken entry still lists its ID, with `N/A` in the fields that could not be read.

## Targeting a workspace

`--workspace <name-or-id>` (`-w`) targets one command at a workspace already registered with Straumr, without touching
global state. It is accepted on every `request`, `auth`, and `variable` subcommand.

Workspace subcommands take the workspace as their argument, and secrets are global, so neither accepts `-w`.

Do not call `use workspace` from a script: it rewrites the active workspace in `~/.straumr` and every other shell and
agent on the machine sees the change. Pass `-w` on each command instead. Without `-w`, commands fall back to the active
workspace and fail with exit `1` when there is none.

## Creating without prompts

```sh
straumr create workspace <name> -o <dir> --json
straumr create request <name> <url> -m POST -t json -d '{"a":1}' -H "X-Key: {{secret:api-key}}" -w <ws> --json
straumr create auth <name> --type bearer --secret "{{secret:api-token}}" -w <ws> --json
straumr create variable <name> <value> -w <ws>
straumr create secret <name> <value>
```

`create workspace` needs `-o` or `paths.workspaces` in `~/.straumr/settings.toml`. `create request` requires both the
name and the URL — a URL with no name is refused. `create auth` requires `--type`; `straumr create auth --help` lists
the fields each type needs. Body types are `json`, `xml`, `text`, `form`, `multipart`, `raw` (`none` on edit).
`-H`/`--header` and `-P`/`--param` repeat.

## Editing without an editor

`edit request` is the only edit with an inline mode. Any of `-n/--name`, `-u/--url`, `-m/--method`, `-H/--header`,
`-P/--param`, `-d/--data`, `-t/--type`, `-a/--auth` switches it out of interactive mode; `-a none` detaches the auth.

```sh
straumr edit request <id> -u https://api.example.com/v2/users -H "Accept: application/json" -w <ws> --json
```

Workspaces, auths, variables, and secrets have no inline edit. To change a variable or secret value unattended, delete
and recreate it — the new entry gets a new ID, so update anything holding the old one. References are by name, so
`{{name}}` keeps resolving.

`delete` never asks for confirmation. `copy <id> <new-name>` duplicates an entry under a new ID.

## Renaming

`edit request <id> -n <new-name>` is the only rename that runs unattended. Everything else is renamed by copying to
the new name, checking the copy, and deleting the original.

Rename a workspace like this:

```sh
straumr list workspace --json                                   # note the old workspace's Id and Path
straumr copy workspace <old-id> <new-name> -o <dir> --json      # -o is the folder the workspace folder goes in
straumr list request --json -w <new-id>                         # same entries, every Status Valid
straumr list auth --json -w <new-id>
straumr list variable --json -w <new-id>
straumr send <request-id> --dry-run -w <new-id>                 # references still resolve
straumr delete workspace <old-id>
straumr use workspace <new-id>                                  # only if the old one was active
```

A workspace copy keeps the request, auth, and variable IDs — only the workspace itself gets a new ID. Scripts holding
request IDs keep working, request-to-auth links survive, and stored OAuth tokens come across, so the copy needs no
re-auth. The copy is registered but not made active.

Points to get right:

- Without `-o`, the copy lands under `paths.workspaces`, not beside the original. `Path` from `list workspace --json`
  is `<root>/<folder>/<workspace-id>.straumr`; pass its grandparent as `-o` to keep the workspace where it was.
- Check the copy **before** deleting. `delete workspace` removes the folder and everything in it from disk,
  recursively and without confirmation. For a rollback, `export workspace <old-id> <dir>` first and restore the
  `.straumrpak` with `import workspace <path>`.
- The new name must not collide with an existing workspace name (compared case-insensitively). The copy fails first,
  so nothing is destroyed.
- Deleting the active workspace leaves no active workspace, and later commands without `-w` fail with exit `1`.
- Do not rename by editing the manifest: `edit workspace` is `$EDITOR`-only, and changing `Name` there leaves the
  folder and the registered path under the old name.

Auths, variables, and secrets copy under a **new ID**. Requests point at an auth by ID, so re-point every request with
`edit request <id> -a <new-auth>` before deleting the old auth. Variables and secrets are referenced by name, so
update every `{{name}}` and `{{secret:name}}` that used the old one.

## Sending

```sh
straumr send <id> --dry-run -w <ws>
straumr send <id> --json -w <ws>
straumr send <id> --json --fail -w <ws>
straumr send <id> --response-status -w <ws>
```

Prefer `--json`: the response body lands on stdout as a single envelope and nothing else is mixed in.

Without `--json`, the body goes to stdout raw — but so do resolution warnings, appended after it. Either use `--json`,
or write the body to a file with `-o <path>` and read it back. `-o` is ignored when `--pretty` or `--json` is set, and
`--silent` suppresses warnings and error text but *not* the body. `-v` writes request and response metadata to stderr.
`--response-status` and `--response-headers` print only that and skip the body.

`--fail` returns exit `22` for an HTTP status of 400 or above. `-L/--location` follows redirects. `-k/--insecure`
skips TLS validation. `-H` and `-P` on `send` apply to that invocation only and do not touch the saved request.

## Placeholder resolution

`{{name}}` resolves against workspace variables, `{{secret:name}}` against global secrets, in the URL, headers, params,
body, and auth config. A name that does not resolve is **left in place literally** and sent as written, with a warning.
A variable name may not start with `secret:`. In a custom auth's apply-header template, `{{value}}` is the fetched
token and is never treated as a variable.

`--dry-run` does not make a network call and does not fetch tokens. It resolves the URL only: headers, params, and the
body are printed as stored, with placeholders intact. `--dry-run --json` also drops the warning list, so run a plain
`send <id> --dry-run` and read stderr-free stdout when you need to confirm that every reference resolves.

## Auth on send

An attached auth is applied on every send. OAuth 2.0 with auto-renew refreshes an expired token before sending; custom
auth fetches its value when nothing is cached. On a 401, basic, OAuth 2.0, and custom auth renew once and retry the
request; bearer does not. Renewed tokens are written back into the auth file, so a send can modify workspace state.

The OAuth 2.0 authorization code grant opens a browser and waits on a local callback listener. It cannot complete
unattended — use client credentials, password, a custom auth request, or a pre-fetched token in a secret.

## JSON output and exit codes

`--json` (`-j`) is supported on `list`, `get`, `create`, `copy`, `edit`, `delete`, `send`, `import`, `export`, and
`config workspace-path`. Output is pretty-printed, PascalCase JSON on stdout — one object or array per invocation, not
a stream. Timestamps are local time, `yyyy-MM-ddTHH:mm:ss`, with no offset.

```text
send            {Status, Reason, Version, DurationMs, Headers, Body}
send --dry-run  {Method, Uri, Auth, Headers, Params, BodyType, Body}
get request     {Id, Name, Method, Uri, BodyType, Headers, Params, Body, AuthId, LastAccessed, Modified}
list request    [{Id, Name, Method, Uri, Status, LastAccessed}]
list workspace  [{Id, Name, Path, IsCurrent, Requests, Status, LastAccessed}]
list auth       [{Id, Name, Type}]
list secret     [{Id, Name, Status}]
list variable   [{Id, Name, Status}]
create/copy     workspace {Id, Name, Path} · request {Id, Name, Method, Uri} · auth {Id, Name, Type}
export          {Path}
```

`send --json` inlines `Body` as JSON when the response content type contains `json`, and otherwise as a JSON string.
`get workspace|auth|secret --json` returns the persisted model rather than a DTO; an auth's `Config` carries an
`AuthType` discriminator (`Bearer`, `Basic`, `OAuth2`, `Custom`).

Failures write an envelope to **stderr** and return nonzero:

```json
{ "Contents": { "Message": "..." } }
```

Read `Contents.Message`. Two exceptions worth handling: `send --json` prints the envelope for a transport failure on
**stdout** and exits `1`, and `create`/`delete` for variables and secrets have no `--json` at all — they print colored
text on stdout. For those, trust the exit code.

| Code | Meaning                                                                     |
|------|-----------------------------------------------------------------------------|
| `0`  | success                                                                     |
| `1`  | expected failure: no workspace, entry not found, invalid input, transport failure |
| `22` | `send --fail` and the response status was 400 or above                      |
| `-1` | unhandled failure, including command-line parse errors (`255` in POSIX shells) |

## Terminal behavior

`--no-color` disables ANSI colour and terminal links; pass it whenever you capture human-readable output. `--version`
prints the version and exits, `--agent-help` prints this guide, and both are handled before command parsing.

## Handling secrets

`get secret <id> --json` and the plain `get secret` panel both print the secret value. Secrets are stored unencrypted
under `~/.straumr/secrets`. Do not echo them into logs, transcripts, or commit them; reference them as
`{{secret:name}}` and let Straumr substitute at send time. An exported `.straumrpak` carries requests, auths, and
variables, never secrets.
