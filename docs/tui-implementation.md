# TUI Implementation Guide

This is the living specification and progress tracker for the Straumr TUI rewrite.
Update it whenever a milestone is completed, a design decision changes, or a
framework constraint is discovered.

## Current Status

- Phase: implementation
- Active screen: Workspaces
- Implementation: W7 complete; every one of its checkpoints is accepted interactively.
  The Workspaces screen now carries its full lifecycle: create, edit, copy, import,
  export and delete
- Next checkpoint: W8, not started. It validates resizing, empty and error states, CLI
  isolation and Native AOT, and inherits the three unverified behaviours left on the
  checklist below: a workspace file broken or removed from outside Straumr, cancellation
  during loading and operations, and `Up`/`Down` prompt history
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
  `OnKeyDown` sidesteps this, but only if no command claims the key first: see the
  case-insensitive routing rule below, which is why `ResourceList`'s `g` was claiming
  `G` on a terminal that reports no modifier for it.
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
- A fullscreen `Terminal.RunAsync` owns one `TerminalApp` and tears down its raw-mode,
  cursor, mouse, paste, and alternate-screen scopes when the loop stops. It does not
  stop the underlying `TerminalInstance` input loop, so an external process that needs
  the terminal must run only after `RunAsync` returns and `StopInputAsync` completes.
- Re-entering fullscreen with the same retained tree requires the supplied root to be
  a `WindowLayer`. When given an ordinary visual, `TerminalApp` wraps it in an internal
  `WindowLayer` whose child relationship survives disposal, and the next hosted run
  rejects that visual as already parented. An explicit `WindowLayer` stays parentless,
  while its content retains all screen state and selection across runs.
- Every hosted run creates a new `TerminalApp`. App-wide commands therefore have to be
  registered for each new instance, and focus restoration has to happen after the
  retained tree attaches to that instance. Holding a visual as the restoration target
  is safe; trying to focus it between runs is not, because `Visual.App` is null then.
- `PromptEditor.Text`'s setter does not raise `OnDocumentChanged`. Only a user edit
  does, so any programmatic change to a prompt's text is invisible to a handler
  hanging off that hook and has to report itself.
- Gesture routing matches a character case-insensitively even though `KeyGesture`
  equality does not: `new KeyGesture('g')` and `new KeyGesture('G')` compare as
  different, but a routed `g` command still claims a `G` key event that carries no
  Shift. A control that wants both keys separately therefore has to keep the hint and
  handle the key itself, which is what `Command.RouteGesture = false` is for.
- A terminal sends `Ctrl` plus a letter as the single C0 byte the letter maps to, so a
  `Ctrl`+letter gesture has to carry that control character —
  `new KeyGesture((char)('L' & 0x1F), TerminalModifiers.Ctrl)`, not
  `new KeyGesture('l', TerminalModifiers.Ctrl)`, which matches nothing. The wrong form
  gives itself away in the command bar: the control character renders as `Ctrl+L` and
  the letter as a lowercase `Ctrl+l`. Measured against 3.9.0's decoder, the raw-byte
  path (Alacritty, xterm, Windows Terminal) and the CSI path (kitty keyboard protocol)
  both arrive as the control character plus `Ctrl`, so one gesture covers both; a host
  that reported the letter and the modifier separately would need the letter form
  registered beside it, unpresented.
- `Ctrl` or `Shift` plus a *named* key is not portable. A plain terminal sends
  `Ctrl+Enter` as a bare `Enter` — 3.9.0's decoder maps the byte to
  `Key=Enter, Modifiers=None` — so such a gesture works only where the kitty keyboard
  protocol or xterm's `modifyOtherKeys` is active, and is advertised everywhere else
  while doing nothing. Prefer a plain character for an accelerator.
- A plain-character gesture must be registered on the control that owns it, not on an
  ancestor. Routing walks the whole focus chain, so a character command on a dialog
  fires while a text field inside it has focus: `/` on the dialog opened the filter
  instead of reaching the path being typed.
- A global command is collected alongside the focus chain's rather than from it, so
  modality does not suppress it the way it suppresses a gesture on a visual. An
  app-wide gesture that should not reach a dialog has to say so itself, by walking up
  from `TerminalApp.FocusedElement` for an `IModalVisual`. A `CommandBar` re-collects
  on invalidation rather than every frame, so a gate that changes with focus takes
  effect on the pass focus moved and not before.
- A command on the focused control wins the gesture over a command with the same
  gesture on an ancestor. So a dialog's `Escape` does not have to be gated for a text
  field inside it to claim `Escape`; gating it is only worth doing to stop a second
  hint for the same key appearing in the command bar. Gating by `CanExecute` plus
  `ConsumesGestureWhenUnavailable = false` also works and falls through to the focused
  control's own `OnKeyDown`.
- A real `TerminalApp` can be driven headlessly, which covers what snapshots cannot:
  focus, input, and anything a `CommandBar` shows, since `CommandQuery.Collect` needs
  a running app and renders empty without one. `InMemoryTerminalBackend` plus
  `TerminalInstance.Initialize` builds the host; `BeginRun`, `Tick`, `EndRun` and
  `HandleTerminalEvent` are internal and still need reflection, but `CaptureSvg` is
  public. `TerminalKeyEvent`, `TerminalTextEvent` and `TerminalMouseEvent` are records
  with settable properties and a parameterless constructor, so events are built by
  property rather than by constructor. Pointer double-clicks are the one gesture this
  cannot reproduce: `ClickCount` is derived by the framework and not carried on the
  event.

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
      WorkspaceDeleteDialog.cs    destructive confirmation for workspace deletion
      WorkspaceFormDialog.cs      create/copy form and local validation
  Visuals/
    Shared/
      ResourceScreenLayout.cs     the list-and-detail screen scaffold
      ResourceFilter.cs           inline `/` filtering and focus behavior
      ResourceList.cs             one- to three-line list with selection, hover and scrolling
      ResourceRow.cs              presentation model for one list row
      ScrollableContent.cs        focusable read-only content with Vim scrolling
      FieldList.cs                label/value grid for detail panes
      BrowserDialog.cs            shared filesystem browser behavior
      FolderBrowserDialog.cs      folder-selection specialization
      FileBrowserDialog.cs        filtered-file selection specialization
      PathCompletion.cs           filesystem completion for browser path entry
      TextPromptDialog.cs         a modal asking for one line of text
      StraumrDialog.cs            shared modal construction and cancellation
      StraumrHeader.cs            the screen header bar
      StraumrSurfaces.cs          dividers, bars, insets
      StraumrStyles.cs            the palette and every control style
  Formatting/
    TimestampFormatting.cs        relative and absolute timestamps
    CountFormatting.cs            pluralised counts
    PathFormatting.cs             home-shortened paths
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
rows itself. A row marked `IsBroken` reads red at every row level, so a resource that
cannot be used is recognisable before it is selected. Row height follows the row data: a list whose rows all omit `Detail` lays
out two lines high, which is what the Auths and Secrets mockups need, and one whose rows
also omit `Meta` lays out one line high with no blank row between items, which is what a
list of plain names such as the folder browser needs. A list whose rows all omit
`IsCurrent` reserves no gutter column for the current-resource dot, so a screen with
no such notion keeps that column for its text. Its contextual commands
expose `j`/`k` movement, `g`/`G` first/last jumps, and activation; arrow, Home/End,
and Page keys remain available without crowding the footer. `activateLabel` names
`Enter` in the command bar, the one contextual command whose meaning changes per list.
`SetRows` updates a retained list in place, preserving its identity and focus while
filtering changes the resources it displays. An optional empty visual occupies the same
focusable surface when no rows match.

