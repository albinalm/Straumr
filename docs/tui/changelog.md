# Change Log

Part of the [TUI implementation guide](./README.md). Newest first. History only —
nothing here is a rule. Read the most recent entries when resuming work.

- 2026-09-15: The hint rows wrap instead of clipping. A bar held to one row drops the hints that do
  not fit, and it drops them from the end — which is where the less-used actions sit, and where a
  reader looks when they do not already know the key. `CommandBar.MultiLine` is the framework's own
  switch for it and the folder browser already used it; the shell's footer, the editor's and the
  full-screen response's now do too. Each of those rows is sized to its content, so the screen above
  gives up the lines, which is the right way round. The row does change height as focus moves and
  the hints with it — the folder browser pins itself to three rows to stop that moving a list under
  the pointer, and any region that cannot take the shift can do the same.

- 2026-09-15: Fixed the screen behind stealing focus, which the developer found two ways into:
  pressing `t` to change the Request pane's tab, and clicking the blank strip above the first row.
  Both end the same way, and the footer gave it away by showing the Workspaces screen's commands
  while Requests was on show. The framework re-homes lost focus on every render by taking the first
  focusable that claims `AutoFocus`, and that search tests the candidate's own visibility and not
  its ancestors' — the same ancestor-blindness as `Tab` traversal, on a path `IsTabStop` never
  reaches. The hidden screen's list claimed `AutoFocus` unconditionally and sat earlier in the tree,
  so it caught every stray focus in the app while staying invisible. Both screens now claim it
  through `FocusScope.IsReachable`, which is the same answer already given to `Tab`. The other half
  was the losing of focus itself: `t` hid the page under the caret and put focus nowhere, where
  `PagedPane` has always taken focus with the page it selects. `PreviewPane` now does too, and stopped letting
  `TabControl` swap its content to get there. Two attempts went the other way first and the
  developer saw both: deferring the focus a pass left a frame with focus nowhere in the pane, which
  reads as the screen flickering to the list and back, and holding focus on the strip across that
  frame cost the footer this page's keys, which is the same fault one step quieter. The control now
  gets one content visual for every tab — a stack holding all three pages — so nothing is ever
  detached, the current page is made visible outright, and focus moves in the keystroke that
  selected it. It keeps the strip, the selection and the styling; it just stops owning the swap.
  `PagedPane` has always worked this way, which is why it never had the problem.

- 2026-09-15: Saving keeps the editor open. It used to close the view and report on the screen
  behind, which is wrong for a form you are still working in — an edit and a save is one thing you do
  repeatedly, not a way out. The marker in the bar now answers instead: green the moment the save
  lands, grey a second later, reading `saved` either way. It needs a clock of its own, since the
  update pass runs on input and a save is followed by none, so the marker is wrapped in a small
  animated visual that fades it on the framework's animation scheduler — the same clock the in-flight
  duration ticks on. Staying open changed three things behind it: a created request stops being new,
  so the next save changes what the first one wrote rather than creating a second (the screen holds
  the id rather than capturing it); the comparison the unsaved marker uses is re-based on what was
  just written, or every save would immediately read as unsaved again; and the result line moved to
  the editor's own footer, since the shell's is behind it.

- 2026-09-15: Secrets became a region of its own. It had been a paragraph at the bottom of the
  Authentication pane, listing names that are already visible in the body and the URL and printing
  `None` the rest of the time. It answers a different question from the rest of that pane — whether
  this request can fill itself in when it is sent — so it now sits under Authentication in the same
  column with its own title on its own rule, its own share of the height, and its own scrolling. The
  references are carried as references rather than as one joined string, so each takes a row with
  its name in the secret colour and `unavailable` in red: the reason a send will fail, before it
  does. `TwoPaneSections` grew an optional predicate for the left title, because a column holding
  two regions cannot have its title lit by both. The full enumeration of what an auth references
  belongs on the Auths screen when A1 comes; what is here is scoped to this request.
