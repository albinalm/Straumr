# TUI Implementation Guide

This is the living specification and progress tracker for the Straumr TUI rewrite.
Update it whenever a milestone is completed, a design decision changes, or a
framework constraint is discovered.

## Current Status

- Phase: implementation
- Active screen: Workspaces
- Implementation: W6 implemented; interactive verification pending
- Next checkpoint: W6 populated filtering verification
- Shared building blocks are in place; see Shared Building Blocks before adding a screen
- Last updated: 2026-09-11

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
  centred and compact; the list panel's filter bar and the detail summary are closed
  by the rule that carries the section labels, so they take three rows and keep a
  blank row on each side, or their text crowds the labels on that rule.
- Both content bars must stay the same height for their rules to meet the column
  divider at a single crossing.
- The two panels mirror each other: a three-row bar, the rule closing it, then
  content. Each bar reads text left, recessed badge right. Every section title is
  notched into that one rule, and content starts on the same row on both sides. So
  floating in a bar means context about the whole panel, and notched into the rule
  means a titled section; the distinction carries meaning rather than history.
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
  focused list is the vivid band. Which resource is current is a separate question
  from which row is selected, so its marker is painted over whichever band the row
  carries and composes with all three levels instead of competing with them.
- Exactly one region owns focus, and its title says so by filling with the selection
  blue as a chip. Every other title is inert grey. A title that cannot take focus is
  always inert. So there is one filled blue title on screen, and finding it is how
  the eye answers "where am I" without having to compare two similar colours.
- Because every title shares the one rule, the chip only ever travels along it. The
  rule is the screen's tab strip, and focus reads as a step sideways rather than a
  jump between levels of the hierarchy.
- The filled accent surface belongs to focus alone. Quantities and identifiers use
  the recessed badge. Do not spread either further.
- Footer: context-aware shortcuts.
- The footer row is one row and holds one thing at a time: the shortcut hints, the
  command prompt while it is open, or a command's result. So the prompt does not
  add a surface or change the shell's height; it takes the row the hints were using
  and gives it back.
- `:` opens the command prompt.
- The prompt is modal while it is open: it owns the keyboard and the pointer, so no
  gesture belonging to a screen fires behind it, `Tab` cannot walk out of it, and a
  click elsewhere does not reach what it lands on.
- `Escape` or submission closes and clears the prompt, on the first press and
  whatever else is on screen. Nothing else can, because nothing else can take its
  focus away.
- `:q` is the only way out of the app, as it is in vim. The framework's own quit
  gesture is removed rather than left beside it, so there is one way to exit and the
  footer speaks one vocabulary.
- The prompt's colon sits in the same column the hints and messages start in, so the
  row reads as one line of text whichever of the three it is showing.
- A command's result replaces the hints on that row and expires, because the footer
  is also the only shortcut surface and a message that outlived its command would
  cost the screen its hints.
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
- Ask the developer before building a harness to answer a question they can answer
  from the running app. Automation is for what a person cannot see: exact cell
  colours, geometry at a size nobody will resize to, a table of command inputs.
  Whether a key works is not that.
- Snapshots cover layout and palette but nothing that needs input or focus, because
  neither exists outside a running loop. Driving those means a real `TerminalApp`
  and its internal `BeginRun`, `Tick` and `HandleTerminalEvent`, reached by
  reflection. Use `HandleTerminalEvent`, the entry point the input relay itself
  uses; `DispatchKeyEvent` routes commands but not the focused control's own key
  handling, so a probe using it reports keys as unhandled that the app handles.
  Treat the whole approach as a diagnostic of last resort rather than a test
  foundation: it depends on private members, it is easy to make it lie by ticking
  in the wrong order, and it took several wrong conclusions before it agreed with
  what the developer saw in one keystroke.
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
- Build the command prompt on `PromptEditor`, which already owns prompt prefixes,
  history, completion hooks, accept and cancel. Three of its constraints matter:
  its prompt column has a minimum width of two cells, so a one-character prompt
  renders a trailing blank and `" :"` is what puts the colon in the text column;
  `PromptEditorStyle` has no foreground for the editor's own text, so the palette
  reaches it through the `Highlighter` delegate rather than the style; and
  `PromptEditorCompletionPresentation.InlineCycle` keeps completion inside the
  prompt row, where `PopupList` would float a surface over the layout.
- Version 3.9.0 has no typed-command surface. `Command.Name` is documented for
  "a future command prompt" and `Command.Execute` receives only the target visual,
  so a command taking an argument cannot be expressed as a framework `Command`.
  Typed commands therefore live in `TuiCommandSet`, while every gesture stays a
  framework command.
- Register an app-wide gesture with `TerminalApp.AddGlobalCommand`. Command
  discovery collects global commands alongside the focus chain's, so a global
  command still shows in the `CommandBar`.
