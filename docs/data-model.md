# Data Model

This document explains how Straumr persists state on disk and how that state is interpreted at runtime.

## Storage Layout

Straumr has two storage roots:

- global state in `~/.straumr`
- workspace content under the configured workspace root

Example layout:

```text
~/.straumr/
  options.json
  secrets/
    <secret-id>/
      <secret-id>.secret.json

<workspace-root>/
  demo/
    <workspace-id>.straumr
    <request-or-auth-id>.json
    <request-or-auth-id>.json
```

## Global Options File

`~/.straumr/options.json` stores:

- `DefaultWorkspacePath`
- `DefaultSecretPath`
- `Workspaces`: registry of workspace IDs and manifest paths
- `Secrets`: registry of secret IDs and secret file paths
- `CurrentWorkspace`: the active workspace entry

Example:

```json
{
  "DefaultWorkspacePath": "/tmp/straumr-docs-ws",
  "DefaultSecretPath": "/tmp/straumr-docs-home/.straumr/secrets",
  "Workspaces": [
    {
      "Id": "c55da52f-a4d7-41b2-a2e2-487a006318d7",
      "Path": "/tmp/straumr-docs-ws/demo/c55da52f-a4d7-41b2-a2e2-487a006318d7.straumr"
    }
  ],
  "Secrets": [
    {
      "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
      "Path": "/tmp/straumr-docs-home/.straumr/secrets/92b14e4b-3248-4708-ab9b-edd171e748a8/92b14e4b-3248-4708-ab9b-edd171e748a8.secret.json"
    }
  ],
  "CurrentWorkspace": {
    "Id": "c55da52f-a4d7-41b2-a2e2-487a006318d7",
    "Path": "/tmp/straumr-docs-ws/demo/c55da52f-a4d7-41b2-a2e2-487a006318d7.straumr"
  }
}
```

## Workspace Manifest

Each workspace is persisted as a `.straumr` JSON file.

Example:

```json
{
  "Secrets": [],
  "Requests": [
    "865c9c5d-ef63-49ec-8a07-c73d84f9cd86"
  ],
  "Auths": [],
  "Id": "c55da52f-a4d7-41b2-a2e2-487a006318d7",
  "Name": "demo",
  "Modified": "2026-04-05T13:10:29.9368272+00:00",
  "LastAccessed": "2026-04-05T13:10:20.8063373+00:00"
}
```

Notes:

- `Requests` and `Auths` are the authoritative membership lists for files in the workspace directory.
- `Secrets` exists on the model but is not populated by the current services. Secrets are tracked globally in options instead.

## Request Files

Requests are stored as `<id>.json` in the active workspace directory.

Example:

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
    "Json": "{\u0022hello\u0022:\u0022world\u0022}"
  },
  "AuthId": null,
  "Id": "865c9c5d-ef63-49ec-8a07-c73d84f9cd86",
  "Name": "demo-request",
  "Modified": "2026-04-05T13:10:29.9314163+00:00",
  "LastAccessed": "2026-04-05T13:10:29.914483+00:00"
}
```

Important serialization details:

- `Method` is serialized as an object with a `Method` property, not as a plain string.
- `BodyType` is serialized as the enum numeric value.
- `Bodies` uses enum names such as `Json` as object keys.

## Secret Files

Secrets are stored globally as `<secret-id>.secret.json`.

Example:

```json
{
  "Value": "secret123",
  "Id": "92b14e4b-3248-4708-ab9b-edd171e748a8",
  "Name": "api-token",
  "Modified": "2026-04-05T13:10:24.668039+00:00",
  "LastAccessed": "2026-04-05T13:10:24.6632504+00:00"
}
```

## Auth Files

Auths are stored as `<id>.json` in the workspace directory, alongside request files.

The workspace manifest determines whether a given GUID-backed JSON file is interpreted as a request or an auth.

Auth config is polymorphic and uses an `authType` discriminator. The concrete config classes are:

- `BearerAuthConfig`
- `BasicAuthConfig`
- `OAuth2Config`
- `CustomAuthConfig`

## Shared Base Fields

Workspace, request, auth, and secret models all inherit:

- `Id`
- `Name`
- `Modified`
- `LastAccessed`

`Modified` is updated when `WriteStraumrModel` is called with `updateModify=true`, which is the normal create/update path.

`LastAccessed` is updated when:

- a model is read through `ReadStraumrModel`
- `StampAccessAsync` is called explicitly

## Lookup Rules

### Workspaces

Resolved by:

1. exact GUID match against the options registry
2. case-insensitive workspace-name match by peeking workspace manifests

### Requests and Auths

Resolved within the current workspace by:

1. exact GUID match against the workspace membership set
2. name match by peeking each member file

Request/auth name matching is case-sensitive in the service-layer lookup path. Some CLI command wrappers compare names case-insensitively while locating IDs for `get`.

### Secrets

Resolved globally by:

1. exact GUID match against the options registry
2. case-insensitive name match by reading secret files

## Secret Placeholder Resolution

Pattern:

```text
{{secret:<name>}}
```

Resolution rules:

- placeholders are resolved in request URL, headers, params, bodies, and auth config fields
- resolved secret values are cached per send operation to avoid repeated file reads
- unresolved placeholders generate warnings instead of hard failures
- successfully resolved secrets have their `LastAccessed` timestamp updated through `secretService.GetAsync`

## HTTP Request Construction

The saved request model is transformed into `HttpRequestMessage` as follows:

- query params are merged into the request URI using `UriBuilder`
- bearer/basic/oauth/custom auth maps onto outgoing headers
- request headers are added first to message headers, then to content headers if needed
- body content is generated from `BodyType`

Body handling details:

- `Json` -> `application/json`
- `Xml` -> `application/xml`
- `Text` -> `text/plain`
- `FormUrlEncoded` -> parsed key/value pairs into `FormUrlEncodedContent`
- `MultipartForm` -> parsed key/value pairs into `MultipartFormDataContent`, with `@path` meaning file upload
- `Raw` -> `StringContent` with no `Content-Type` header

## Import and Export Format

Straumr exports workspaces as `.straumrpak` zip archives.

Archive contents:

- `.pak`: two-line metadata file containing workspace ID then workspace name
- one workspace directory containing the `.straumr` manifest and any `.json` member files

Import validation rules:

- archive must contain `.pak`
- archive must contain exactly one directory
- destination workspace folder name is derived from the workspace name in `.pak`

## Autocomplete Data Sources

The hidden autocomplete query command derives suggestions from live stored state:

- top-level verbs from static lists
- nouns from static verb-to-noun maps
- workspace names and IDs from the options registry
- request/auth names and IDs from the current workspace
- secret names and IDs from the global secrets registry

Completion only works for request and auth identifiers when a current workspace is set.