- 2026-09-15: A secret reference in a label now reads as `{token}` rather than `{{secret:token}}`.
  The stored form is deliberately unmistakable, which is right in a document being written and
  wrong in a list of names — the developer's request name ended in `{{secret:t…`, most of a row spent
  on syntax and then trimmed before reaching what it refers to. `SecretFormatting.Display` shortens
  it for the request list, the summary bars, the editor's header and the full-screen response's.
  Only labels: the editor's fields and the body preview keep the stored text, because that is the
  text being edited.

- 2026-09-15: The unsaved marker now answers whether anything differs, not whether anything was
  typed. It latched on the first edit and never came back, so undoing a change by hand left the
  editor claiming work that no longer existed — and `Escape` asking about it. `ResourceEditorView`
  takes a `hasChanges` from its caller and re-asks after every edit; `RequestEditor` keeps a copy of
  the request as it was opened and compares. Bodies a type no longer uses are left out of the
  comparison, since the editor deliberately keeps what every type was given and looking at XML
  should not count as editing. A new or copied request stays unsaved regardless, having nothing to
  be the same as. Removed the `Credential material hidden` line from the Authentication pane at the
  developer's request: that a token is not printed on screen is what anyone would assume, and the
  line spent a row of the pane saying it.

- 2026-09-15: The editor's chrome went stale: the bar still said `GET` after the method was changed
  to `PUT`, the URL beside it never moved, and the header kept the old name while a new one was
  typed. All three read the state being edited, which is a plain object the binding graph knows
  nothing about — the same root as the Body page's discriminator, in a place that only looks
  different. What made it strange to look at is that the method's colour did follow: a style given
  as a function is a factory the renderer calls every frame, while a visual's dynamic text is a
  binding re-read only when something it read changes. The three values are now mirrored into
  `State<T>` once per update pass, by `RequestEditor.SyncSummary` and by the view for the header, so
  every binding over them works normally again. Copying into tracked state is the general answer
  where assigning outright is not: the bar and the header hold no focus, so there is no reason to
  hand-write what the bindings already do well.

- 2026-09-15: The Vim keys now work on a dropdown that is closed, not only on the list it opens.
  The developer found it on the multipart part's kind picker, and it was true of every dropdown in
  the app: the earlier change reached the popup through the style's factory, which is the only hook
  into a list `Select` builds itself, and left the closed control answering to the arrows alone —
  which is where a value is usually changed, since a two-choice picker is not worth opening. Both
  states are now `SelectKeys`, which also takes the popup half out of `StraumrStyles`, where a
  key handler had no business being.
- 2026-09-15: `Ctrl+Tab` steps focus backwards, as a global command so it means the same thing on
  every screen and in every dialog. The framework handles `Shift+Tab` itself and exposes no way to
  ask for the same move, so `FocusScope.FocusPrevious` walks the window holding focus — the active
  modal when one is focused, the screen otherwise — collects what traversal would collect, and
  steps back through it. It is unpresented for the reason `Ctrl+Enter` is: many terminals send
  `Ctrl` with `Tab` as a bare `Tab`, and several keep the combination for their own tab switching
  and never pass it on. Where it does not arrive, `Shift+Tab` is unaffected.

- 2026-09-15: Fixed a crash the developer hit on a multipart part: choose File or Text, press a key,
  and the app died with `Cannot read and then write FormTextBox.IsVisible within a same tracking
  context`. One update pass may not both read and write the same bindable value, and the pair
  dialog had arranged exactly that — the value box and the file picker swapped on a bound
  `IsVisible`, while the text fields inside them compute their own `IsTabStop` from their ancestors'
  visibility, which was the day's earlier fix for `Tab` reaching hidden pages. The dialog now
  assigns those two visibilities outright, the same rule the editor's pages already follow, and
  `FocusScope.IsReachable` starts its walk at the parent: a visual's own visibility is the half tab
  traversal already tests, so reading it was redundant and only created a property that was both
  read and written. Nothing in the app now binds the visibility of a subtree that holds a
  focusable; an audit of the remaining bound `IsVisible` calls found only text.