- `KeyGesture` compares modifiers for equality, and `new KeyGesture(':')` carries
  `TerminalModifiers.None`. A terminal that reports Shift for shifted punctuation
  therefore never matches it, so a character that needs Shift has to be registered
  twice, once bare and once with `Shift`. Only one of the two should be presented
  in the `CommandBar` or the hint appears twice. A control that reads `e.Char` in
  `OnKeyDown` sidesteps this, which is why `ResourceList`'s `G` works even though
  its command gesture alone would not match.
- `Visual.App` is null until the app is running, so anything that needs the
  `TerminalApp` (focus, global commands) has to happen from input or from the
  update loop, never from a constructor.
- Overlay visuals that must keep their identity in a `ZStack` and drive each one's
  `IsVisible`. `ContentSwitcher` looks like the control for this and is the wrong
  one: it attaches only the selected child, so the others have no `App` and cannot
  be focused. A visual the app has to focus also cannot be rebuilt by a
  `ComputedVisual` each frame.
- Focus is revoked from a visual that is not visible when the focus pass runs, and
  a `[Bindable]` computed only takes effect during that pass. So a visual that
  takes focus the moment it appears has to set its own `IsVisible` imperatively
  first; a binding that turns it visible later is too late and focus falls back to
  whatever claims `AutoFocus`.
- `Visual.HasFocus` is a bindable mirror of `TerminalApp.FocusedElement` and lags
  it by an update pass. Compare against `FocusedElement` when the answer is needed
  in the same frame focus moved.
- A printable keystroke arrives as two independent terminal events, a
  `TerminalKeyEvent` and a `TerminalTextEvent`, and handling the key does not
  suppress the text. So a character gesture that gives focus to a text control
  hands that control the very character that triggered it. Nothing in the framework
  suppresses the pair, so the control has to ignore the echo itself.
- The completion handler is called once per `Tab` and not while the user types, and
  the framework keeps no cycle state of its own: it re-asks on every trigger. So a
  handler that derives candidates from the current text can only ever offer the one
  it already inserted, and `InlineCycle` cycles nowhere. Cycling means holding the
  candidate list across triggers and recognising a repeat by the text and caret the
  previous completion produced. Because no request arrives between them, that
  recognition is exact.
- A surface that should own input while it is up implements
  `Input.IModalVisual`, the interface `Dialog` and `Popup` use. Declaring
  `IsModal` keeps focus traversal inside it, stops gestures on other visuals from
  firing, and swallows pointer input landing elsewhere. Without it a key the surface
  does not handle falls through to `Tab` traversal, focus leaves, and a surface that
  closes on lost focus disappears — which is what `Tab` on a prompt with no
  completion candidate did.
- Neither `PromptEditorEscapeBehavior` gives `Escape` one meaning: the default
  spends the first press dismissing an active completion and only the second closes
  the prompt, and `CancelCompletionOnly` stops it closing the prompt at all. To make
  one press always close, clear `CancelCommand.Gesture` in the `PromptEditorConfig`
  so no command claims the key, then handle `Escape` in `OnKeyDown` and call
  `Cancel()` before closing so the framework's own completion state is reset too.
- The framework registers its own quit command, `Ctrl+Q` in fullscreen hosting and
  `Escape` inline. `TerminalApp.RemoveGlobalCommand(TerminalApp.DefaultQuitCommandId)`
  takes it out, and takes both the gesture and its command bar hint with it, so an
  app that owns its own exit does not have to live beside a second one. Removing it
  is also what frees `Escape` inline; in fullscreen it was already free.

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
    StraumrTuiApp.cs              window frame, header, screen content, footer row
    TuiScreen.cs                  screen enum; its name renders in the header
    CommandPrompt.cs              the `:` prompt: open, close, focus, completion
    TuiCommand.cs                 one typed command and its result
    TuiCommandSet.cs              the command table: resolution and completion
  Screens/
    Workspace/
      WorkspaceScreen.cs          data loading and the parts unique to Workspaces
      WorkspaceScreenItem.cs      presentation model over StraumrWorkspace + entry
  Visuals/
    Shared/
      ResourceScreenLayout.cs     the list-and-detail screen scaffold
      ResourceFilter.cs           inline `/` filtering and focus behavior
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
    HttpMethodFormatting.cs       semantic colour per HTTP method
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
three, which is what the Auths and Secrets mockups need. A list whose rows all omit
`IsCurrent` reserves no gutter column for the current-resource dot, so a screen with
no such notion keeps that column for its text. Its contextual commands
expose `j`/`k` movement, `g`/`G` first/last jumps, and activation; arrow, Home/End,
and Page keys remain available without crowding the footer. `SetRows` updates a
retained list in place, preserving its identity and focus while filtering changes
the resources it displays. An optional empty visual occupies the same focusable
surface when no rows match.

`ResourceFilter` is the borderless single-line `/` editor used by resource screens.
It stays out of initial focus and Tab traversal until `/` or the pointer activates
it. Text changes filter immediately. `Enter` keeps the query and returns focus to
the results; `Escape` clears it and returns focus. The owning layout contributes the
`/` command so filtering is reachable from either panel without making it global to
screens that do not use the resource-browser scaffold.

