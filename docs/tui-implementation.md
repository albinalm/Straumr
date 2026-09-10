# TUI Implementation Guide

This is the living specification and progress tracker for the Straumr TUI rewrite.
Update it whenever a milestone is completed, a design decision changes, or a
framework constraint is discovered.

## Current Status

- Phase: implementation
- Active screen: Workspaces
- Implementation: W1 complete
- Next checkpoint: begin W2 after reviewing the shared application shell checkpoint
- Last updated: 2026-09-10

## Goals

- Implement the approved TUI layouts one screen at a time.
- Start with Workspaces before Requests, Auths, and Secrets.
- Use XenoAtom.Terminal.UI as a retained, reactive UI framework.
- Keep TUI presentation independent from CLI presentation.
- Reuse Core services and models instead of duplicating business logic.
- Preserve the CLI-only build and its smaller dependency footprint.
- Keep code straightforward enough that structure and naming explain behavior.

## Non-Goals

- Do not build an in-terminal JSON editor. Structured editing continues through
  the configured external editor.
- Do not move terminal concerns into Core.
- Do not call CLI commands from the TUI.
- Do not recreate framework controls through manual measurement, rendering,
  scrolling, or focus code when a built-in control supports the behavior.
- Do not implement screens beyond the currently approved milestone.

## Sources of Truth

Use these in this order:

1. The package pinned by Straumr:
   `XenoAtom.Terminal.UI 3.9.0` and its installed XML documentation.
