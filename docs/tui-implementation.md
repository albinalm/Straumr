# TUI Implementation Guide

This is the living specification and progress tracker for the Straumr TUI rewrite.
Update it whenever a milestone is completed, a design decision changes, or a
framework constraint is discovered.

## Current Status

- Phase: implementation
- Active screen: Workspaces
- Implementation: W4 awaiting visual verification
- Next checkpoint: developer verification of focus cues, pointer focus, and interaction feel
- Shared building blocks are in place; see Shared Building Blocks before adding a screen
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

- A single window frame encloses the whole screen. Regions inside it are
  separated by one-cell rules, not by nested boxes with gaps between them.
- The whole app shares one background. Regions are told apart by dividers alone.
  Panel tints were tried twice and removed both times: a wide step made a boundary
  read as clipped, and a narrow one was not worth the seam it still produced.
- The background is dark enough that the accents carry it. A badge is the only
  surface allowed to sit above it, and scroll bar chrome is kept below the
  dividers so it never competes with content.
- A single line of text only reads as vertically centred in an odd-height band, so
  a bar is one row or three, never two. Choose by what closes the bar: the screen
  header is bounded by the window frame above and a plain rule below, so one row is
  centred and compact; the list heading and detail summary are closed by the rule
  that carries the section labels, so they take three rows and keep a blank row on
  each side, or their text crowds the labels on that rule.
- Both content bars must stay the same height for their rules to meet the column
  divider at a single crossing.
- Header left: `{straumr} {screen}`.
- Header right: `active workspace {name}` in a subdued success color.
- Header content uses normal-weight text with horizontal and vertical breathing
  room, closed by a rule.
- Main content: resource list on the left and selected-resource details on the
  right, separated by a column divider that carries the correct junction glyph
  wherever a rule meets it.
- Detail content begins with a summary spanning both detail columns, closed by a
  rule that aligns with the first rule of the list panel; the two sections beneath
  it are separated by a column divider and titled on that rule itself, one label
  per section column. The labels appear only when a resource is selected.
- Every value that can outgrow its region is trimmed with an ellipsis or wrapped.
  Nothing is hard-clipped mid-word.
- Colour carries meaning rather than decoration. A populated count is amber and an
  empty one is inert; the selected row lifts to the bright foregrounds; the active
  workspace is the only green on screen.
- The shell responds to focus and pointer. A list owns three row levels, distinct
  from each other: hover is the faintest, a selected row in an unfocused list is
  stronger and drops its accent marker to a neutral one, and a selected row in a
  focused list is the vivid band.
- Exactly one region owns focus, and its title says so by filling with the selection
  blue as a chip. Every other title is inert grey. A title that cannot take focus is
  always inert. So there is one filled blue title on screen, and finding it is how
  the eye answers "where am I" without having to compare two similar colours.
- The filled accent surface belongs to focus alone. Quantities and identifiers use
  the recessed badge. Do not spread either further.
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

- Compose the application shell from `Grid`, `Padder`, `Rule`, layout containers,
  and a footer or `CommandBar` where their behavior fits.
- Use `State<T>`, bindings, and computed visuals for changing UI state.
- Use `ListBox<T>` with a `DataTemplate<T>` for single-line resource lists.
- Use a small retained-mode list visual for multiline resource rows; the pinned
  `ListBox<T>` fixes every item to one terminal row and `Button` imposes an
  unsuitable bold, filled treatment.
- Bind list selection to state and derive the detail pane from that selection.
- Use framework commands and gestures for actions exposed in the command bar.
- Keep key handling local to the control that owns the interaction.
- Let built-in controls own focus, scrolling, pointer input, clipping, selection
  styling, and invalidation.
- Perform I/O outside render, measure, arrange, and per-frame update paths.
- Load once on entry and refresh only after an operation or explicit refresh.
- Show loading, empty, and error states as visuals driven by state.
- Use the approved Straumr palette through shared control styles, declared as hex
  literals in `Visuals/Shared/StraumrStyles.cs`. Avoid ad hoc colors outside the
  shared palette except for semantic mappings such as HTTP methods.
- Style every framework control that paints chrome of its own. `ScrollViewer`
  defaults to a bright grey track and thumb that does not belong to the palette.