`ScrollableContent` owns focus and scrolling for retained read-only content such as
the recent Requests preview. It exposes contextual `j`/`k`/`g`/`G` commands while
arrow, Home/End, Page and wheel input update the same bindable offset.

`FieldList.Create` builds a detail pane's label/value grid. Use `FieldList.Count`
for quantities so they inherit the amber-when-populated rule, `Wrapped` for values
long enough to wrap such as paths, and `Text` otherwise.

### The command prompt

`CommandPrompt` owns the `:` prompt: opening it, focusing it, restoring the focus it
took, clearing its text and asking for completions. It is the shell's, not a
screen's, so it lives beside `StraumrTuiApp` and every screen reaches it the same
way.

`TuiCommandSet` is the command table. A `TuiCommand` is a name, optional aliases, an
async handler that receives the argument text, and optionally a delegate supplying
its argument values for completion. The set resolves a typed name by exact match,
then alias, then unique prefix, so `:q` and `:w` work without being declared; an
ambiguous prefix names its candidates rather than guessing. It also answers
completion for whichever token the caret sits in: command names in the first token,
that command's argument values after it.

A handler returns a `TuiCommandResult`: nothing, a message, or a failure. The
application root shows it on the footer row and lets it expire. Handlers run from
the update loop rather than from the accept event, which is what lets them do I/O
and keeps them on the same path as the screen's other Core calls.

`StraumrTuiApp` registers the commands that belong to the whole app and appends what
the current screen contributes through its `PromptCommands`.

### Adding a screen

1. Add the screen to `TuiScreen`; the header renders its lowercased name.
2. Add `Screens/<Name>/<Name>Screen.cs` and, when a Core model does not directly
   represent what is shown, `<Name>ScreenItem.cs` beside it.
3. Load through Core services in a `LoadAsync`, into `State<T>` fields.
4. Map each item to a `ResourceRow` and call `ResourceScreenLayout.Create`.
5. Register the screen in `TuiConsoleIntegration.ConfigureServices` and navigate to
   it from `StraumrTuiApp`.
6. Expose the screen's typed commands as `PromptCommands` so the shared prompt picks
   them up.

Nothing in steps 1-6 touches layout, palette, dividers or row styling. If a screen
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
- run it with `Terminal.RunAsync` and hand the loop's `TerminalApp` to the root, which
  needs it for global commands and focus
- translate application exit into the process exit code

The TUI integration must register the Core services it requires and must not
depend on CLI registration as an accidental side effect.

The application root should own:

- current screen
- active workspace display state
- command prompt visibility and text
- the command table, composed from its own commands and the current screen's
- what the footer row is showing, and expiring a command's message
- top-level commands and exit state
- focus restoration when screens or overlays change

A screen should own:

- its loading, loaded, empty, and error state
- its selected index or selected item
- data loading and refresh after screen-specific operations
- visuals and commands that belong only to that screen, gestures and typed commands
  alike

## Workspaces Screen Specification

### Layout

Header:

- left: `{straumr} workspaces`
- right: `active workspace demo`, using the actual active workspace name

Left pane:

- filter affordance and a recessed count badge in a three-row bar, mirroring the
  detail summary and its identifier badge
- title `Workspaces` on the rule closing that bar, beside the detail section titles
- one multiline item per workspace, the first starting on the same row as the first
  detail value
- a green dot on the active workspace's middle line, in the gutter between the
  selection bar and the text, so the list itself says which workspace is live. The
  middle line is what reads as centred against a three-line row
- the marker follows activation, so pressing `Enter` moves it without a reload
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

Details group:

- titled `Details` rather than by the resource type, which the screen name and the
  summary bar directly above it already carry
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

- workspace validity labels
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
- Arrow, Home/End and Page keys and pointer behavior are owned by `ResourceList`,
  which replaced `ListBox<T>` once its fixed one-row item height ruled it out.
- `j` and `k` move the workspace selection when the list owns focus.
- `Enter` activates the selected workspace through
  `IStraumrWorkspaceService.ActivateAsync`. Two clicks on the same row do the same, so
  activation is reachable without the keyboard.
- `/` focuses the inline workspace filter.
- `:` opens the shared command prompt from anywhere on the screen, including from
  inside the request preview.
- `c`, `e`, `y`, `x`, `i`, and `d` are introduced with their corresponding
  lifecycle operations, not as inert hints.
- Destructive actions require an explicit confirmation surface.

### Filtering

- `/` focuses the inline filter without typing the trigger into the query.
- Pointer selection of the filter is supported without making it the initial focus
  target or a Tab stop.
- Filtering updates as text changes and matches workspace names and configured paths,
  case-insensitively.
- The count badge shows the total when no filter is active and `matches/total` while
  filtering.
- If the selected workspace remains visible, selection stays on it. Otherwise the
  first match is selected; no matches clear the detail panes and show an empty state
  on the still-focusable list surface.
- `Enter` keeps the current filter and returns focus to the result list. `Escape`
  clears the filter and returns focus to the list.