- 2026-09-15: `a` on a header, parameter or multipart part opened the pair dialog with the entry
  already named `a`, and `e` appended an `e` to the name it was editing. The third time this
  framework behaviour has bitten — a printable keystroke arrives as a key event and an independent
  text event, and handling the key does not suppress the text — and the first two times were fixed
  by arming the field that takes focus with the keystroke to discard. `KeyValuePairDialog` already
  had the `PendingEcho` property for it; `KeyValueField` simply never set it. The command helper now
  hands its own letter to the action it runs, so an action that opens something typed into has the
  keystroke without naming its gesture twice, and forgetting it again means leaving an unused
  parameter rather than writing nothing. Activation by `Enter` or a double-click passes none, since
  neither types anything.

- 2026-09-15: Coming back from the editor now lands on the field the reader left, not on the
  page's entry point. The view is shown again on the app that follows the external program, and a
  view being shown asks for focus the way it does when it first opens — which is the first field
  that applies, so editing a body handed focus back to the type above it. `Suspend` now remembers
  the focused visual and `FocusPage` prefers it: holding a visual across the two apps is safe, and
  only asking it for focus between them is not. The developer confirmed the rest of the editor
  works.

- 2026-09-15: Three from the developer, all about where the caret and the eye go. A field entered
  by `Tab` now has its caret after its value instead of in front of it. The first attempt did this
  from `OnDocumentChanged` and did nothing at all, which the developer's next screenshot showed:
  `TextBox.Text` is a plain bindable property that the document reads through to on demand, so the
  document raises a change for text the reader typed and none for text the form assigned — there is
  no event to hang it off. It is therefore done at the point of writing, by `FormTextBox.SetText`,
  with every field in the app filled through that instead of through `Text`. A pointer click still
  places the caret where it was clicked, and a field the reader returns to holds the place they
  left. The label beside the focused field now fills with the focus chip — blue ground, bright text
  — on the developer's reading that the field being written into is worth saying loudly. It is the
  one place in the app where two chips show at once: the page title on the rule names the page and
  the label names the field inside it, which are different questions at different levels. Nothing
  moves as a label lights, because the label column is already the longest label plus the gap and
  that is exactly what the chip's cell of fill on each side needs.
  And the caret position now applies to a body that already exists, not only to a scaffold: it
  opens at the end of the line the reader would carry on from, which for JSON is the line above the
  closing brace, since the end of that brace is the one place in the document nothing can be added.

- 2026-09-15: Opened the editor with the caret where the writing starts, as far as that can be
  asked for. It cannot be asked for universally — nothing in the `EDITOR` convention covers a
  position, so every editor that can be told has invented a flag of its own and the rest cannot be
  told at all. `ExternalEditor` therefore carries a table of the documented syntaxes: `+L,C` for
  nano, `+L` for the vim family, `+L:C` for emacs, micro and kakoune, `-l`/`-c` for kate,
  `-n`/`-c` for Notepad++, and the position on the path itself for helix, Sublime and VS Code's
  `--goto`. An editor not in the table is opened with no extra argument at all, because an argument
  it does not understand is either a second file it would create or an error it would exit on; it
  opens at the top, which is where it would have opened anyway. The position only ever applies to a
  scaffold — line 2, column 3 for JSON, under the declaration for XML — since an existing body opens
  at its first character, which needs nothing said about it. The caret travels with the text as
  `EditorDocument`, so the shape is there for the CLI's own editor flows to use later.