- Verify layout and palette headlessly before asking for an interactive check.
  `VisualSnapshotRenderer.Render(root, width, height, Theme.Default)` returns a
  `CellBuffer`, and `CellBuffer.ToMarkupLines()` gives per-cell text with foreground
  and background, which is enough to assert geometry, trimming, junction glyphs and
  exact hex colours. `TerminalAppSnapshotRenderer.RenderSvg` renders the same tree
  to SVG when the result needs to be looked at rather than asserted. Two caveats:
  the snapshot renderer does not apply `AutoFocus`, so it renders the unfocused
  state, and the SVG exporter draws glyphs one row above their cell backgrounds, so
  trust the cell dump over the picture when they disagree.
- Refactors of shared visuals should be proved by diffing snapshots before and
  after; normalise generated ids and timestamps first.
- `Visual.Invalidate` is obsolete. Drive every visual state change through a
  `[Bindable]` partial property so the app invalidates on its own; the framework's
  own `TreeView.HoveredIndex` is the pattern for pointer state.
- `Theme` is immutable and can only be built by `Theme.FromScheme` from a
  16-color `ColorScheme`, so the palette is applied per control style rather than
  through the theme. Unstyled filler cells therefore keep the framework theme's
  near-white foreground; it is invisible on blank cells, but a Straumr
  `ColorScheme` is the eventual fix and any new visible text must be styled
  explicitly until then.
- Introduce a custom `Visual` only after confirming that composition, templating,
  or styling cannot express the requirement.
- Version 3.9.0 has no vertical rule control, so the column divider is painted
  through a `Canvas` painter in `Visuals/Shared/StraumrSurfaces.cs` rather than
  through a new `Visual`. There is no per-visual background either, which is a
  second reason to keep one background for the whole app.
- Do not use `Header` for the screen bars. It forces bold slot text and its own
  surface color, which the approved layouts do not use. `StraumrSurfaces.Bar`
  composes the same left/right arrangement from a `Grid`.

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

Current structure:

```text
Straumr.Console.Tui/
  Integration/
    TuiConsoleIntegration.cs      host: DI registration, Terminal.RunAsync, exit code
  Infrastructure/
    StraumrTuiApp.cs              window frame, header, screen content, command bar
    TuiScreen.cs                  screen enum; its name renders in the header
  Screens/
    Workspace/
      WorkspaceScreen.cs          data loading and the parts unique to Workspaces
      WorkspaceScreenItem.cs      presentation model over StraumrWorkspace + entry
  Visuals/
    Shared/
      ResourceScreenLayout.cs     the list-and-detail screen scaffold
      ResourceList.cs             multiline list with selection, hover and scrolling
      ResourceRow.cs              presentation model for one list row
      ScrollableContent.cs        focusable read-only content with Vim scrolling
      FieldList.cs                label/value grid for detail panes
      StraumrHeader.cs            the screen header bar
      StraumrSurfaces.cs          dividers, bars, insets
      StraumrStyles.cs            the palette and every control style
  Formatting/
    TimestampFormatting.cs        relative and absolute timestamps
    CountFormatting.cs            pluralised counts
```

Add a file only when it owns meaningful behavior.

## Shared Building Blocks

The Workspaces, Auths and Secrets mockups are structurally identical: a titled list
panel with a filter row on the left, and a selected-resource detail panel on the
right with a summary bar over two titled panes. That layout lives in
`ResourceScreenLayout` and a screen supplies only its own content.

`ResourceScreenLayout.Create` takes the list title, a count for the badge, the
filter placeholder, and three factories:

- `listContent` — the list, or a loading, empty or error visual.
- `detailHead` — the summary bar, or a message when nothing is selected. It must
  always return content, because its three rows are what align the two panels'
  rules against the column divider.
- `detailSections` — the rule closing the head plus everything below it, from
  `TwoPaneSections` or `EmptySections`.

Supporting helpers on the same class: `Pane` for pane padding, `Scrollable` to wrap
a `ResourceList` in the styled scroll viewer, and `Message` to centre a state
message.

`ResourceList` owns selection, hover, focus response, scrolling and row styling. A
screen hands it `ResourceRow` values and binds its `SelectedIndex`; it never styles
rows itself. A list whose rows all omit `Detail` lays out two lines high instead of
three, which is what the Auths and Secrets mockups need. Its contextual commands
expose `j`/`k` movement, `g`/`G` first/last jumps, and activation; arrow, Home/End,
and Page keys remain available without crowding the footer.