- `:workspace <name>` and `:use <name>` clear an active filter when necessary so a
  command can select any workspace, not only a visible match.

### Command Prompt

The commands the Workspaces screen answers, in addition to the app's own:

- `workspace <name>` selects a workspace without activating it. A name resolves by
  exact match, then unique prefix; an ambiguous prefix names the workspaces it
  matched.
- `use [name]` activates a workspace through `IStraumrWorkspaceService.ActivateAsync`,
  the named one or the selected one. `Enter` on the list does the same thing.
- `refresh` reloads the registry through Core, drops the cached request previews and
  keeps the selected workspace selected. It is the explicit refresh the load-once
  rule refers to.

`quit`, aliased `q` and `exit`, belongs to the application root and is the only way
out of the app; the framework's own quit gesture is removed on startup.

`Tab` completes: command names in the first token, workspace names after `workspace`
and `use`. `Up` and `Down` walk the prompt's history. A command that succeeds and
has nothing to report says nothing, because the list, the dot and the header already
show what changed; only `refresh` and failures produce a message.

Commands that would duplicate a gesture are deliberately absent. There is no `next`,
`first` or `last`, because `j`, `k`, `g` and `G` already move the selection, and no
screen-switching commands until there is a second screen to switch to.

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
| W4 | Add focus, arrow, pointer, `j`/`k`, and activation behavior | Complete | Implemented: Tab/Shift+Tab focus traversal, contextual command hints, arrows/Home/End/Page plus `j`/`k`/`g`/`G` on both the list and the request preview, wheel support, Core activation, and double-click activation. Framework finding: `PointerEventArgs.ClickCount` counts a click sequence by time and not by position, so a click anywhere followed by one click on a row arrived as a pair; the gesture therefore also requires both clicks on the same row, and a pointer leaving the list voids the sequence. Focus cues were reworked twice after review: the focused section title fills with the selection blue while every other title is inert, the permanently bright left detail title was fixed, all titles moved onto one rule so the chip travels sideways rather than diagonally, the first detail pane was retitled `Details`, and the active workspace gained a green dot that follows activation. Release and CLI-only builds pass. Cell dumps cover the chip states at exact hex, the mirrored panel geometry, and the dot across plain, hovered and both selected bands. Accepted interactively: focus cues, keyboard selection, hover band, pointer selection, the focused and unfocused selection bands, both focus directions, long-preview scrolling, top/bottom jumps, paging, activation moving the dot, and clean exit |
| W5 | Add command prompt integration and workspace navigation commands | Complete | `PromptEditor` overlaid on the footer row in a `ZStack`, the `:` gesture registered globally both bare and with `Shift`, `TuiCommandSet` with exact/alias/unique-prefix resolution and per-token completion, `quit`/`q`/`exit`, and the screen's `workspace`, `use` and `refresh`. `WorkspaceScreen`'s activation was split out so `Enter`, a double-click and `:use` share one method, and `LoadAsync` became re-runnable for `refresh`. Eleven framework findings, all recorded in Framework Rules: `PromptEditor`'s prompt column has a two-cell minimum, so `" :"` is what aligns the colon with the text column; `PromptEditorStyle` cannot colour the editor's own text, so the palette goes through the `Highlighter` delegate; `Visual.App` is null until the app runs; `ContentSwitcher` attaches only its selected child, which is why it cannot host a visual the app must focus; focus is revoked from a visual that is invisible during the focus pass, so the prompt sets its own `IsVisible` before asking for focus; `HasFocus` lags `FocusedElement` by a pass; and a printable keystroke emits a key event and a text event independently, so the gesture that opens the prompt also types its own character into it unless the prompt discards the echo; the completion handler is re-asked on every `Tab` and the framework keeps no cycle state, so the prompt has to hold the candidate list itself; neither `PromptEditorEscapeBehavior` gives `Escape` one meaning, so the prompt clears `CancelCommand.Gesture` and handles the key itself; and a key a surface does not handle becomes focus traversal, so a surface that must own input has to declare `IModalVisual` as `Dialog` and `Popup` do; and the framework's own quit command comes off through `RemoveGlobalCommand(DefaultQuitCommandId)`, gesture and hint together. Solution, Release and CLI-only builds pass. Evidence: command resolution and completion tables over 15 inputs and 14 caret positions; footer cell dumps at exact hex for hints, message, error and prompt states; full-screen dumps at 96x24, 70x20 and 44x14; and a full round trip driven through the real input path on a running `TerminalApp` — `:` opens and focuses the prompt, typed text reaches the editor, `Enter` runs `:use dashboards` through Core and returns focus to the list, a single `Escape` closes and clears even with a completion on screen, `:bogus` reports `unknown command: bogus`, nothing behind the modal prompt reacts to `Tab`, `Shift+Tab`, a screen gesture or a click, and `:q` is the only exit now that the framework's `Ctrl+Q` is removed. The developer confirmed `:` opens the prompt in a terminal, reported the stray colon that the echo discard now fixes, reported that `Tab` could not cycle between two workspaces sharing a prefix, which the held candidate list now fixes, and reported the three fall-through bugs that modality now fixes. Not covered: `Up`/`Down` history, which needs a terminal |
| W6 | Add filtering | Awaiting verification | Added the shared retained `ResourceFilter`, live case-insensitive workspace-name/path filtering, match/total badge, stable selection by workspace identity, a focusable no-match state, and `Enter`/`Escape` result focus behavior. The `/` gesture is registered in bare and Shift forms and its paired text echo is discarded. `ResourceList.SetRows` keeps list identity and focus stable while rows change. Debug, Release and CLI-only builds pass with no warnings; CLI help and an empty-registry launch/`:q` exit pass. Populated interactive behavior awaits developer verification |
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
- [x] verify pointer selection, including the hover band and the focused/unfocused
      selection band (headless snapshots render the unfocused state because the
      snapshot renderer does not apply `AutoFocus`, so this was verified in a terminal)
