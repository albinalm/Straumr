# Development

Requires the .NET 10 SDK. There are no test projects — verification is a build of both variants plus a manual run.

```sh
dotnet build src/Straumr.sln -p:IncludeTui=true
dotnet build src/Straumr.sln -p:IncludeTui=false
dotnet run --project src/Straumr.Console.App/Straumr.Console.App.csproj -- --help
```

`IncludeTui` defaults to `true`. Build the CLI-only variant whenever you touch host composition, CLI registration, or trimming roots; it has its own output tree, so alternating between the two is exact without a clean.

A manual run reads and writes the real `~/.straumr` directory. Point `HOME` (or `USERPROFILE`) at a scratch directory before exercising persistence, secrets, or workspace lifecycle, so a test run cannot touch your own workspaces.

Editor-backed flows need `$EDITOR`; without it those commands refuse rather than guess.

## Conventions

`src/.editorconfig` drives formatting. The conventions it cannot express — no XML documentation, comments only for what the code cannot say, explicit types except where the right-hand side names them — are in the root [AGENTS.md](../../AGENTS.md), which applies to humans and agents alike.

## Release

`.github/workflows/dev.yml` builds on every push to `main` and on demand, versioned `<date>.<run>-dev`. `.github/workflows/release.yml` is manually triggered and versioned `<date>.<run>`. It builds the `win-x64` and `linux-x64` artifacts, writes `sha256sums.txt` and a minisign signature per file, and then pushes the release out to its package channels: winget, the AUR, COPR, and the APT repository.

Keep the source-generated serialization contexts and the CLI trimming descriptor aligned with code changes; both are release-breaking rather than build-breaking, so an AOT publish is the thing that catches a mistake, not `dotnet build`.