`ResourceFilter` is the borderless single-line `/` editor used by resource screens.
It stays out of initial focus and Tab traversal until `/` or the pointer activates
it. Text changes filter immediately. `Enter` keeps the query and returns focus to
the results; `Escape` clears it and returns focus. The owning layout contributes the
`/` command so filtering is reachable from either panel without making it global to
screens that do not use the resource-browser scaffold. `Clear` announces the change
itself, because `PromptEditor.Text`'s setter raises nothing; both `Escape` and a
screen clearing the filter to reach a hidden resource go through it.

A filesystem browser is a resource browser, so `BrowserDialog` is built from the
same pieces rather than from framework list controls: the location and a count badge
in a bar, its contents titled on the rule closing it, a `ResourceList` of one-line rows
below, and a second rule over a `CommandBar`. So the focus chip travels the same rule
it does on a screen, and `j`/`k`/`g`/`G`, hover, the selection bands, pointer
selection and double-click activation all arrive with the list instead of being
rebuilt. What it adds is its own: a parent row named after the folder it leads to,
`Backspace` to walk up landing on the folder just left, `Ctrl+L` to swap the
breadcrumb for an editable path with `Tab` completion, `s` or the button to return the
highlighted folder, `n`/`r`/`d` to create, rename and delete a folder, and one notice
line under the title for a failed read, an empty folder, or a query with no matches.
There are two confirms. `s` and the button return the *highlighted* folder, as a native
picker's button does, so a folder can be chosen without descending into it first; on the
row that walks back out there is nothing highlighted to return, so it falls back to the
folder being browsed. `Ctrl+Enter` returns the folder being browsed outright, which is
what makes "create a folder, open it, accept it" two keys. Both answers are on screen at
once: the breadcrumb at the top is what `Ctrl+Enter` returns, and the line beside the
button is what `s` returns, because a destination nobody can see is one nobody can trust.
Every gesture it uses is either a plain character, a named key, or `Ctrl` plus a letter
in its control-character form, so none of them depends on which terminal is running it.

It is a generic component, not a workspace one: a caller supplies the start path, the
title and the confirming button's label, and gets a path back. It therefore cannot tell
that a folder holds a workspace, and its rename and delete are as unguarded as a native
picker's — the registry stores absolute paths and can be broken from any file manager,
so guarding only inside Straumr would buy safety nowhere. Delete is recursive and final,
since a portable implementation has no recycle bin to reach for, so its confirmation
states what the folder holds rather than asking a bare yes or no. `TextPromptDialog`
is the one-line-of-text modal that create and rename both ask through, and a
row-scoped command is withdrawn rather than merely disabled where it does not apply,
because `CommandBarStyle` has no disabled treatment and an inert hint is
indistinguishable from a live one.

`FolderBrowserDialog` specializes it for folder selection. `FileBrowserDialog` takes a
file predicate and adds matching files beside navigable folders; folder activation
continues navigating, while file activation and the confirm button return the file. The
location field's `Tab` completion follows the same rule, so it completes both folders
and matching files. Import uses the file browser for `.straumrpak`; export uses the
folder browser.

`ScrollableContent` owns focus and scrolling for retained read-only content such as
the recent Requests preview. It exposes contextual `j`/`k`/`g`/`G` commands while
arrow, Home/End, Page and wheel input update the same bindable offset.

The workspace form shows the location it will actually use on a line under the field,
but only while that field is blank: once something is typed the field is already showing
the answer, and a line repeating it underneath is one path too many. Blank is the case
nothing else on screen can carry, and it is not something a placeholder can carry either
— a `TextBox` has no trimming control, so a long path filled the field head-first and cut
the tail, the half that says which folder it is. The line is trimmed from the front
instead, and the same property feeds both it and the submission, so what is shown and
what happens cannot drift apart. Leaving the field blank therefore means the location on
that line, not whatever the global default happens to be. Create offers the configured default; Copy
offers the folder holding the workspace being copied, which is where a sibling of it
would be written and is the answer far more often than a setting that has gone stale.

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

### Unreadable Workspaces

A registry entry whose file is not a workspace stays on the list instead of vanishing
from it. It is named after the folder Core created for it, since there is no name
inside the file to read, and it reads red on every row level so it is recognisable
without being selected.

- Two things make an entry unreadable: its file is not valid JSON, or the ID inside it
  is not the ID the registry has. Both are shown the same way and both are repaired the
  same way.
- An entry whose file is simply gone is not shown. That is a workspace removed from
  outside Straumr, not one in trouble, and Core's own listing drops it too.
- The summary bar reads the name and `cannot be read`; the Details pane names the path,
  states the problem in red, and says `e` opens the file for repair. The Requests pane
  says only that it is unavailable, because nothing can be read to list.
- It cannot be activated. `Enter`, a double-click and `:use` all report why instead,
  and `Copy` and `Export` withdraw from the command bar. `Edit` and `Delete` stay,
  because repairing it and removing it are the two things left to do with it.
- One unreadable workspace never fails the load. Each registry entry is read on its own,
  so the failure is scoped to the row it belongs to.

An edit that produces one of the two is written to the workspace file rather than
discarded, and the workspace is listed as unreadable until it is repaired. `e` on it
reopens exactly the text that needs fixing, so a mistyped brace costs a keystroke rather
than the edit.

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

### W7 Workflow Checkpoints

W7 is split by interaction shape so each reusable surface is reviewed before the
next one builds on it:

1. W7a: delete confirmation, async mutation, refresh, notification, and focus
   restoration.