2. The [XenoAtom.Terminal.UI repository](https://github.com/XenoAtom/XenoAtom.Terminal.UI/tree/main/).
3. The [FullscreenDemo](https://github.com/XenoAtom/XenoAtom.Terminal.UI/tree/main/samples/FullscreenDemo)
   for composition, bindings, commands, focus, overlays, and fullscreen hosting.
4. The approved mockups under [`docs/design`](./design/).
5. Existing Straumr CLI and Core behavior.

Upstream `main` can be newer than the pinned NuGet package. Copy architectural
patterns from upstream, but verify concrete APIs against version `3.9.0` before
using them.

## Design Contract

The mockups define information hierarchy and intended interaction, not a mandate
to reproduce browser CSS or pixel geometry. Prefer native terminal controls and
theme styles that produce the same structure.

Shared screen shell:

- Header left: `{straumr} {screen}`.
- Header right: `active workspace {name}` in a subdued success color.
- Main content: resource list on the left and selected-resource details on the
  right.
- Footer: context-aware shortcuts.
- `:` opens the command prompt.
- `Escape`, submission, or loss of focus closes and clears the command prompt.
- Vim navigation is supported where it supplements native control navigation.

Approved references:

- [Requests mockup](./design/straumr-requests-layout.html)
- [Workspaces mockup](./design/straumr-workspaces-layout.html)
- [Auths mockup](./design/straumr-auths-layout.html)
- [Secrets mockup](./design/straumr-secrets-layout.html)
- [Combined secondary screens](./design/straumr-other-screens.html) for overview
  only; the individual files above are authoritative

## Framework Rules

- Compose the application shell with `Header`, `DockLayout`, layout containers,
  and a footer or `CommandBar` where their behavior fits.
- Use `State<T>`, bindings, and computed visuals for changing UI state.
- Use `ListBox<T>` with a `DataTemplate<T>` for resource lists.
- Bind list selection to state and derive the detail pane from that selection.
- Use framework commands and gestures for actions exposed in the command bar.
- Keep key handling local to the control that owns the interaction.
- Let built-in controls own focus, scrolling, pointer input, clipping, selection
  styling, and invalidation.
- Perform I/O outside render, measure, arrange, and per-frame update paths.
- Load once on entry and refresh only after an operation or explicit refresh.
- Show loading, empty, and error states as visuals driven by state.
- Use theme colors and control styles. Avoid hard-coded terminal palettes except
  for stable semantic mappings such as HTTP methods.
- Introduce a custom `Visual` only after confirming that composition, templating,
  or styling cannot express the requirement.

The existing `RequestList` manually implements layout, scrolling, selection,
pointer input, and rendering. Treat it as prototype code, not the pattern for new
screens.

## Straumr Code Rules

- Match the feature-oriented structure used by `Straumr.Console.Cli`.
- Inject Core interfaces instead of resolving services throughout the visual
  tree.
- Keep integration setup in `Integration`.
- Keep navigation and shared application state in `Infrastructure`.
- Keep each screen and its screen-specific presentation models together.
- Put genuinely shared visuals and formatting helpers under `Visuals/Shared`.
- Use TUI presentation models when a Core model does not directly represent the
  information shown on screen.
- Do not add comments unless they explain an implicit behavior or a constraint a
  future developer could reasonably miss.
- Prefer short methods that construct one coherent region or perform one
  operation.
- Do not add interfaces or callback abstractions until there is more than one
  concrete consumer or a real test boundary.

Target structure:

```text
Straumr.Console.Tui/
  Integration/
    TuiConsoleIntegration.cs
  Infrastructure/
    StraumrTuiApp.cs
    TuiScreen.cs
  Screens/
    Workspace/
      WorkspaceScreen.cs
      WorkspaceScreenItem.cs
  Visuals/
    Shared/
      StraumrHeader.cs
      StraumrCommandPrompt.cs
  Formatting/
    HttpMethodFormatting.cs
```

This is a starting boundary, not a requirement to create empty abstractions.
Add a file only when it owns meaningful behavior.

## Runtime Boundaries

`TuiConsoleIntegration` should remain a thin host:

- register the TUI's dependencies
- construct the application root
- run it with `Terminal.RunAsync`
- translate application exit into the process exit code

The TUI integration must register the Core services it requires and must not
depend on CLI registration as an accidental side effect.

The application root should own:

- current screen
- active workspace display state
- command prompt visibility and text
- top-level commands and exit state
- focus restoration when screens or overlays change

A screen should own:

- its loading, loaded, empty, and error state
- its selected index or selected item
- data loading and refresh after screen-specific operations
- visuals and commands that belong only to that screen

## Workspaces Screen Specification

### Layout

Header:

- left: `{straumr} workspaces`
- right: `active workspace demo`, using the actual active workspace name

Left pane:

- title `Workspaces` and total count
- filter affordance
- one multiline item per workspace
- workspace name
- request and auth counts
- workspace directory
- selection styling supplied by the list control

Detail header:

- workspace name
- last-accessed timestamp
- shortened workspace ID aligned right

Workspace detail group:

- path
- request count
- auth count
- modified timestamp

Requests group:

- requests from the selected workspace
- ordered by `LastAccessed` descending
- HTTP method with the shared semantic color mapping
- request name
- relative last-used timestamp
- no auth entries or generic `Contents` label

Do not display:

- workspace validity or active-status labels
- a Secrets group
- invented environment or scope values

### Initial Data Flow

1. Load options once before the screen becomes interactive.
2. Load workspaces through `IStraumrWorkspaceService.ListAsync` without updating
   their access timestamps.
3. Join each loaded workspace to its `StraumrWorkspaceEntry` for its configured
   path and current-workspace identity.
4. Populate one TUI presentation item per loaded workspace.
5. Select the current workspace when present; otherwise select the first item.
6. Load requests for the selected workspace through
   `IStraumrRequestService.ListAsync`.
7. Sort the selected workspace's requests by `LastAccessed` descending.
8. Update the details reactively when selection changes.

Selection must not mutate `LastAccessed`. Activation and other explicit actions
may persist changes through the appropriate Core service.

### Interaction

- Arrow keys and pointer behavior come from `ListBox<T>`.
- `j` and `k` move the workspace selection when the list owns focus.
- `Enter` activates the selected workspace through
  `IStraumrWorkspaceService.ActivateAsync`.
- `/` focuses filtering when filtering is implemented.
- `:` opens the shared command prompt.
- `c`, `e`, `y`, `x`, `i`, and `d` are introduced with their corresponding
  lifecycle operations, not as inert hints.
- Destructive actions require an explicit confirmation surface.

### Loading and Failure Behavior

- Show a spinner and concise loading text during initial I/O.
- Show a clear empty state when no workspaces exist.
- Keep the selected workspace visible while its request preview loads.
- Show recoverable operation failures in the screen or a framework toast/dialog.
- Do not swallow unexpected exceptions.
- Cancellation exits or abandons the operation without presenting it as a
  failure.

## Implementation Milestones

| ID | Milestone | Status | Evidence |
| --- | --- | --- | --- |
| P0 | Create implementation guide and tracker | Complete | This document |
| W1 | Replace prototype root with the shared application shell | Complete | `DockLayout`, reactive header/content, framework `CommandBar`; solution and CLI-only builds pass; fullscreen start/exit and CLI help verified |
| W2 | Add read-only Workspaces list and selected-workspace details | Not started | |
| W3 | Add selected workspace's recently used Requests pane | Not started | |
| W4 | Add focus, arrow, pointer, `j`/`k`, and activation behavior | Not started | |
| W5 | Add command prompt integration and workspace navigation commands | Not started | |
| W6 | Add filtering | Not started | |
| W7 | Add create, edit, copy, import, export, and delete workflows | Not started | |
| W8 | Validate resizing, empty/error states, CLI isolation, and Native AOT | Not started | |
| R1 | Implement Requests screen | Not started | |
| A1 | Implement Auths screen | Not started | |
| S1 | Implement Secrets screen | Not started | |

Only one milestone should be active at a time unless a prerequisite must be
completed with it. Update the status and Evidence column in the same change that
completes a milestone.

## Collaboration and Checkpoints

- Stop at a reviewable checkpoint after each milestone instead of attempting the
  entire TUI rewrite in one pass.
- Keep this document current so implementation can resume safely after context
  compaction or a later session.
- Ask the user to run an interactive check, platform-specific build, or publish
  when their local environment can provide better evidence than automation.
- Record unresolved framework behavior and user verification results in the
  active milestone's Evidence entry before moving on.
- Do not begin the next milestone until the current milestone builds and its
  smallest applicable behavior has been verified.

## Validation Checklist

For each Workspaces milestone, run the smallest applicable subset:

- [x] `dotnet build src/Straumr.sln`
- [x] launch `straumr` and inspect the Workspaces screen interactively
- [ ] verify resize behavior at narrow and wide terminal sizes
- [ ] verify keyboard and pointer selection
- [ ] verify focus restoration after prompt, dialog, and external editor use
- [ ] verify empty workspace registry behavior
- [ ] verify missing or corrupt workspace behavior
- [ ] verify cancellation during loading and operations
- [x] verify `straumr --help` still opens CLI help
- [x] verify `-p:IncludeTui=false` builds without the TUI dependency graph
- [ ] verify the full self-contained Native AOT publish when the screen slice is
      complete

## Definition of Done for Workspaces

- The implemented screen matches the approved information hierarchy.
- All displayed values come from Core or derived TUI presentation state.
- No data access occurs during rendering or the terminal update loop.
- Native controls own selection, focus, scrolling, and invalidation.
- All visible shortcuts perform their advertised action.
- The TUI, CLI help path, CLI-only build, and full build remain functional.
- Loading, empty, error, cancellation, and resize behavior have been exercised.
- This tracker records completed milestones and any accepted deviations.

## Decision Log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-09-10 | Implement one screen at a time, beginning with Workspaces | Keeps the rewrite reviewable and establishes reusable shell patterns first |
| 2026-09-10 | Use Core services directly from TUI application logic | Prevents CLI presentation concerns from leaking into the TUI |
| 2026-09-10 | Prefer framework controls, bindings, templates, and commands | Preserves retained-mode behavior and avoids duplicating framework machinery |
| 2026-09-10 | Keep external-editor workflows outside Core | Core owns model load/save; terminal integrations own editor orchestration |
| 2026-09-10 | Preserve `IncludeTui=false` | Keeps a compact CLI-only publish available |
| 2026-09-10 | Keep headers minimal and show the active workspace at right | Matches the approved layouts without invented status information |
| 2026-09-10 | Show only recent requests in the Workspaces detail preview | Keeps the pane relevant to workspace usage |

## Change Log

- 2026-09-10: Split the approved mockups into one editable HTML reference per screen.
- 2026-09-10: Created the guide. No TUI implementation was started.
- 2026-09-10: Began W1 and added explicit implementation checkpoints and developer-assisted verification.
- 2026-09-10: Completed W1 with the shared reactive shell and removed the manual Requests prototype.