- 2026-09-15: Gave the external editor something to open on. An empty file is a poor thing to be
  handed, so a body that does not exist yet arrives as the document it is about to become: `{`, an
  indented blank line, `}` for JSON, and the declaration line for XML. A body that already exists
  arrives as it is, with one exception — JSON kept on a single line is laid out over lines first,
  because a one-line body is one a program wrote and opening an editor is the moment to make it
  readable; a body that already has lines is never reformatted, since its layout is then someone's
  decision. Opening a document and closing it again is not an edit either way: what comes back
  identical to what went out leaves the field holding exactly what it held, so looking at an empty
  body does not give the request a `{}` nobody asked for. The JSON formatter behind it is now one
  method, `RequestEditingHelpers.TryFormatJson`, with the response pane's beautify/minify toggle
  and the CLI's `--beautify` repointed at it rather than a third copy of the same six lines.
  Debug, Release and CLI-only builds pass without warnings and CLI `send --help` renders.

- 2026-09-15: Fixed two bugs the developer found on the Body page, both about hidden things.
  The page went on saying the request sent no body after JSON had been chosen for it: a field's
  `Visible` reads the state being edited, which is a plain object, so the function bound over it
  was evaluated once when the form was built and never again — nothing it read is in the binding
  graph. `EditorForm.Sync` now assigns each field's visibility outright, from the update pass and
  whenever a field changes, which is what `PagedPane` already does with its pages. The second was
  stranger to see: a blinking caret in the middle of the Body page that accepted typing nothing
  showed. Tab traversal tests a candidate's own `IsVisible` and not its ancestors' — a rule this
  app already knew and `FocusScope.IsReachable` already answers, but only `ResourceList` and
  `ScrollableContent` were asking it. A framework `TextBox`, `Select` or `Switch` used raw is a
  Tab stop wherever it sits, so `Tab` on the Body page walked into the hidden Request page and
  landed on Name, which lays out over the `Type` row, and on URL, which lays out just under
  `Content`. `FormTextBox`, `ChoiceField` and `ToggleField` now compute `IsTabStop` from the whole
  ancestor chain. That second bug is very likely the original report of invisible body text as
  well: the caret was never in the body at all, it was in the URL box of a page that was not
  being drawn. The body still moved to `$EDITOR`, which was a direction and not a repair.

- 2026-09-15: Gave the body to `$EDITOR` and the dropdowns the movement keys, both from the
  developer's report of the editor in a terminal: the method list could not be moved through
  with `j`/`k`, and the text of a JSON body being typed into was invisible while the caret moved
  through it. The body's answer is not a repair. Writing a body in the reader's own editor is
  what this app is for — the editor they have already highlights JSON, indents it and closes its
  brackets and quotes, which is the third thing they asked for and not something worth
  reimplementing here — so the framework's `CodeEditor` is out of the form altogether, with
  `JsonLineHighlighter` and the palette entries that served it. `ContentField` now shows the
  document as the request and response previews show one, a scrollable list of styled lines that
  `j`/`k`/`g`/`G` move through, and `Enter` or `Ctrl+E` opens it in `$EDITOR` under the extension
  its content type implies — `.json`, `.xml`, `.txt` — so the editor knows what it was handed.
  What comes back is taken byte for byte, a trailing newline included, because that is a real
  change to a body that will be sent as it stands. Running another program means giving up the
  terminal, so the field asks rather than launches: an `ExternalContentEdit` travels out to the
  screen, which suspends the view, runs the edit through the same external-action path `e` on a
  workspace already uses, and shows the view again on the app that follows — a dialog stays
  parented to the app that showed it, so one that is not closed first is refused by the next
  run. The reader comes back to the Body page with everything they had typed still on it, and a
  failed edit is reported on the editor's own footer rather than on the shell's line behind it.
  A body can no longer be written without `EDITOR` set; the field says so, and the CLI has
  always required it for the same work. The dropdowns were the other half: `Select` builds its
  popup list itself and exposes it only to `SelectStyle.PopupTemplateFactory`, so the style now
  attaches `j`/`k`/`g`/`G` there, handled from the key event for the same reason every other
  list in the app handles them there, and framed by the framework's own factory afterwards.
  Every dropdown in the app gets them, not just the method list. Debug, Release and CLI-only
  builds pass without warnings. None of it has been seen in a terminal: the suspend and resume
  around `$EDITOR` and the keys inside an open dropdown are the two things to check first.