2. W7b: create and copy input forms.
3. W7c: import and export path forms.
4. W7d: external-editor edit orchestration and terminal focus restoration.

Gesture callbacks only capture intent or close their surface. Core I/O runs from
the screen update path, then the screen reloads through the same retained state used
by `refresh`. Dialogs and forms use framework controls and shared Straumr styles.

## Implementation Milestones

| ID | Milestone | Status | Evidence |
| --- | --- | --- | --- |
| P0 | Create implementation guide and tracker | Complete | This document |
| W1 | Replace prototype root with the shared application shell | Complete | Reactive header/content and framework `CommandBar`; solution and CLI-only builds pass; fullscreen start/exit and CLI help verified. W2 later replaced the `DockLayout` root with a rule-separated `Grid` inside one window frame |
| W2 | Add read-only Workspaces list and selected-workspace details | Complete | Screen accepted interactively after several passes over layout, palette, vibrancy, cohesion and header. Extracted into `ResourceScreenLayout`/`ResourceList`/`FieldList`; refactor proved render-identical by snapshot diff at 120x28, 70x20 and 90x16, populated and empty. Solution and CLI-only builds pass; broader resilience checks remain in W8 |
| W3 | Add selected workspace's recently used Requests pane | Complete | Non-stamping request loading, per-workspace caching, recency ordering, loading/empty/error states and semantic method colours implemented. Release build passes; initial load, workspace switching, cache reuse and clean exit verified in an 80x24 populated terminal. User directed work to continue with W4 |
| W4 | Add focus, arrow, pointer, `j`/`k`, and activation behavior | Complete | Implemented: Tab/Shift+Tab focus traversal, contextual command hints, arrows/Home/End/Page plus `j`/`k`/`g`/`G` on both the list and the request preview, wheel support, Core activation, and double-click activation. Framework finding: `PointerEventArgs.ClickCount` counts a click sequence by time and not by position, so a click anywhere followed by one click on a row arrived as a pair; the gesture therefore also requires both clicks on the same row, and a pointer leaving the list voids the sequence. Focus cues were reworked twice after review: the focused section title fills with the selection blue while every other title is inert, the permanently bright left detail title was fixed, all titles moved onto one rule so the chip travels sideways rather than diagonally, the first detail pane was retitled `Details`, and the active workspace gained a green dot that follows activation. Release and CLI-only builds pass. Cell dumps cover the chip states at exact hex, the mirrored panel geometry, and the dot across plain, hovered and both selected bands. Accepted interactively: focus cues, keyboard selection, hover band, pointer selection, the focused and unfocused selection bands, both focus directions, long-preview scrolling, top/bottom jumps, paging, activation moving the dot, and clean exit |
| W5 | Add command prompt integration and workspace navigation commands | Complete | `PromptEditor` overlaid on the footer row in a `ZStack`, the `:` gesture registered globally both bare and with `Shift`, `TuiCommandSet` with exact/alias/unique-prefix resolution and per-token completion, `quit`/`q`/`exit`, and the screen's `workspace`, `use` and `refresh`. `WorkspaceScreen`'s activation was split out so `Enter`, a double-click and `:use` share one method, and `LoadAsync` became re-runnable for `refresh`. Eleven framework findings, all recorded in Framework Rules: `PromptEditor`'s prompt column has a two-cell minimum, so `" :"` is what aligns the colon with the text column; `PromptEditorStyle` cannot colour the editor's own text, so the palette goes through the `Highlighter` delegate; `Visual.App` is null until the app runs; `ContentSwitcher` attaches only its selected child, which is why it cannot host a visual the app must focus; focus is revoked from a visual that is invisible during the focus pass, so the prompt sets its own `IsVisible` before asking for focus; `HasFocus` lags `FocusedElement` by a pass; and a printable keystroke emits a key event and a text event independently, so the gesture that opens the prompt also types its own character into it unless the prompt discards the echo; the completion handler is re-asked on every `Tab` and the framework keeps no cycle state, so the prompt has to hold the candidate list itself; neither `PromptEditorEscapeBehavior` gives `Escape` one meaning, so the prompt clears `CancelCommand.Gesture` and handles the key itself; and a key a surface does not handle becomes focus traversal, so a surface that must own input has to declare `IModalVisual` as `Dialog` and `Popup` do; and the framework's own quit command comes off through `RemoveGlobalCommand(DefaultQuitCommandId)`, gesture and hint together. Solution, Release and CLI-only builds pass. Evidence: command resolution and completion tables over 15 inputs and 14 caret positions; footer cell dumps at exact hex for hints, message, error and prompt states; full-screen dumps at 96x24, 70x20 and 44x14; and a full round trip driven through the real input path on a running `TerminalApp` — `:` opens and focuses the prompt, typed text reaches the editor, `Enter` runs `:use dashboards` through Core and returns focus to the list, a single `Escape` closes and clears even with a completion on screen, `:bogus` reports `unknown command: bogus`, nothing behind the modal prompt reacts to `Tab`, `Shift+Tab`, a screen gesture or a click, and `:q` is the only exit now that the framework's `Ctrl+Q` is removed. The developer confirmed `:` opens the prompt in a terminal, reported the stray colon that the echo discard now fixes, reported that `Tab` could not cycle between two workspaces sharing a prefix, which the held candidate list now fixes, and reported the three fall-through bugs that modality now fixes. Not covered: `Up`/`Down` history, which needs a terminal |
| W6 | Add filtering | Complete | Added the shared retained `ResourceFilter`, live case-insensitive workspace-name/path filtering, match/total badge, stable selection by workspace identity, a focusable no-match state, and `Enter`/`Escape` result focus behavior. The `/` gesture is registered in bare and Shift forms and its paired text echo is discarded. `ResourceList.SetRows` keeps list identity and focus stable while rows change. Debug, Release and CLI-only builds pass with no warnings; CLI help and an empty-registry launch/`:q` exit pass. Accepted interactively by the developer: filtering worked as intended |
| W7 | Add create, edit, copy, import, export, and delete workflows | Complete | W7a accepted interactively: the `d` modal, cancellation, deletion, refresh, notification, and focus restoration work as intended. W7b implemented: `c` and `y` open one shared styled form for create and copy, with Name validation, an optional location using the configured default as its placeholder, a tab-reachable folder browser, keyboard/pointer controls, queued Core I/O, reload, selection of the result, and footer reporting. The folder browser is composed from the framework's modal, one-line list, scrolling, and button controls because version 3.9.0 and current upstream provide no ready-made directory picker. The empty registry keeps the focusable retained list mounted so Create remains reachable. The folder browser then became `FolderBrowserDialog`, a reusable component that takes a start path, a title and a confirm label and knows nothing about workspaces: navigation on a `ResourceList` of one-line rows, a parent row named after the folder it leads to, walking up landing on the folder just left, a front-trimmed breadcrumb swapping for an editable path on `Ctrl+L`, `s` or the button confirming the highlighted folder — a native picker's meaning, falling back to the folder being browsed on the parent row, with the resolved path shown beside the button — `Ctrl+Enter` confirming the folder being browsed outright, unpresented because only some terminals report the modifier, `n`/`r`/`d` to create, rename and delete, and one notice line for a failed read, an empty folder or a query with no matches. Delete is recursive with a confirmation naming the folders and files inside, because a portable implementation has no recycle bin; rename and delete are unguarded by design, the registry being equally exposed to any file manager. Ten defects were found and fixed by probes driving a real `TerminalApp`, and three by the developer in a terminal. From the probes: the path field stole initial focus so no list hints were reachable, `Escape` in it closed the whole picker, walking up lost the cursor's place, the count badge counted the parent row, `Ctrl+L` echoed its own character, a wrapped validation message, `Rename`/`Delete` advertised but inert on the parent row, and the browser opening on the parent row. From the developer: `Ctrl+L` did nothing, because `Ctrl`+letter arrives as the letter's C0 control byte and the gesture has to carry it — the probe had been synthesising an event shape no decoder produces; and `:` was offered while browsing, a global command being collected alongside the focus chain rather than from it. That audit also condemned `Ctrl+Enter` for Select, which a plain terminal cannot distinguish from `Enter`, and moved character gestures off the dialog onto the list, where they no longer fire while a text field has focus. Four pre-existing shared bugs came out with them, all fixed: `ResourceFilter` never reported a cleared query because `PromptEditor.Text`'s setter raises no event; `G` jumped to the top of a list in both `ResourceList` and `ScrollableContent` because gesture routing matches characters case-insensitively; and `ResourceRow.Meta` became optional for one-line rows. `ResourceList` proved render-identical for the three-line and two-line shapes by snapshot diff against `HEAD`. Debug, Release and CLI-only builds pass with no warnings; CLI help passes; 25 headless behavior assertions pass. The developer accepted the browser over four rounds of interactive review, which produced the remaining fixes: the `Ctrl+L` control-character gesture, `:` gated out of modals, Copy starting beside its source, the resolved location moved out of the placeholder, and the two confirms split so `s` takes the highlight and `Ctrl+Enter` the folder being browsed. W7b is complete but for a populated create and copy end to end, which has no developer confirmation of its own yet. W7c adds `i` import and `x` export through the shared `BrowserDialog` specializations: `FileBrowserDialog` lists and completes only `.straumrpak` archives beside navigable folders, while `FolderBrowserDialog` retains folder selection. Both operations queue Core I/O through `UpdateAsync`, reload and select an imported workspace, restore modal focus, and report through the footer. Debug, Release, CLI-only, and CLI help checks pass; the developer accepted the W7c UX interactively. W7d adds `e` editing through `$EDITOR`: the host ends fullscreen and stops the framework's persistent input loop before launching the process, then re-enters with the retained tree, reloads and reselects the edited workspace, restores list focus, and reports success or discarded changes in the footer. Quoted executable paths and editor arguments such as `--wait` are supported without a shell. A headless end-to-end probe verified command parsing, JSON edit/save, two consecutive fullscreen hosts over one retained tree, root detachment, and focus restoration. The developer then rejected discarding an edit that produced invalid JSON, so `ExternalEditor` now returns text and the screen owns what happens to text it cannot read: the edit is written to the workspace file and the workspace is listed as unreadable, named after its folder, red on the list, refused for activation, withdrawn from `Copy` and `Export`, and repaired by pressing `e` again. A mismatched ID is treated the same way rather than rejected and thrown away. Loading moved from `ListAsync` to one read per registry entry so one unreadable file scopes its failure to its own row instead of taking the screen to its error state, which also covers a file broken from outside Straumr. `ResourceRow.IsBroken`, `FieldList.Problem` and a `RedBright` palette entry carry it in the shared pieces. Debug, Release, solution and CLI-only builds pass with no warnings. The developer then accepted W7d interactively, and finally a populated create and copy end to end, which was W7b's own outstanding confirmation. All four checkpoints are accepted and W7 is complete |
| W8 | Validate resizing, empty/error states, CLI isolation, and Native AOT | Not started | Alternating CLI-only and full incremental builds in the same Release output can leave the full app without the TUI assembly; a full rebuild restores it. Separate or otherwise make variant outputs reliable during W8 |
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
- [ ] verify resize behavior at narrow and wide terminal sizes (the folder browser
      trims its height to the viewport on open; a resize while it is open is not handled)
