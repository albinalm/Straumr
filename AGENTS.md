# AGENTS.md

## Working agreement

This section gates everything below it. Satisfy it before writing code.

- **All new development happens on a feature branch.** Never commit to `main`, and never merge into it. Work reaches `main` through a pull request, reviewed and merged by a person — an agent opens the PR and stops there.
- **Every feature branch has a linked GitHub issue.** Look for an existing issue first. If none covers the work, you may create one and branch from it when the user asks for the work — confirm the title and body with them first, then keep going. Never invent an issue number, and never branch without one.
- **Branch name: `<initials>/<issue-id>-<feature-description>`** — the author's initials, lowercase, then the issue number, then a short hyphenated description.

  ```text
  aa/42-fix-theme-export
  ```

  Git refs cannot contain a colon or a space, so the separator between the issue id and the description is a hyphen.
- Reference the issue as `#<id>` in the pull request body so GitHub links the two, and let the user merge it.

## Repository rules

- Target .NET 10. Build the solution with `dotnet build src/Straumr.sln -p:IncludeTui=true`, and verify `-p:IncludeTui=false` still builds when you touch host composition, CLI registration, or trimming roots.
- There are no test projects. Verification is a build of both variants plus a manual run; ask for a manual check instead of building a probe harness.
- The host is Native AOT/single-file. Add persisted models to `StraumrJsonContext`; add CLI JSON DTOs to `CliJsonContext`; preserve command types when changing CLI registration/trimming roots (`Straumr.Console.Cli/CliRoots.xml`).
- Do not use user storage for verification runs. Settings and state live under the user profile in `.straumr`; use an isolated profile when exercising the app manually.

## Code style

`src/.editorconfig` is authoritative; ReSharper/Rider formats from it. What it does not encode:

- **No XML documentation.** No `<summary>`, no `<remarks>`, no `<param>`. A one- or two-line `//` comment is allowed only when it states something the code cannot: a terminal quirk, a framework constraint, an ordering requirement. Across the whole repository there are seven, plus a few `// ReSharper disable` pragmas. If the comment restates the code, delete the code's ambiguity instead.
- **File-scoped namespaces**, one type per file, folder path matches namespace. Cross-project usings that every file needs go in that project's `GlobalUsings.cs`.
- **Explicit types**, except where the type is already written on the right-hand side — `var x = new Thing()`, `var s = provider.GetRequiredService<T>()`. `foreach`, `await`, LINQ results and method returns all name their type.
- **Expression bodies** for methods, properties and local functions whenever the body is a single expression; `switch` expressions over `switch` statements.
- Allman braces. Blank lines are not used to separate fields, properties or members — the file stays dense.
- `sealed` by default on new classes; `record` for value-shaped models; primary constructors for dependency injection.
- `string.Empty`, never `""`. Line length 182.
- Types the framework instantiates by reflection (Spectre command and settings classes, TUI models bound by name) carry `[UsedImplicitly]` so the analyzer does not strip them.
- Nullable is enabled everywhere. Model a missing value as `null` or a `TryX` pattern rather than throwing for control flow; `StraumrException` with a `StraumrError` reason is the domain failure channel.

## Architecture boundaries

- `Straumr.Console.App` composes integrations and dispatches arguments. CLI and TUI are peer integrations; bare `straumr` opens the TUI, while registered command nouns route to the CLI. Keep `IncludeTui=false` working.
- Put persistence, domain behavior, HTTP dispatch, auth, and secret resolution in `Straumr.Core`. UI layers call Core services; they do not duplicate business logic or invoke one another's commands.
- `Straumr.Console.Cli` owns Spectre command parsing, terminal presentation, and editor/prompt orchestration. `Straumr.Console.Tui` owns retained UI presentation and TUI interaction. `Straumr.Console.Shared` holds integration contracts and genuinely cross-frontend editing abstractions.
- Persist through `IStraumrFileService` and the entity services. Resource files are JSONC and comments must survive rewrites. `ReadStraumrModelAsync` updates `LastAccessed`; use `PeekStraumrModelAsync` for non-mutating reads.
- Workspace membership sets distinguish request and auth files that share a directory and extension. Secrets are global. Preserve IDs on editor-backed changes.

## TUI rules

- Use the pinned `XenoAtom.Terminal.UI` 3.9.0 API, not upstream examples without checking the installed version.
- Keep I/O out of render, measure, arrange, and per-frame paths. Load when entering a screen; refresh after an operation or explicit refresh.
- Reuse existing components in `Screens/Components` and state/binding patterns before introducing custom layout, scrolling, focus, or clipping behavior.
- Route colour and control styles through `StraumrStyleService`/the theme models. Do not add ad-hoc colours or assume colours are RGB; themes may use terminal defaults or palette slots.
- Text and body editing hands off to the user's `$EDITOR`; that handoff is the product's premise, so do not build an in-TUI text editor for it.
- Bind keys through `StraumrKeybinds` action ids, never to literal keys. A new action needs a default in `StraumrKeybinds.Defaults` and a row in `docs/keybinds.md`, which `settings.toml` points users to.
- TUI theme changes rebuild the scoped app tree. Keep TUI registrations independent of CLI registration so the TUI can run on its own.

## Safe changes

- Keep command JSON output machine-readable and source-generated; use IDs and explicit workspace context in automation-facing flows.
- Keep OAuth/custom-auth and secret resolution in Core. Never log secret values.
- If the app can repair what a change breaks, offer the repair in a dialog rather than printing a caution.
- Consumer docs live in `docs/` and describe observable product behavior; durable developer explanation goes in `docs/developer/`. `--agent-help` prints `src/Straumr.Console.Cli/Resources/AutomationHelp.md`; keep it in step with automation-facing flags. Do not add agent guides outside this file.