- 2026-09-15: Fixed the editor firing commands while a field was being typed into, reported by
  the developer on the name box. `PagedPane` registered its page-cycling `t` on `Root`, which is
  an ancestor of every field on every page; commands are collected up the focus chain and run
  before the focused control sees the key, so typing a name containing a `t` changed page. In
  its editor form the gesture is now `Ctrl+T` as a control byte. An audit of every other
  ancestor between a field and the window root found nothing else: the framework's
  `ScrollViewer`, `Select`, `Switch` and `Dialog` register no commands of their own, and the
  editor's `Escape` and `Ctrl+S` are not printable. The general rule is recorded in
  [decisions.md](./decisions.md) — it is the second time this has come up, after
  `BrowserDialog`'s `n`/`r`/`d` firing over its path editor. Still open and worth a check: the
  app's global Ctrl+C interrupt is ungated and a text field binds Ctrl+C to Copy, so which one
  wins inside the editor is unverified; it matters more now that a form can hold unsaved work.

- 2026-09-15: Added the resource editor, and with it the shared kit every editable resource
  will use. `c` creates a request, `e` edits one, `y` copies one, all through one full-screen
  form built from the shell's own pieces: the header naming the request, a three-row bar
  carrying the live method and URL opposite an unsaved marker, and Request / Headers / Params
  / Body notched into the rule that closes it. `Tab` moves between fields, `t` and the titles
  change page, `Ctrl+S` saves and `Escape` closes, asking first when there are edits to lose.
  The kit is `Visuals/Shared/Editor/`: `EditorField` and its kinds — text, secret text,
  choice, toggle, key-value map, document, and a message for a page a discriminator has
  emptied — plus `EditorForm` for one page of them, `KeyValuePairDialog` for adding a pair,
  and `ResourceEditorView` for the screen. `Screens/Request/RequestEditor.cs` is the whole of
  what a resource has to supply, and is the size an auth or a secret will be. Headers,
  parameters, form fields and multipart parts are one `KeyValueField` over a `ResourceList`,
  so selection, hover, scrolling and `j`/`k`/`g`/`G` arrive with the list. A multipart part
  can be text or a file, chosen through the existing `FileBrowserDialog` and stored as the
  `@path` Core already reads; a part whose file has gone reads red as any unusable resource
  does. The body is the framework's `CodeEditor` with a JSON line highlighter written against
  the palette, and each body type keeps its own content, so choosing XML to look at it no
  longer overwrites the JSON. Choosing a type writes `Content-Type`, and the Headers page
  re-reads it. `:json` keeps the external editor reachable for a request that parses; `e` on
  a broken one still opens the text to repair, unchanged. Extracted along the way:
  `PagedPane` now owns the rule-as-tab-strip idiom that was welded into `PreviewPane`, with
  `Tab`-cycles-pages as a parameter; `FormTextBox` is the one echo-discarding text field the
  three dialogs were each declaring privately; `ConfirmDialog` is what `WorkspaceDeleteDialog`
  now is; and `RequestEditingHelpers` gained the URL check, the method list, the body-type
  list and the `Content-Type` mapping the CLI had privately, with the CLI repointed at them so
  the two cannot drift. Debug and Release solution builds and the Release CLI-only build pass
  without warnings, and CLI `--help` still runs. Nothing here has been seen in a terminal yet:
  the layout at real sizes, `Ctrl+S` surviving flow control, the dropdown popups, the body
  editor and the file picker all need the developer's check.