- [x] verify the folder browser: initial focus, every advertised gesture, walking up
      landing on the folder just left, filtering and clearing, the path field and its
      completion, a folder that cannot be read, an empty folder, and a short terminal
      (headless, on a running `TerminalApp`; pointer double-click activation is inherited
      from `ResourceList` and was verified interactively in W4)
- [x] verify every gesture is encoded the way a terminal sends it, not the way a probe
      finds convenient: plain characters, named keys, and `Ctrl` plus a letter in its
      control-character form, with both decoder paths measured
- [x] verify `Ctrl+L` and `s` in a terminal, and that no dialog offers `:` (developer
      confirmed after the control-character fix)
- [x] verify the folder browser's create, rename and delete: the prompts and their
      validation, a delete confirmation naming the contents, cancellation leaving the
      folder alone, selection after a delete, and the parent and drive rows offering
      neither (headless, on a running `TerminalApp`)
- [x] verify create, rename and delete in a terminal (developer accepted the browser's
      operations; a cancelled and a confirmed delete were not called out separately)
- [x] verify a populated create and copy end to end: the form and its opening-gesture echo,
      Name validation, the resolved-location line appearing only while the field is blank,
      the Browse round trip, Copy starting beside its source, Core's duplicate-name refusal,
      and the result reloaded, selected and reported (accepted by the developer)
- [x] verify W7c import and export end to end, including archive-only file listing,
      folder navigation, filtering, `Ctrl+L` completion, selection, cancellation,
      imported-workspace selection, the exported package, and footer failures
- [x] verify Create offers the configured default and Copy the folder holding its source,
      that a blank location submits what the line under the field shows, that a typed one
      wins, and that a missing default is reported before submission
