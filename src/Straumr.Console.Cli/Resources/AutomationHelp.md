# Straumr automation guide

Use `--json` for machine-readable output and `--workspace <name-or-id>` for request and auth operations. Do not use `use workspace` in a script because it changes persistent state.

```sh
straumr list workspace --json
straumr list request --json --workspace <workspace>
straumr send <request-id> --dry-run --json --workspace <workspace>
straumr send <request-id> --json --workspace <workspace>
```

After discovery, prefer IDs over names. `--filter` on list commands matches a case-insensitive name substring or ID prefix. On failure, JSON-capable commands write an error envelope to stderr and return a nonzero exit code.

Requests may reference global secrets as `{{secret:name}}` and workspace variables as `{{name}}`. A variable name never starts with `secret:`. Dry runs resolve request/auth data without making a network call; unresolved references are reported as warnings. `send --fail` maps HTTP 4xx/5xx responses to a failing exit code.

Run `straumr <command> --help` for the complete option set of the installed release.