`ScrollableContent` owns focus and scrolling for retained read-only content such as
the recent Requests preview. It exposes contextual `j`/`k`/`g`/`G` commands while
arrow, Home/End, Page and wheel input update the same bindable offset.

`FieldList.Create` builds a detail pane's label/value grid. Use `FieldList.Count`
for quantities so they inherit the amber-when-populated rule, `Wrapped` for values
long enough to wrap such as paths, and `Text` otherwise.

### Adding a screen

1. Add the screen to `TuiScreen`; the header renders its lowercased name.
2. Add `Screens/<Name>/<Name>Screen.cs` and, when a Core model does not directly
   represent what is shown, `<Name>ScreenItem.cs` beside it.
3. Load through Core services in a `LoadAsync`, into `State<T>` fields.
4. Map each item to a `ResourceRow` and call `ResourceScreenLayout.Create`.
5. Register the screen in `TuiConsoleIntegration.ConfigureServices` and navigate to
   it from `StraumrTuiApp`.

Nothing in steps 1-5 touches layout, palette, dividers or row styling. If a screen
needs to, that is a signal to extend the shared piece rather than to hand-roll a
variant.

### Where the Requests screen differs

The Requests mockup is the one exception. Its detail panel has three regions rather
than two panes: a request head, an overview row of two panels, and a response
preview below. Its sidebar rows are also a single line led by an HTTP method token
in a semantic colour. So Requests reuses `Create`, the palette, the surfaces and the
formatting helpers, but supplies its own `detailSections`, and `ResourceRow` will
need a leading-token field for the method. Both are known extension points, not
redesigns.

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

- title `Workspaces` and a filled count badge, padded above and below, closed by a
  rule that aligns with the detail summary rule
- filter affordance row, closed by a rule
- one multiline item per workspace
- workspace name, trimmed with a trailing ellipsis
- request and auth counts, amber when the workspace holds anything and inert when
  it holds nothing
- workspace directory, trimmed with a leading ellipsis so the tail stays visible
- selection band spanning the panel inset with a leading accent bar; the selected
  row resolves to the bright foregrounds

Detail header:

- workspace name
- last-accessed timestamp, relative for recent values
- shortened workspace ID aligned right

Workspace detail group:

- path, home-shortened and wrapped, ellipsized when the region is too short
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

- The row under the pointer shows a hover band; the selection band and its marker
  desaturate while the list does not hold focus.
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
| W1 | Replace prototype root with the shared application shell | Complete | Reactive header/content and framework `CommandBar`; solution and CLI-only builds pass; fullscreen start/exit and CLI help verified. W2 later replaced the `DockLayout` root with a rule-separated `Grid` inside one window frame |
| W2 | Add read-only Workspaces list and selected-workspace details | Complete | Screen accepted interactively after several passes over layout, palette, vibrancy, cohesion and header. Extracted into `ResourceScreenLayout`/`ResourceList`/`FieldList`; refactor proved render-identical by snapshot diff at 120x28, 70x20 and 90x16, populated and empty. Solution and CLI-only builds pass; broader resilience checks remain in W8 |
| W3 | Add selected workspace's recently used Requests pane | Complete | Non-stamping request loading, per-workspace caching, recency ordering, loading/empty/error states and semantic method colours implemented. Release build passes; initial load, workspace switching, cache reuse and clean exit verified in an 80x24 populated terminal. User directed work to continue with W4 |
| W4 | Add focus, arrow, pointer, `j`/`k`, and activation behavior | In progress | Tab/Shift+Tab focus traversal, contextual command hints, list arrows/Home/End/Page plus `j`/`k`/`g`/`G`, request arrows/Home/End/Page plus `j`/`k`/`g`/`G`, wheel support, and Core activation are implemented. Release and CLI-only builds pass; terminal checks covered both focus directions, long-preview scrolling, top/bottom jumps, paging, activation feedback, and clean exit. Focus cues were then reworked: the focused section title fills with the selection blue, other titles are inert, and the permanently bright left detail title was fixed. Chip and inert states verified by cell dump at exact hex. Awaiting developer visual and pointer verification |
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
- [x] verify keyboard selection
- [ ] verify pointer selection, including the hover band and the focused/unfocused
      selection band (headless snapshots render the unfocused state because the
      snapshot renderer does not apply `AutoFocus`)