- [x] verify `s` returns the highlighted folder, that the parent row falls back to the
      folder being browsed, that plain `Enter` still descends, and that `Ctrl+Enter`
      returns the folder being browsed — including create, open, accept in two keys
      (headless, on a running `TerminalApp`)
- [x] verify keyboard selection
- [x] verify pointer selection, including the hover band and the focused/unfocused
      selection band (headless snapshots render the unfocused state because the
      snapshot renderer does not apply `AutoFocus`, so this was verified in a terminal)
- [x] verify external editor handoff, save, redraw, selection, and focus restoration in
      a populated terminal, and that an edit producing invalid JSON is written back rather
      than discarded, reads red, cannot be activated, names its problem in the detail pane,
      and reopens under `e` (accepted by the developer)
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
- [x] verify `/` from both list and request-preview focus, live name/path filtering,
      match counts, no matches, Enter retention, Escape clearing, and pointer entry
- [x] verify empty workspace registry behavior
- [ ] verify a workspace file broken or removed from outside Straumr: a corrupt one lists
      red and names its problem, a missing one is dropped, and neither takes the screen to
      its error state. The same presentation reached through an edit is accepted already;
      what is untested is reaching it without one
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
| 2026-09-11 | Split W7 by interaction shape and begin with deletion | Deletion exercises the shared modal, safe default focus, async Core mutation, refresh, notification, and focus restoration before forms, paths, or an external process add more variables |
| 2026-09-11 | Use the framework `Dialog` and `Button` controls for lifecycle surfaces | They already own modality, focus traversal, pointer input, command discovery, and close-time focus restoration; shared Straumr styles preserve the established visual language without replacing framework behavior |
| 2026-09-11 | Use one workspace form for Create and Copy | Both operations collect the same name and optional output directory; the Copy variant only adds source context. One retained form keeps focus, validation, buttons, styling, and submission semantics identical |
| 2026-09-11 | Keep the empty-state `ResourceList` mounted | Create must remain reachable when no workspace exists. The retained list already owns focus, gestures, and empty content, so replacing it with a plain message discarded useful behavior for no visual gain |
| 2026-09-11 | Suppress the opening `c` or `y` text event in the form's first field | Like `:` and `/`, a printable gesture arrives as separate key and text events. The key opens and focuses the form before the paired text event arrives, so the field must discard that one known echo or begin with an unintended character |
| 2026-09-11 | Compose a cross-platform directory picker from framework controls | The pinned package and current upstream have no file or folder picker. A native Windows dialog would compromise portability, Native AOT, and dependency isolation; a small `Dialog` plus `ListBox`, `ScrollViewer`, and buttons preserves all three and can be reused by export workflows |
| 2026-09-11 | Bind Enter submission to form text fields, not the whole dialog | Framework command shortcuts run before focused-control key handling. A dialog-wide Enter command therefore submitted the form while Browse owned focus; scoping it to Name and Location keeps fast field submission while Enter activates whichever button is focused |
| 2026-09-11 | Build the folder browser on `ResourceList` rather than the framework `ListBox` | The picker was the only list in the app that looked and behaved like a different product: `ListBoxStyle` has no hover state at all, and `j`/`k`, jumps, pointer selection and double-click activation were being rebuilt beside a shared list that already owns them. Single-line rows were the only thing missing, and the Requests screen needs those too, so extending the shared list served two consumers rather than one |
| 2026-09-11 | Give the folder browser its own `CommandBar` instead of relying on the shell footer | The footer does collect a modal's commands, but it sits at the far edge of the screen from a dialog that covers the middle, and on a short terminal the dialog can reach it. A modal that owns the keyboard should say so where the eye already is. The footer keeps showing the same hints; suppressing it for modals would mean giving every dialog a bar, which reopens two accepted surfaces |
| 2026-09-11 | Fix the hint bar at three rows | The number of hints changes with focus, so a bar sized to its content grew and shrank as `Tab` moved, and the folder list moved with it. Three rows is what the list-focused set needs at this width; spare rows when fewer hints show cost less than a list that will not hold still |
| 2026-09-11 | Name the parent row after the folder it leads to | `..` says there is a way up but not where up goes, which is most of what is disorienting about walking a tree blind. The parent's name makes every step legible before it is taken, and `↑` keeps the row from reading as a folder that is actually called that |
| 2026-09-11 | Land on the folder just left when walking up | Descending and coming back put the cursor on the parent row, so returning through a tree meant finding your place again at every level. Passing the folder being left as the preferred selection is what makes up and down inverse operations |
| 2026-09-11 | Keep the breadcrumb and the path editor in one cell, swapped on `IsVisible` | They are one thing in two states. The breadcrumb trims from the front so the folder you are in survives a path longer than the dialog, which an editable `TextBox` cannot do; the editor shows the real unshortened path, which is what you have to edit. A `ZStack` is the same idiom the footer uses for its three contents |
| 2026-09-11 | Trim the dialog to the viewport on `Show`, and fix its height otherwise | A list that grew and shrank with each folder's contents would move the buttons under the pointer, so the height is fixed. But at the full height a short terminal pushed the hints and both buttons off screen with no way to see them. `Visual.App` is null until the dialog is shown, so `Show` is the first point the viewport can be read |
| 2026-09-11 | Report a failed read and still show the way out of the folder | Committing no rows when enumeration failed left the browser in a folder it could not list, with an error and an empty list. Gathering the parent row separately from the folders means a denied folder still offers the row that walks back out of it |
| 2026-09-11 | Put a failed read, an empty folder and a query with no matches on one notice line | Only one of them can be true, and a list showing nothing but its parent row otherwise reads as a load that failed. One row under the title also means no state needs a region of its own, and the count badge already carries the number |
| 2026-09-11 | Confirm with `s`, and keep `Ctrl+Enter` as an unpresented alias | A plain terminal sends `Ctrl+Enter` as a bare `Enter`, so on its own the gesture needed the kitty keyboard protocol to work while being advertised everywhere — and worse than dead, it would descend into the folder instead of choosing it. The advertised key is therefore a plain letter, the most portable input there is. The alias costs a few lines and honours the muscle memory a native picker builds, on the terminals that report the modifier; it is not presented, because a key that does the wrong thing on some terminals should not be promised on all of them |
| 2026-09-11 | Confirm the highlighted folder rather than the one being browsed | It is what a native picker's button means, and it saves descending into a folder only to choose it — the keystrokes were the developer's reason for asking. The parent row has nothing highlighted to return, so it falls back to the folder being browsed, which doubles as the only way to choose that one. The resolved path sits beside the button so neither case has to be inferred |
| 2026-09-11 | Keep the target's glyph in a column of its own | The path is trimmed from the front so its tail survives a narrow dialog, and a marker placed at the head of that text is the first thing a leading ellipsis eats. The arrow was gone at the first width that mattered |
| 2026-09-11 | Split the two confirms: `s` takes the highlight, `Ctrl+Enter` takes the folder being browsed | They are genuinely different questions, and a picker wants both: choose a folder you can see, or walk into one and accept where you stand. Giving the second to `Ctrl+Enter` also puts each answer beside the thing that displays it — the breadcrumb for the folder being browsed, the target line for the highlight — so neither needs explaining. It stays unpresented because a terminal that does not report the modifier sends a bare `Enter` and opens the highlighted row instead |
| 2026-09-11 | Start Copy at the folder holding the workspace being copied | A copy belongs beside its source far more often than in whatever the global default points at — and that setting is the one most likely to be stale, as the developer's own was, left pointing at a contract-check temp directory while every real workspace lived elsewhere. Core lays workspaces out as `{output}/{name}/{id}.straumr`, so the folder a sibling goes in is one level above the workspace's own |
| 2026-09-11 | Show the resolved location under the field instead of in the placeholder | A `TextBox` placeholder has no trimming control, so a long path filled the field head-first and cut the tail, which is the half that identifies a folder. The line below is trimmed from the front and reads the same property the submission does, so the field can no longer promise one location and write another. It also lets a missing default say so before the operation fails rather than after |
| 2026-09-11 | Make a blank location mean the line under the field, not Core's default | Once Copy offers a location of its own, returning null on a blank field would have sent Core to the configured default instead — the field showing one destination and the operation choosing another. The resolved location is the only answer either of them reads now |
| 2026-09-11 | Bind the resolved line's trimming to what it is showing | A path keeps its tail and a sentence keeps its head, so one trimming mode cannot serve both: the missing-default warning came out as `…red. Choose one, or set config workspace-path.` `TextBlock.Trimming` has a `Func` overload, which is the framework's own hook for it |
| 2026-09-11 | Show the resolved location only while the field is blank | Typing a path put it on screen twice, once in the field and once on the line below it. The line exists for the destination that is otherwise invisible — the default that applies to an empty field — and a field with something in it is already the answer |
| 2026-09-11 | Scope plain-character gestures to the list, not to the dialog | Routing walks the whole focus chain, so `/` registered on the dialog fired while the path field had focus and opened the filter instead of typing a separator into a path. Characters belong to the control that owns them; `Ctrl`+letter can stay on the dialog because it is not text anyone can type |
| 2026-09-11 | Gate the app-wide `:` while a modal owns focus | The prompt belongs to the shell and its commands act on the screen behind a dialog, so offering it among a folder browser's navigation keys was noise for something that should not work there either. A global command is collected alongside the focus chain rather than from it, so modality does not suppress it and it has to check for itself |
| 2026-09-11 | Give the folder browser create, rename and delete, and generalize it as a reusable component | Native pickers all offer them, and the objection that rename or delete could break the registry does not hold: it stores absolute paths and is equally exposed to any file manager, so refusing here protects nothing and only forces a trip outside the app. Keeping the browser ignorant of workspaces is what makes it reusable by import and export later, and means it guards nothing a native picker would not |
| 2026-09-11 | State what a delete destroys rather than asking a bare yes or no | There is no recycle bin behind `Directory.Delete`, and a portable implementation has none to reach for — `Microsoft.VisualBasic.FileIO` is Windows-only and against the Native AOT and dependency-isolation goals. So the confirmation counts the folders and files inside first, bounded at a thousand entries because the count runs from a keystroke and scale is all it has to convey |
| 2026-09-11 | Withdraw a row-scoped command where it does not apply, rather than disabling it | `CommandBarStyle` has no disabled treatment, so a command the selected row cannot run rendered identically to one it could. After a milestone spent removing keys that were advertised and dead, leaving three more would have been the same defect by another route |
| 2026-09-11 | Select the first real folder on open, not the parent row | Opening a folder is a statement of interest in its contents. Landing on the row that walks back out also hid `Rename` and `Delete` from the hint bar until the cursor moved, which is the discoverability problem this milestone exists to fix |
| 2026-09-11 | Pause the fullscreen host around `$EDITOR` instead of awaiting the process inside its update callback | The framework deliberately keeps input and rendering alive while an async update awaits. Letting the editor run there would leave two owners fighting over one terminal. Ending the host releases its display modes, stopping the terminal input loop releases stdin, and a new host can then redraw the retained state |
| 2026-09-11 | Launch the configured editor directly from a tokenized command | `$EDITOR` commonly contains a quoted executable path or required arguments such as `code --wait`. Tokenizing those into `ProcessStartInfo.ArgumentList` supports both without shell-specific escaping or injection, while the temporary JSON path remains one safe final argument |
| 2026-09-11 | Ask for a name in a nested modal rather than inline in the location bar | The bar already does two jobs, and a third would have made one row mean three things. Delete needs a confirmation modal regardless, so the nesting depth was already there to be proved rather than avoided |
| 2026-09-11 | Extend the shared folder browser with filtered-file selection for import | Import and export should not introduce their own path UI after the browser was built for reuse. A caller-supplied predicate keeps filesystem policy outside the component while preserving one navigation, filter, completion and focus model |
| 2026-09-11 | Split filesystem browsing into a shared base and explicit folder/file dialogs | The selection policies are different concepts even though their navigation and presentation are identical. Keeping `FolderBrowserDialog` and `FileBrowserDialog` thin makes the call site state its intent without duplicating the browser |
| 2026-09-11 | Write back an edit Core cannot accept and list the workspace as unreadable, rather than reporting the error and discarding it | The text is the developer's work and the editor is gone by the time it is judged, so discarding it is unrecoverable: a mistyped brace costs everything typed beside it. Writing it back costs nothing that was not already the developer's own file, and makes `e` reopen the very text that needs fixing. It also means the screen has to survive an unreadable workspace, which it had to do anyway for a file broken from outside Straumr |
| 2026-09-11 | Load each registry entry on its own rather than through `IStraumrWorkspaceService.ListAsync` | The list answers with workspaces, so an entry that yields none has already been dropped by the time the screen sees it, and a file that throws rather than returning null took the whole screen to its error state. Reading per entry is what lets one row carry its own failure. Core keeps the reading; the screen only decides what an unreadable entry looks like, which is presentation |
| 2026-09-11 | Show an unreadable workspace's folder name rather than an invented placeholder | Core lays workspaces out as `{output}/{name}/{id}.straumr`, so the folder is the name the workspace was created with. It is a real answer already on disk, where `(unreadable)` or a bare ID would tell the developer nothing about which workspace this is |
| 2026-09-11 | Add a brighter red to the palette for a broken name on the selection band | The base red holds 7:1 over the background but only 3.4:1 over the selection blue, so a selected broken row would have failed the palette's own 4.5:1 rule. `RedBright` plays the part `TextBright` already plays for ordinary row text |