- 2026-09-15: Screen navigation no longer participates in unique-prefix resolution.
  `workspace` / `ws` and `request` / `rq` are the only accepted navigation spellings,
  so `:w` in Requests now reports an unknown command instead of opening Workspaces.
  Prefix matching remains enabled for ordinary commands. A focused resolver probe
  confirmed `w` and `wo` do not resolve while `ws` and `workspace` do; Debug, Release,
  and CLI-isolated builds pass without warnings.

- 2026-09-14: Cross-screen Send now returns to its caller. `send` declares that it opens
  a transient full-screen child; when a noun namespace navigates to run it, the shell
  pushes the source screen onto a return stack. Closing the response emits the screen's
  generic transient-close event, which reloads and restores that source. There is no
  Workspaces constant in the return path, so later screens and nested transient views can
  use the same contract. Request identifiers are now parsed as one command argument:
  whitespace requires double quotes, completion inserts those quotes automatically (and
  matches a partially typed opening quote), and malformed quoting reports through the
  footer. Core create/save rejects request names containing a double quote; copy reaches
  the same check through create. Focused probes pass for quoted parsing, missing-quote and
  unquoted-space errors, automatic completion quoting, and Core name rejection. Debug,
  Release, and CLI-isolated builds pass without warnings; terminal acceptance remains.

- 2026-09-14: Added typed cross-screen command namespaces. `:ws use <workspace>` and
  `:workspace use <workspace>` now load Workspaces invisibly, run its existing `use`,
  and reload the still-visible Requests screen; `:rq send <request>` and
  `:request send <request>` do the
  reverse through Requests' new direct `:send <request>`. The canonical navigation
  commands are singular — `:workspace` and `:request` — with `:ws` and `:rq` as their
  aliases; the plural spellings are gone. The former noun-based item selection commands
  are now `:select <name>`, retaining `:w` and `:r`. Namespace completion delegates to the destination
  command table. A workspace-context change reloads every hidden screen before the next
  input, so nested `use` completes valid workspace names/IDs and nested `send` completes
  current request names even when their screens have not been visited. A no-workspace send reports a
  footer error, and post-navigation focus waits while the response modal is open so the
  Requests list cannot steal it. Debug, Release, and CLI-isolated solution builds pass
  without warnings; terminal acceptance remains.

- 2026-09-14: `Tab` now changes page in the full-screen response, and `Shift+Tab` steps
  back, because `Tab` already means "move the chip along the rule" and that rule carries
  the pages here; a command claims the key before focus traversal reaches it, and nothing
  else on the rule can be stepped to. `t` still moves pages too and the hint names both, as
  `Tab /t Next tab`: a bar renders one gesture per hint and renders a gestureless hint not at
  all, so the second key rides in the presented hint's label, painted in the bar's key colour
  through the label markup a `CommandBar` already parses. The inline pane keeps `t`
  as its own hint, since `Tab` there belongs to the screen's regions. To
  make room for the hint, the full-screen pane builds its `ScrollableContent` with
  `hints: false`: the movement commands are advertisements only — `OnKeyDown` handles the
  keys — so `j`/`k`/`g`/`G`, the arrows, Home/End, Page and the wheel all still work while
  the footer reads `b Beautify / minify · y Copy body · Escape Back · s Send again ·
  Tab /t Next tab`. That is the same trade the resize keys already make. Verified on a running
  app: `Tab` moved the selected page and focus followed it, `Shift+Tab` came back, and the
  Requests screen's own snapshots are unchanged. Debug and Release solution builds pass.