- [ ] verify focus restoration after prompt, dialog, and external editor use
- [x] verify empty workspace registry behavior
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
- Everything not specific to workspaces lives in `Visuals/Shared` or `Formatting`,
  and a second screen can be built without touching layout or palette code.

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
| 2026-09-10 | Do not use `ListBox<T>` for multiline resource cards | Version 3.9.0 measures and arranges every list item at a fixed height of one row |
| 2026-09-10 | Use the approved mockup colors as a shared application palette | Default control colors and button treatment do not preserve the mockup's hierarchy or contrast |
| 2026-09-10 | Build the shell from one window frame plus rules and surface tints | Nested bordered groups with gaps read as separate widgets instead of one screen |
| 2026-09-10 | Paint column dividers and surface tints with `Canvas` | Version 3.9.0 has no vertical rule control and no per-visual background |
| 2026-09-10 | Compose screen bars from `Grid` instead of `Header` | `Header` forces bold slots and its own surface color |
| 2026-09-10 | Trim list paths with a leading ellipsis and names with a trailing one | The tail of a workspace path identifies it; the head of a name does |
| 2026-09-10 | Verify layout with headless `VisualSnapshotRenderer` snapshots | Gives per-cell evidence of geometry, trimming, and palette before an interactive check |
| 2026-09-10 | Rotate the surface ramp from the mockup's hue 202 to hue 222 | The mockup's literal surface hex reads as teal in a terminal and dated on review; hue 222 keeps the same surface relationships and contrast while reading as deep blue |
| 2026-09-10 | Declare palette colors as hex literals | Keeps the ramp comparable to the mockup CSS at a glance |
| 2026-09-10 | Raise foreground and selection chroma above the mockup | The mockup's low-chroma foregrounds read as flat next to comparable TUIs; the surfaces stay as they are |
| 2026-09-10 | Resolve list row styles from the selection each frame | Gives the selected row its own hierarchy instead of relying on the band alone |
| 2026-09-10 | Colour counts by whether they are populated | Adds the variation that makes a screen read as live while carrying real information |
| 2026-09-10 | Put section titles on the divider rule instead of inside the panes | Reads as a labelled instrument rather than a caption floating in empty space, and returns two rows of pane height |
| 2026-09-10 | Add hover and focus row states to the list | A UI that answers the pointer and the focus ring feels live in a way no static palette can |
| 2026-09-10 | Do not brighten dividers on focus | In a rule-separated shell the rules are structure, not panel borders, so reacting to focus makes them shimmer |
| 2026-09-10 | Limit filled badges to the count and the identifier | Filled accent surfaces stop reading as emphasis once they are everywhere |
| 2026-09-10 | Mark the focused region by filling its title with the selection blue, and demote the count badge to the recessed treatment | Swapping two accent blues on Tab was near-invisible: the eye tracks luminance and position, not hue, and the signal was subtractive, so nothing appeared where focus landed. Reserving the one filled accent surface for focus makes it additive and unique |
| 2026-09-10 | Render an unfocusable section title as inert rather than accented | `TitledDivider` defaulted a missing focus predicate to the focused style, so the left detail title was permanently bright and bright-blue therefore did not mean "focused" |
| 2026-09-10 | Compress the surface ramp and raise panels above the chrome | The near-black list panel against the shell read as an abrupt step, so the boundary above the list heading looked clipped instead of divided |
| 2026-09-10 | Drop panel tints entirely for one shared background | Dividers already carry the structure; any tint step reintroduced a seam, and a flat dark ground lets the accents carry the screen. Removed the `ZStack`/`Canvas` wash helpers with it |
| 2026-09-10 | Darken the shared background to `#090D15` | Gives the accent, amber, green and selection more room to read against |
| 2026-09-10 | Make the screen header a single row | A two-row bar cannot centre one line of text, and three rows read as too airy; at one row the frame above and the rule below are equidistant, so it is both centred and compact |
| 2026-09-10 | Extract the list-and-detail scaffold into `ResourceScreenLayout` | The Auths and Secrets mockups are structurally identical to Workspaces, so there are three consumers rather than a speculative one |
| 2026-09-10 | Let `ResourceList` own row styling from `ResourceRow` data | Keeps the selection, hover and populated/inert rules in one place instead of re-deriving them per screen |
| 2026-09-10 | Prove the extraction with snapshot diffs and a throwaway second screen | Behaviour-preserving refactors of shared visuals are otherwise unverifiable without a terminal |
| 2026-09-10 | Give the list heading the same padding as the top bar and summary | A heading pressed against its rule reads as cut off; padding it also aligns the two panels' first rules |
| 2026-09-10 | Give retained read-only previews their own scroll surface | A plain visual has no focus or input ownership; `ScrollableContent` keeps its viewport, bindable offset, scrollbar, commands, keys and wheel behavior together without coupling screens to scrolling mechanics |