- [ ] verify focus restoration after prompt, dialog, and external editor use
- [x] verify `:` opens the prompt, that one `Escape` or submission closes and clears
      it whatever is on screen, and that focus returns to the region that had it
      (driven through `HandleTerminalEvent` on a running `TerminalApp`; the developer
      confirmed `:` in a terminal)
- [x] verify the prompt is modal: `Tab` with no candidate and `Shift+Tab` both leave
      it open and focused, a screen gesture behind it does nothing, and a click
      elsewhere is swallowed
- [x] verify `:q` is the only exit: `Ctrl+Q` does nothing and leaves the app
      responsive, a bare `Escape` on the list does not exit, and the footer no longer
      advertises a quit gesture
- [x] verify `Tab` completion cycles past the first candidate, over the reported
      prefix pair, command names, one candidate and none, and that editing mid-cycle
      starts a fresh list
- [ ] verify `Up`/`Down` walk the prompt history
- [x] verify typed command resolution, aliases, unique prefixes, ambiguity and
      unknown names (headless tables over the real command set)
- [ ] verify `/` from both list and request-preview focus, live name/path filtering,
      match counts, no matches, Enter retention, Escape clearing, and pointer entry
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
| 2026-09-10 | Activate on double-click as well as `Enter` | Activation was keyboard-only, so a pointer user could select a workspace but not use it |
| 2026-09-10 | Require both clicks of a double-click to land on the same row, rather than trusting `ClickCount` alone | `ClickCount` counts a click sequence by time and not by position, so a click anywhere followed by a click on a row arrived as the second of a pair and activated on what felt like a single click. Keeping `ClickCount` for the timing and adding the same-row test avoids running a click clock of our own |
| 2026-09-10 | Hold the current workspace's identity as screen state rather than as a field on the presentation item | It changes while the screen is live. Baked into `WorkspaceScreenItem` at load it went stale on activation, and nothing the list read was reactive, so the dot could not move until the next load. State read inside the list's builder is what makes the rebuild happen |
| 2026-09-10 | Mark the active workspace in the list with a green dot, reversing the rule against active-status labels | The header answers "which workspace is live" for global awareness but is the wrong place to look while working in the list. A dot in the row gutter is scannable down the column, costs no name width beyond its one reserved column, and needs no band of its own, so it composes with selection and hover rather than fighting them |
| 2026-09-10 | Title the first detail pane `Details` instead of the resource type, and never leave a section on the rule untitled | The screen name and the summary bar above already say which workspace this is, so `Workspace` only repeated them. Leaving the slot blank was the alternative, but an unlabelled section between two labelled ones reads as a missing label rather than a deliberate absence |
| 2026-09-10 | Move the list title onto the rule and lift the filter and count into the bar above it | The list title was a panel title floating in a bar while the section titles were notched into a rule two rows lower, so the two Tab stops were not visual peers and the chip travelled a diagonal. The left panel's extra filter rule also started its content two rows below the detail panes. One rule for every title fixes the diagonal, the floating-versus-notched mismatch and the row offset together, and the panels become mirror images |
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
| 2026-09-10 | Give the command prompt the footer row rather than a surface of its own | The mockup's footer already advertises `: command` and the shell is one frame with no floating layers. Taking the row keeps the shell's height fixed, needs no new border or shadow, and matches where a terminal user looks for a colon prompt |
| 2026-09-10 | Show a command's result on the same row and expire it | The row is also the only shortcut surface, so a message that stayed would cost the screen its hints for good. Expiry keeps the report visible long enough to read and then gives the row back; the loop is event-driven, so in practice the next keystroke is what clears it |
| 2026-09-10 | Overlay the footer's three contents in a `ZStack` and toggle `IsVisible`, not `ContentSwitcher` | The prompt has to be focusable and keep its text and caret, so it can be neither rebuilt by a `ComputedVisual` nor swapped by `ContentSwitcher`: the switcher attaches only its selected child, leaving the others without an `App` and therefore unfocusable. A `ZStack` attaches all three, so the editor the app focuses is the same attached instance every time |
| 2026-09-10 | Own the typed-command table instead of driving the prompt from framework commands | Version 3.9.0 has no typed-command surface; `Command.Name` is documented for "a future command prompt" and `Command.Execute` receives only the target visual, so `:use payments` cannot be a framework `Command`. Gestures stay framework commands, and only the typed layer is ours |
| 2026-09-10 | Resolve a typed name by exact match, then alias, then unique prefix | It is what a vim user expects from `:q` and `:w`, and it means the command table does not have to declare an alias for every abbreviation. An ambiguous prefix names its candidates instead of picking one |
| 2026-09-10 | Register the `:` gesture as a global command, disabled while the prompt is open | Global registration is the documented way to reach a gesture from anywhere, and command discovery still surfaces it in the command bar, so the footer hint and the gesture come from one declaration. Disabling it while the prompt is open, with `ConsumesGestureWhenUnavailable` off, is what lets a typed colon reach the editor |
| 2026-09-10 | Run command handlers from the update loop rather than from the accept event | Handlers do Core I/O. The loop is already where the screen's loading and activation happen, so draining a queue there keeps one asynchronous path and lets a command's selection change reach the request preview in the same frame |
| 2026-09-10 | Prompt with `" :"` and no left inset | `PromptEditor`'s prompt column has a two-cell minimum, so a bare `":"` renders `: text`, which reads as a stray space. Padding the markup instead of the control puts the colon in the column the hints and messages start in, so all three footer contents share one text column |
| 2026-09-10 | Colour the prompt's text through the `Highlighter` delegate | `PromptEditorStyle` styles the prompt, ghost, placeholder, selection and background but not the editor's own text, which otherwise keeps the framework theme's near-white. A single style run over the snapshot is the framework's own extension point for it |
| 2026-09-10 | Complete inline instead of in a popup | `PopupList` floats a surface over the layout, which the one-frame shell does not have anywhere to put. `InlineCycle` with ghost completion keeps the whole interaction on the prompt row |
| 2026-09-10 | Move the prompt's history onto `Up` and `Down` | The default `Alt+Up` is awkward, and in a single-line prompt the plain arrows have nothing else to do. `PromptEditorConfig` is the framework's own hook for the remap |
| 2026-09-10 | Add `refresh` and no other commands that duplicate a gesture | The load-once rule already refers to an explicit refresh that had no trigger, so `refresh` makes an existing decision real. `next`, `first` and `last` would only restate `j`, `k`, `g` and `G`, and screen-switching commands would advertise screens that do not exist yet |
| 2026-09-10 | Report only failures and `refresh` | Selecting and activating already show themselves in the list, the dot and the header, so a message would be noise that costs the footer its hints. A command whose effect is invisible is the one that has to speak |
| 2026-09-10 | Let the prompt set its own `IsVisible` imperatively and compare focus against `FocusedElement` | Both follow from when the framework does its work. Focus is revoked from a visual that is invisible during the focus pass, so a computed `IsVisible` that turns the prompt on later loses focus back to whatever claims `AutoFocus`; the prompt therefore shows itself before asking for focus. And `HasFocus` is a bindable mirror that lags `FocusedElement` by a pass, so a focus-loss check reading it fires on the frame the prompt opened. Reading the authoritative value instead removed the retry that had been papering over both |
| 2026-09-10 | Register the `:` gesture twice, bare and with `Shift` | `KeyGesture` compares modifiers for equality and a character gesture carries `None`, so on a terminal that reports Shift for shifted punctuation the prompt would never open and nothing would say why. Two registrations cost ten lines and remove a dependency on how the terminal reports a key; only the bare one is presented, so the footer still shows one hint |
| 2026-09-10 | Swallow the colon that opens the prompt in a small `PromptEditor` subclass | A printable keystroke emits a key event and a text event independently, so the gesture opened the prompt and the paired text event then typed a colon into it. There is no framework hook to suppress the pair, and every ordering-based dodge (focus a frame later, clear the text on the next pass) depends on the two events landing in the same input batch. Discarding one leading character equal to the prompt's own prefix does not: it is correct whichever order they arrive in, and a leading colon is not something a command could ever need. `OnTextInput` is the framework's own extension point for it, which is what justifies the subclass |
| 2026-09-10 | Hold the candidate list in the prompt so `Tab` can cycle | The framework re-asks the handler on every trigger and keeps no cycle state, so candidates derived from the current text collapse to the one already inserted the moment the first `Tab` fills a whole name in. The prompt now keeps the list and recognises a repeat trigger by the text and caret its own last completion produced, which is exact because no other request arrives in between. Any edit moves the text off that mark and starts a fresh list |
| 2026-09-10 | Remove the framework's `Ctrl+Q` quit now that `:q` exists | Two ways out is one more than vim has, and the second one was not ours: it put a `Quit ctrl+q` hint in the footer beside commands the screen actually owns, in a vocabulary the rest of the app does not use. `RemoveGlobalCommand(DefaultQuitCommandId)` drops the gesture and the hint together, so the footer reads as Straumr's own and `:q`, `:quit` and `:exit` are the exits. Signal handling is untouched, so the terminal's own interrupt still applies |
| 2026-09-10 | Make the prompt modal while it is open | Three reported bugs were one cause: a key the prompt did not handle fell through to focus traversal, focus left, and the prompt closed. `Tab` with no completion candidate closed it, `Shift+Tab` closed it and moved to the request preview, and a screen's own gestures were still live behind it. `IModalVisual` is the framework's answer, the one `Dialog` and `Popup` use, and it fixes all three at once instead of consuming keys one at a time. It also settles what "other actions are suppressed" means: the keyboard and the pointer both belong to the prompt until it closes |
| 2026-09-10 | Keep the lost-focus close as an invariant guard, not a feature | Modality means nothing can take the prompt's focus, so the check can no longer fire and the screen contract no longer promises it. It stays because it enforces "open implies focused" for six lines, and the failure it prevents — a modal prompt left open but unfocused, swallowing every key with no way out — is far worse than the cost of keeping it |
| 2026-09-10 | Make one `Escape` always close the prompt, against the framework's two-stage default | The screen contract says `Escape` closes and clears the prompt, and neither `PromptEditorEscapeBehavior` delivers that: the default spends the first press dismissing an active completion, and the alternative never closes the prompt at all. Documenting the two-stage behaviour was bending the contract to the framework. Clearing `CancelCommand.Gesture` and handling `Escape` in `OnKeyDown` gives the key one meaning; calling `Cancel()` before closing still lets the framework reset its own completion state, so reopening starts a fresh cycle |
| 2026-09-11 | Keep one retained `ResourceList` and replace its rows in place while filtering | Rebuilding the list for every character would replace the focused visual, reset scrolling and make focus restoration fail when a query had no matches. A stable list also gives the empty state somewhere valid to return focus to |
| 2026-09-11 | Use a borderless `PromptEditor` for the inline resource filter | It already owns single-line text input, paste, caret, selection, Enter and Escape. Reusing it keeps text editing in the framework while the resource screen owns only filtering semantics |
| 2026-09-11 | Keep the filter out of initial focus and Tab traversal | The filter exists while Core data is still loading, so an ordinary focusable editor claims initial focus before the workspace list is attached. Activating it only through `/` or the pointer preserves the list as the default region and keeps Tab moving between the list and detail preview |
| 2026-09-11 | Filter workspaces by name and configured path, and show matches over total | Both values are visible list identity, while request/auth counts are metadata rather than names. `matches/total` makes an active filter and its effect explicit without adding another label |

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
- 2026-09-10: Completed W4. Added double-click activation through the framework's
  `PointerEventArgs.ClickCount` for its timing, plus a same-row test: `ClickCount` is
  position-independent, so on its own it fired activation for a click outside the list
  followed by a single click on a row. Pointer selection, the hover band and both
  selection bands were accepted in a terminal.