## Change Log

- 2026-09-11: W7 is complete. The developer accepted W7d interactively, including keeping
  an edit that produces invalid JSON, and then accepted a populated create and copy end to
  end, which was W7b's own outstanding confirmation — the folder browser had been accepted
  in its place. All four checkpoints are now accepted and the Workspaces screen carries its
  full lifecycle. Three behaviours remain unverified and pass to W8 rather than blocking
  W7: a workspace file broken or removed from outside Straumr, which reaches the unreadable
  presentation without going through an edit; cancellation during loading and operations;
  and `Up`/`Down` prompt history. W8 itself is not started.

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
- 2026-09-11: The developer accepted W6 filtering. Began W7 as four reviewable
  workflow checkpoints and implemented W7a deletion with a styled framework modal,
  safe Cancel focus, queued Core mutation, retained-screen reload, nearest-survivor
  selection, request-cache cleanup, and shared footer notification. Interactive
  verification remains before W7b begins.
- 2026-09-11: The developer accepted W7a. Extracted shared modal construction and
  implemented W7b Create and Copy forms with shared input styling, local Name
  validation, optional output location, queued Core operations, retained reload and
  result selection. Kept the resource list mounted for an empty registry so Create
  remains reachable. A live terminal probe caught and fixed the opening `c` text echo
  using the established printable-gesture rule; populated operation verification
  remains before W7c begins.