## Change Log

- 2026-09-10: Split the approved mockups into one editable HTML reference per screen.
- 2026-09-10: Created the guide. No TUI implementation was started.
- 2026-09-10: Began W1 and added explicit implementation checkpoints and developer-assisted verification.
- 2026-09-10: Completed W1 with the shared reactive shell and removed the manual Requests prototype.
- 2026-09-10: Began W2 with the committed W1 shell as the clean baseline.
- 2026-09-10: Reworked the W2 list after visual verification exposed the framework's fixed one-row `ListBox<T>` layout.
- 2026-09-10: Implemented the W2 workspace list/details slice; populated rendering remains to be verified against a real registry.
- 2026-09-10: Removed the premature filter affordance, replaced the interactive splitter with a weighted grid, and composed multiline workspace items inside `ScrollViewer`.
- 2026-09-10: Replaced button-based workspace rows with a retained multiline list and applied the approved palette after populated visual testing exposed poor contrast and excessive emphasis.
- 2026-09-10: Corrected the workspace shell to use a padded top bar, an in-panel list heading, a full-width selected-workspace summary, and two lower detail columns with headings inside their borders.
- 2026-09-10: Extracted the shared screen scaffold, resource list, row model, field
  grid and count formatting so Auths and Secrets can be built without new layout
  code; verified render-identical and exercised with a throwaway Secrets screen.
- 2026-09-10: Tightened the screen header to a single row. Two rows was tried first
  and rejected because one line of text cannot sit centred in an even band. Applying
  the same tightening to the content bars was also tried and reverted: it crowded the
  detail summary against its labelled rule and broke the panels' rule alignment.
- 2026-09-10: Replaced all panel tints with one shared `#090D15` background,
  deleted the surface wash helpers, and dimmed the scroll bar chrome that the
  darker ground had made prominent.
- 2026-09-10: Compressed the surface ramp for cohesion (chrome `#0D121C`, list
  `#111826`, detail `#141C2C`, badge `#0A0E18`) and padded the list heading, which
  moved the shared rule to row 3 and the filter rule to row 5.
- 2026-09-10: Added hover and focus response to the workspace list, a filled badge
  for the workspace count, and a recessed badge for the workspace identifier.
- 2026-09-10: Moved the detail section titles onto the divider rule via
  `Rule.StartLabel`, one per section column, shown only when something is selected.
- 2026-09-10: Raised palette vibrancy on review: fully saturated accent, amber and
  green, a vivid selection band, crisper dividers, bright foregrounds on the
  selected row, and counts coloured by whether they are populated.
- 2026-09-10: Reworked the palette to a deep blue ramp after visual review, and
  styled the list scrollbar, which was still painting the framework's default
  bright grey track and thumb.
- 2026-09-10: Replaced the boxed workspace shell with the mockup's rule-separated
  single frame: one window border, surface-tinted list and detail panels, column
  dividers with junction glyphs, mockup column ratios, the filter affordance row,
  ellipsis and wrap behavior on every value that can outgrow its region, a
  selection band inset from the panel edges, relative last-accessed timestamps,
  and a detail region that empties instead of showing bare section titles when
  nothing is selected.
- 2026-09-10: Accepted the W2 visual checkpoint and began W3 with non-stamping,
  per-workspace cached request loading for the recent Requests pane.
- 2026-09-10: Reworked the W4 focus cue after visual review. The focused section's
  title now fills with the selection blue as a chip and every other title is inert
  grey, the count badge dropped to the recessed treatment so the filled accent is
  unique to focus, and `TitledDivider` no longer treats a missing focus predicate as
  focused, which had left the left detail title permanently bright. Removed the
  now-unused dim-accent style and colour. Verified by cell dump in both states.
- 2026-09-10: Completed W3 and implemented W4 focus ownership, contextual commands,
  Vim and standard scrolling, pointer-ready focus targets, and asynchronous workspace
  activation through Core. Automated terminal behavior passes; visual and pointer
  feel await developer verification.