- 2026-09-10: Moved the current-resource dot to the row's middle line so it reads as
  centred, and made activation move it: the current workspace's identity became screen
  state that the list builder reads, replacing the `IsCurrent` field that was baked
  into `WorkspaceScreenItem` at load and went stale on `Enter`.
- 2026-09-10: Marked the active workspace in the list with a green dot in the row
  gutter, added `ResourceRow.IsCurrent` to carry it, and reserved its column only when
  a row claims it. Removed the guide's own prohibition on active-status labels, which
  the request reverses. Verified by cell dump across plain, hovered, selected-focused
  and selected-unfocused rows, and with no current row at all.
- 2026-09-10: Renamed the first detail pane's title from `Workspace` to `Details`.
- 2026-09-10: Restructured both panels to mirror each other after visual review found
  the two focus targets asymmetrical. The list title moved onto the rule beside the
  detail section titles, the filter and the count moved into the bar above it, the
  filter's own rule and its `┤` junction went away, and list content gained a row of
  top inset so both panels start content on the same row. Verified by cell dump at
  96 columns with the focus chip bound on.
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
- 2026-09-10: Implemented W5. Added `CommandPrompt` over the framework's `PromptEditor`,
  `TuiCommand`/`TuiCommandSet` for the typed command table, the global `:` command, and
  the Workspaces commands `workspace`, `use` and `refresh` beside the application's
  `quit`. The footer row became a `ZStack` over the shortcut hints, a message line and
  the prompt, so the shell's height and the hints' text column are unchanged.
  `WorkspaceScreen`'s activation was split out of the pending-activation path so
  `Enter`, a double-click and `:use` all go through one method, and `LoadAsync` became
  re-runnable for `refresh`.