- 2026-09-11: Removed the validation error glyph and added a tab-reachable Browse
  button to the workspace form. Since the framework has no directory picker, added a
  shared cross-platform folder dialog with list navigation, Enter to descend,
  Backspace to go up, and Select Folder to return the absolute path. A live terminal
  probe caught a dialog-wide Enter command stealing activation from Browse; submission
  now belongs only to the two text fields, so focused buttons receive Enter normally.
- 2026-09-11: Reworked the folder browser for discoverability after review found it had
  no visible shortcuts and nothing that said where navigation was going. A probe driving
  a real `TerminalApp` on an in-memory backend found the cause of the missing hints
  first: the path field was taking initial focus from the folder list, so the only
  commands in the focus chain were the dialog's own and none of the list's. Four more
  defects came out of the same probe. `Escape` in the path field closed the whole picker
  because the dialog's command ran before the field's key handling, leaving the field's
  revert unreachable. Walking up reset the cursor to the parent row. The count badge
  counted the parent row. `Ctrl+L` typed its own `l` into the field it opened, the
  printable-gesture echo again. The dialog now opens on the list, carries its own
  three-row `CommandBar`, names the parent row after the folder it leads to, lands on
  the folder just left when walking up, swaps its breadcrumb for the editable path only
  when asked, reports a failed read while still offering the way out, and trims itself
  to short terminals. It is composed from `ResourceList` now rather than `ListBox`, so
  hover, the selection bands, jumps and pointer activation come from the shared list;
  `ResourceRow.Meta` became optional to give it one-line rows, which the Requests screen
  also needs. Proved render-identical for the existing three-line and two-line list
  shapes by snapshot diff against `HEAD`.
- 2026-09-11: Two pre-existing bugs in shared code surfaced while building the above,
  both affecting the Workspaces screen and neither introduced by it. `ResourceFilter`
  never reported a cleared query, because `PromptEditor.Text`'s setter raises no
  document-changed event: `Escape` emptied the editor while the results stayed filtered
  by the query that was no longer visible, and `:workspace <name>` could not reach a
  workspace the filter was hiding even though the screen contract says it clears the
  filter to do exactly that. `Clear` now announces the change itself. And `G` jumped to
  the top of a list instead of the bottom, because gesture routing matches a character
  case-insensitively while `KeyGesture` equality does not, so the routed `g` command
  claimed `G` before `OnKeyDown` could tell them apart; both jump commands now keep
  their hint and leave the key to `OnKeyDown` through `RouteGesture = false`. The guide
  had recorded the opposite conclusion about `G`, which held only on a terminal that
  reports Shift for it. `ScrollableContent` carried the same bug — `G` scrolled the
  request preview to the top instead of the bottom — and is fixed the same way; its
  commands were already named `Hint`, which is what `RouteGesture = false` declares.
- 2026-09-11: The developer reported that `Ctrl+L` did nothing and asked why `:` was
  offered while browsing folders. Both were real and both came from trusting a probe
  that fabricated its input. `Ctrl+L` was registered as `KeyGesture('l', Ctrl)`, which
  matches nothing a terminal sends: `Ctrl` plus a letter arrives as the letter's C0
  control byte, and the gesture has to carry that character. The probe had been
  synthesising `Char='l'` plus a `Ctrl` modifier, an event shape no decoder produces,
  so it confirmed a key that could never work; the lowercase `Ctrl+l` in the hint bar
  was the visible tell, since the correct form renders as `Ctrl+L`. The developer also
  warned that keybinds had broken across terminals on a previous framework, so the two
  encodings were measured rather than assumed: 3.9.0's decoder maps both the raw-byte
  path and the kitty CSI path to the control character plus `Ctrl`, so one gesture
  covers Alacritty, xterm, Windows Terminal and kitty alike, and the letter form is
  registered beside it unpresented in case a host reports the letter separately. That
  audit also condemned `Ctrl+Enter Select`, which a plain terminal cannot distinguish
  from `Enter`; it is now `s`. Moving `s` and `/` onto the list rather than the dialog
  fixed a third defect found while testing the first two: a character gesture on an
  ancestor fires while a text field has focus, so `/` had been opening the filter
  instead of reaching the path being typed. The `:` leak was separate — a global
  command is collected alongside the focus chain rather than from it, so modality never
  suppressed it — and it is now gated on whether an `IModalVisual` owns focus, which
  keeps it out of every dialog rather than just this one.