- 2026-09-14: Three corrections to the full-screen response from the developer's review of
  the cohesion pass. It now opens with the Body page focused — `AutoFocus` is not enough,
  because the dialog takes the focus pass that follows its own `Show`, so the view asks for
  focus once it has an app to ask, which leaves a titled region lit from the first frame
  instead of none. The header names the request alone, `{straumr} · categorize`: the kind of
  view is already evident from what is on screen, and the request is what the reader has to
  keep track of, so `StraumrHeader`'s overload now takes one name rather than a screen and a
  crumb. And `s` sends the request again without leaving the view, the same letter the list
  sends with: it queues through `UpdateAsync` where every other Core call runs, stands down
  while a send is already in flight, and puts the view back in its in-flight state. Escape
  cancels a re-send from a cached view too, which it could not before, since that path had
  been given a cancel that did nothing. A running in-memory app confirms the Body page holds
  focus when the view opens, the footer reading `b Beautify / minify · y Copy body ·
  Escape Back · s Send again` at 120 columns, and the bar and footer returning to
  `IN FLIGHT` and `Escape Cancel` on a re-send. Debug and Release solution builds pass
  without warnings. The send itself is not driven headlessly and still wants a terminal.

- 2026-09-14: Made the full-screen response read as a screen of the app rather than as a
  dialog over one, from the developer's report that it works but does not feel cohesive.
  It now carries the shell's own identity header — `{straumr} · response · <request>` on
  the left, the active workspace on the right — a three-row summary bar holding the method
  and URL opposite either the in-flight pulse or the finished measurements, and its Body,
  Headers and Network titles notched into the rule that closes that bar, the selected one
  carrying the focus chip. That replaces a `Response` title stacked over a separate tab
  strip, which named the region twice and lit two focus cues at once. The dialog also lost
  its frame title and its padding, so its rules run to the frame as the shell's do; the bar
  keeps its three rows when a send finishes instead of collapsing the activity row and
  shifting the screen under the reader; a transport failure is reported once, short in the
  bar and in full on the pages, instead of twice; the Network page uses the `Key: value`
  formatting the Headers page uses; and the footer notice lives as long as the shell's
  messages do. The inline response's third tab was renamed from `Details` to `Network` so
  both surfaces name the same page the same way, and the method token now keeps its width
  in both summary bars rather than being the first thing trimmed. New shared pieces:
  `PreviewPane.OnRule`, `StraumrDialog.CreateScreen`, a `StraumrHeader` overload taking the
  screen name and a crumb, `StraumrSurfaces.RowInset` and a public `ResourceScreenLayout.PaneInset`.
  Debug solution build passes without warnings; Workspaces and the shared list snapshots are
  byte-identical to before the change, and the response view was captured at 120x30, 80x24
  and 44x14 while sending, on success and on a transport failure. The focus chip, clicking a
  title and the animation need the developer's terminal.

- 2026-09-14: Restored the initial full-screen response appearance at the developer's
  request: compact pulse and live elapsed label above the response tabs. Removed the
  separate orbital loading scene, transfer dashboard and its streaming-progress API.
  Kept the timer fix, response inspection, formatting, clipboard and cancellation.

- 2026-09-14: Fixed the frozen in-flight duration reported by the developer. A plain
  Stopwatch read is not a reactive dependency, and the screen update awaits sending.
  The label now uses the framework animation scheduler at 100 ms intervals to update
  its retained TextBlock on the UI thread, with no background timer. A running
  in-memory TerminalApp verified the text advancing during an awaited callback and
  remaining unchanged after the clock stopped. Debug and Release builds pass.

- 2026-09-14: Replaced the Requests send spinner dialog with a viewport-sized response
  view that stays open on success, HTTP/transport errors, cancellation and timeout.
  A shared-style pulse and elapsed counter animate while sending. Body, Headers and
  Network tabs expose full wrapped content and measured header/body timings, bytes,
  HTTP version and body read rate. `v` expands a cached response without resending;
  `b` beautifies/minifies JSON and `y` copies the full currently formatted body in
  either response surface. Invalid JSON and unavailable clipboard access report
  errors. Debug/Release and CLI-only builds pass, as do local formatting/metrics
  checks and layout captures at 120x28, 80x24 and 44x14. Interactive acceptance pending.

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