- 2026-09-10: Six framework constraints found while building the prompt, three of them
  only after the developer asked whether `:` was supposed to work. `PromptEditor`'s
  prompt column has a two-cell minimum, so a bare `":"` renders a stray space before
  the text and `" :"` is what aligns the colon with the hints' column.
  `PromptEditorStyle` has no foreground for the editor's own text, so the palette
  reaches it through the `Highlighter` delegate. `Visual.App` is null until the app is
  running. `ContentSwitcher` attaches only its selected child, so the prompt it was
  hosting had no `App` and could not be focused; the footer is a `ZStack` now. Focus is
  revoked from a visual that is invisible when the focus pass runs, so the prompt sets
  its own `IsVisible` before asking for focus rather than leaving it to a computed
  binding. And `HasFocus` lags `FocusedElement` by a pass, so the focus-loss check
  compares against `FocusedElement`. The last two together removed a focus-retry that
  had been hiding both.
- 2026-09-10: Registered the `:` gesture twice, bare and with `Shift`. `KeyGesture`
  compares modifiers for equality, so on a terminal that reports Shift for shifted
  punctuation the single bare registration would never have matched. The developer's
  terminal reports no modifier, which is why it worked there.
- 2026-09-10: Built a reflection harness to drive input on a running `TerminalApp`
  after the developer had already answered the question by pressing the key. It did
  find the attachment and focus bugs above, but asking first would have found them
  sooner and cheaper. Recorded the order in Framework Rules: ask, then automate what
  a person cannot see.
