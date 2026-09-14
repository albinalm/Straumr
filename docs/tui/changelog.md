# Change Log

Part of the [TUI implementation guide](./README.md). Newest first. History only —
nothing here is a rule. Read the most recent entries when resuming work.

- 2026-09-14: Persisted the Requests screen's three movable divider shares under its
  own `StraumrOptions.PaneLayouts["Requests"]` entry, leaving an independent settings
  slot for each future screen. Values restore before the retained tree is built, save
  through the update loop after a resize, and clamp malformed persisted shares. The TUI
  now loads options before constructing the shell and starts on Requests when the
  validated options contain an active workspace; otherwise it starts on Workspaces.
  A focused probe restored 42/53/61, moved and saved 46/53/61 exactly once, round-tripped
  the entry through generated JSON metadata, and confirmed Requests as the initial screen.
  Debug and Release solution builds and the Release CLI-only build pass without warnings.

- 2026-09-14: Fixed the Requests screen's ghost focus states, reported from a terminal:
  Tab reached positions where no region was titled and the resize keys moved a divider
  belonging to somewhere else. Three separate causes, found by driving Tab through
  `HandleTerminalEvent` on a running `TerminalApp` and reading `FocusedElement` one frame
  per press. `HasFocusWithin` excludes the visual itself, so the Response title went dark
  while its own `TabControl` held focus; `TabControl`'s tab strip is a focus stop of its
  own, so the Request and Response panes each answered two Tabs; and traversal tests only
  the candidate's `IsVisible`, so the hidden Workspaces screen kept its list in the
  rotation and took a Tab of its own. `FocusScope` now carries both questions the
  framework answers only about a single visual, the two shared focusables compute
  `IsTabStop` from their whole ancestor chain, and the tab strip is out of the rotation
  while staying clickable. The Requests cycle is now Requests → Authentication → Request →
  Response and back, forwards and in reverse, with a title lit at every stop, and the
  Workspaces screen rejoins the rotation when navigated back to. Debug, Release and
  CLI-only builds pass with no warnings, and the shared snapshots are identical to the
  same tree with the three fixes reverted.

- 2026-09-11: Checkpointed the first R1 implementation at the developer's request to
  conserve usage. They report that it works but have UI gripes to discuss next. Added
  `RequestScreen`, its presentation/authentication models, `ITuiScreen`, shared leading
  tokens, `PreviewPane`, bounded content formatting and stacked detail sections. The
  shell hosts both retained screens with typed navigation and rebuilds contextual
  commands on navigation. Shared pane headings/content now use one grid. Debug builds
  and local snapshot comparisons provide initial evidence; detailed interaction checks,
  the developer's visual feedback and R1 Release/CLI-only/AOT checks remain. R1 stays
  active; no later screen has started.

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