- 2026-09-11: Generalized the picker into `FolderBrowserDialog` and gave it the create,
  rename and delete a native picker offers. The developer made the case that refusing
  them protected nothing, and that is right: the registry stores absolute paths and is
  equally exposed to any file manager, so the only thing the refusal bought was a trip
  outside the app. They chose recursive delete with a confirmation naming the contents,
  and no workspace guard — which is also what keeps the browser reusable by import and
  export, since it now knows nothing about workspaces and takes only a start path, a
  title and a confirm label. `TextPromptDialog` was extracted for the one line of text
  that create and rename both ask for, `n`/`r`/`d` carry them, and the delete
  confirmation counts folders and files first, bounded at a thousand entries because the
  count runs from a keystroke. Three defects came out of testing it. The validation
  message wrapped to two lines, so it now names the actual culprit in one. `Rename` and
  `Delete` stayed advertised but inert on the parent row, because `CanExecute` alone
  leaves a hint visible and `CommandBarStyle` has no disabled treatment — they are
  withdrawn by `IsVisible` now, which is the same defect this milestone spent its time
  removing. And the browser opened on the parent row, which both buried those two hints
  and made a poor default; it lands on the first real folder now. Verified on a running
  `TerminalApp`: 25 assertions over the three operations, their validation and refusal
  paths, focus returning through three levels of nested modal, nearest-survivor
  selection after a delete, the guards on the parent and drive rows, and the earlier
  gesture fixes as regression cover. All thirteen hints still fit the bar's three rows.
  `ResourceList` re-proved snapshot-identical to `HEAD`; Debug, Release and CLI-only
  builds pass with no warnings.
- 2026-09-11: The developer asked for `Ctrl+Enter` back, to choose the highlighted folder
  without descending into it. The keystroke saving was the point, and the better answer
  was to change what confirming means rather than to add a key: `s` and the button now
  return the highlighted folder, as a native picker's button does, falling back to the
  folder being browsed on the row that walks back out. `Ctrl+Enter` came back beside it
  as an unpresented alias, which measurement justified rather than ruled out — a terminal
  reporting the modifier matches it, and one that does not sends a bare `Enter` and
  descends, so it is honoured where it works and promised nowhere. The resolved path is
  shown beside the button, since a fallback nobody can see is a fallback nobody can
  trust, and its arrow needed a column of its own: the path is trimmed from the front, so
  a leading marker was the first thing the ellipsis ate.
- 2026-09-11: The developer asked for `Ctrl+Enter` to confirm the folder being browsed
  rather than the highlighted one, so that creating a folder, opening it and accepting it
  is two keys. They are two different questions and the browser now answers both: `s` and
  the button take the highlight, `Ctrl+Enter` takes the folder being stood in. Each is
  displayed by the surface next to it — the breadcrumb for one, the target line for the
  other — so the split needs no explaining. Worth noting for the terminals that do not
  report the modifier: `Ctrl+Enter` reaches them as a bare `Enter` and opens the
  highlighted row, which is now a different action rather than merely a different
  destination, and is why it stays unpresented. `s` on a freshly created folder reaches
  the same result in one key, since creating selects what it created.
- 2026-09-11: The developer showed the browser opening at a `.tmp/claude-cli-contract-check`
  directory and asked for the start path to come from context. The path itself was not a
  bug — the browser was faithfully opening `DefaultWorkspacePath`, which their
  `options.json` had left pointing at a contract-check run while every real workspace sat
  elsewhere. That is the argument for the change rather than against it: a global setting
  goes stale, and context does not. Copy now starts at the folder holding the workspace
  being copied, which is where a sibling of it would be written; Create still offers the
  configured default. Two problems surfaced while wiring it. The location was being
  advertised through the `TextBox` placeholder, which has no trimming control and so
  showed the head of a long path and cut the tail — the same defect as the browser's
  breadcrumb, in the one place it had not been fixed; the resolved location now has its
  own line under the field, trimmed from the front, and a missing default says so there
  rather than failing on submission. And a blank field still meant "null", which after
  this change would have sent Core to the global default while the form displayed the
  source's folder, so both now read one property. Verified by nine assertions over both
  operations, a typed override, a blank submission and an unconfigured default.
- 2026-09-11: The developer pointed out that a typed path was then showing twice, in the
  field and again on the line below it. The line now appears only while the field is
  blank, which is the case it was added for: the default that applies to an empty field
  and has nowhere else to be seen. A typed path needs no echo, and the field scrolls with
  the caret, so the tail stays visible as it is entered.
- 2026-09-11: Implemented W7c import and export through the shared browser. Import adds
  only `.straumrpak` files beside navigable folders and completes both from `Ctrl+L`;
  export retains folder selection. Both gestures queue Core I/O in `UpdateAsync`, import
  reloads and selects its result, and both report through the footer. Debug, Release,
  CLI-only and CLI help checks pass; interactive verification remains.
- 2026-09-11: Extracted the browser implementation into `BrowserDialog`; folder and
  filtered-file selection now live behind thin `FolderBrowserDialog` and
  `FileBrowserDialog` specializations. Import uses the file dialog and export uses the
  folder dialog.
- 2026-09-11: The developer accepted the W7c import/export UX interactively. W7d is the
  next checkpoint.
- 2026-09-11: Workspace editing no longer discards an edit Core cannot accept. The
  external editor now returns text and leaves parsing to its caller, so the screen decides
  what to do with JSON it cannot read: it writes the text to the workspace file and lists
  the workspace as unreadable until it is repaired. That required the screen to survive an
  unreadable workspace, which it now does for one broken from outside Straumr too — each
  registry entry is read on its own, a failure is scoped to its row, the row is named after
  its folder and reads red, the detail pane names the problem and points at `e`, activation
  is refused with its reason, and `Copy` and `Export` withdraw while `Edit` and `Delete`
  stay. A mismatched ID inside the file is treated the same way rather than being rejected
  and thrown away. Shared pieces gained `ResourceRow.IsBroken`, `FieldList.Problem`, and a
  `RedBright` palette entry for a broken name on the selection band. Debug, Release,
  solution and CLI-only builds pass with no warnings. Interactive verification remains.
- 2026-09-11: Implemented W7d workspace editing through `$EDITOR`. The screen queues a
  reusable external action rather than starting a process under the live TUI; the host
  lets `Terminal.RunAsync` release fullscreen modes, explicitly stops the framework's
  persistent input loop, executes the edit, then starts a new host over the same retained
  state. An explicit `WindowLayer` makes that tree re-hostable, and app-wide commands are
  registered on every new `TerminalApp`. The editor writes source-generated JSON to a
  temporary file, supports quoted executable paths and arguments without a shell, rejects
  changed workspace IDs, deletes the file, saves through Core, reloads and reselects the
  workspace, and restores list focus on attachment. A headless end-to-end probe edited and
  saved a workspace, reused the root across two fullscreen runs, and verified the restored
  focus. Debug, Release, CLI-only, and CLI help checks pass. Interactive verification
  remains.