- 2026-09-10: Fixed a stray colon in the prompt, reported from the running app. The
  gesture opened and focused the prompt on the key event, then the same keystroke's
  independent text event typed a colon into it. `CommandPrompt` now discards one
  leading character equal to its own prefix through a small `PromptEditor` subclass
  overriding `OnTextInput`, which is order-independent unlike deferring focus or
  clearing the text a frame later. Reproduced and verified by driving both events
  through `HandleTerminalEvent`.
- 2026-09-10: Fixed `Tab` completion offering only its first candidate, reported from
  the running app with two workspaces sharing a prefix. The handler recomputed
  candidates from the current text, so once the first `Tab` had inserted a whole name
  that name was the only match. `CommandPrompt` now keeps the candidate list and the
  text and caret its last completion produced, and treats an unchanged document as a
  repeat trigger that advances the cycle. Measured first: the handler runs once per
  `Tab` and never while typing, which is what makes the recognition exact. Verified
  across the reported pair, command names, a single candidate, no candidate, editing
  mid-cycle, submitting a cycled value, and reopening after `Escape`.
- 2026-09-10: Made a single `Escape` close the prompt even with a completion on
  screen, which is what the screen contract always said. The framework's
  `CancelPromptOrCompletion` spent the first press on the completion, and its only
  alternative stops `Escape` closing the prompt at all; clearing
  `CancelCommand.Gesture` and handling the key in `OnKeyDown` gives it one meaning.
  `Cancel()` still runs first so the framework's completion state resets and a
  reopened prompt cycles from the start. Found while doing so that a probe driving
  `DispatchKeyEvent` misses the focused control's key handling entirely; the probes
  now use `HandleTerminalEvent` like the input relay does.
- 2026-09-10: Made the prompt modal, fixing three reported bugs with one change.
  `Tab` on a command with no completion candidate closed the prompt, `Shift+Tab`
  closed it and moved focus to the request preview, and the screen's own gestures
  were still live behind it. All three were the same fall-through: an unhandled key
  became focus traversal, focus left the prompt, and the lost-focus check closed it.
  `PromptInput` now implements `IModalVisual`, so while the prompt is up it owns the
  keyboard and the pointer. The lost-focus check stays as an invariant guard that can
  no longer fire, and the screen contract no longer promises lost focus as a way to
  close the prompt.
- 2026-09-10: Found that the earlier "no candidate leaves the text alone" check had
  passed only because it asserted before the update pass that closed the prompt. A
  probe that reads state without settling first can miss exactly the bug it covers.
- 2026-09-10: Removed the framework's built-in quit so `:q` is the only way out, as
  in vim. `RemoveGlobalCommand(DefaultQuitCommandId)` takes the `Ctrl+Q` gesture and
  its command bar hint together, leaving the footer showing only commands the app
  owns: `j`, `k`, `Enter`, `g`, `G` and `:`. Verified that `Ctrl+Q` now does nothing
  and the app stays responsive, that a bare `Escape` on the list does not exit
  either, and that `:q` still does. The probes moved to fullscreen hosting at the
  same time, because the exit gesture differs between hosts and the inline default
  had been swallowing `Escape` in tests.
- 2026-09-10: Observed while dumping the shell at small sizes that the footer row is
  dropped entirely below roughly 16 rows, the star row keeping its content's minimum
  instead. Pre-existing and unrelated to the prompt; recorded for W8.
- 2026-09-11: Implemented W6 filtering and left it at the populated interactive
  checkpoint. Added the shared inline `ResourceFilter`, made `ResourceList` update
  rows without replacing its focused visual, matched workspace names and paths live,
  preserved selection by workspace identity, added a focusable no-match state and a
  match/total badge, and kept typed workspace commands independent of the visible
  filter. Debug, Release and CLI-only builds pass; CLI help and an empty-registry
  launch and exit were exercised.
