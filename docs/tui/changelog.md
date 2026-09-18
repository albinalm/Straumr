# Change Log

Part of the [TUI implementation guide](./README.md). Newest first. History only —
nothing here is a rule. Read the most recent entries when resuming work.

- 2026-09-18: Added syntax colouring to the response body. A body that parses as JSON is drawn as
  one `Paragraph` per line carrying a `StyledRun` per token — key, string, number, boolean, null,
  punctuation, and a plain run for everything between — instead of one uncoloured `TextBlock`.
  `PreviewPane` gained an optional per-page highlighter, so a page is coloured only where a caller
  asks for one and every other page is drawn exactly as before. `JsonHighlighting` is the tokenizer:
  it works a line at a time, which is sound because a JSON string cannot contain a raw newline, and
  tolerates text it does not recognise rather than refusing it, since a bounded preview may be cut
  mid-document. The colours are a `[code]` table in the theme — six roles, optional, inheriting the
  semantic roles nearest each meaning the way `[methods]` does — and both built-ins name one: the
  terminal theme in palette slots, straumr in the palette it already has. `h` on the Body page
  toggles, `[response] highlight` decides what a body arrives with, and `[response] highlight-limit`
  is the size in KiB past which it arrives off, so a large body does not pay for colouring nobody
  asked for; `h` overrides the limit and says what it may cost. Colouring is gated on the body
  actually being JSON, so `h` on anything else says so rather than colouring words at random.
  `ResponseBodyActions` now takes one `ResponseBodyOptions` from the settings rather than a format
  alone. Debug and Release builds pass without warnings; the terminal check is pending.

- 2026-09-18: Added `[response] format` to `settings.toml`: `none` (the default), `beautify` or
  `minify`, deciding what happens to a JSON response body the moment it arrives, on the inline
  Response pane and in the full-screen view alike. The setting is read at the moment the body is
  set, so `:settings` takes effect on the next response without a restart, and the formatted body is
  what `y` copies and what `b` toggles away from. A body that is not JSON is shown as sent and
  nothing is reported; `b` still says so when pressed by hand. A value that is none of the three
  falls back to `none` and is reported in the footer like any other settings problem. `StraumrSettings`
  gained a `[response]` table and `IStraumrSettingsService` a resolved `ResponseBodyFormat`. Debug
  and Release builds pass without warnings; the terminal check is pending.

- 2026-09-18: Fixed the full-screen views keeping the size of the terminal they opened in. The
  send/response view and the resource editor bind their width and height to the viewport, and that
  binding read `TerminalApp.Terminal.Size`, which is not bindable: the framework had no reason to
  re-evaluate it, so resizing the window left the view painted at its old size over a shell that had
  already re-laid itself out. The size now comes from `TerminalViewport`, which reads the app root's
  bindable `Bounds` — the size the framework arranges its root to inside the render frame. The first
  attempt wrote the size into state from the shell's update pass and fixed only the finished view:
  that pass is awaiting the send, so a resize while the request was in flight still did nothing until
  the response arrived. The folder browser and the extraction help dialog clamped their height the
  same way and now reclamp for real on a resize. Debug and Release builds pass without warnings; the
  terminal check is pending.

- 2026-09-18: The custom-theme default is now `~/.straumr/themes`: `:theme oxblood` resolves
  `~/.straumr/themes/oxblood.toml`, with both the directory and extension inferred. Explicit
  absolute and `~` paths still work, and settings-directory-relative references such as
  `themes/oxblood.toml` remain compatible. The settings template and theme guide now show the bare
  form. Debug and Release builds pass without warnings.

- 2026-09-18: Renamed the fixed dark-blue built-in theme from `deepocean` to `straumr` throughout
  lookup, completion, export, the generated settings template and documentation. Its embedded TOML
  display name is now `Straumr`, so `:theme straumr` reports the same name it was selected by and
  `:theme export straumr` writes `straumr.toml`. There is no compatibility alias during early access.

- 2026-09-18: Confirmation dialogs can now be browsed with any arrow key as well as `Tab` and
  `Shift+Tab`. They still open safely on Cancel, and arrows only move focus — `Enter` activates the
  focused answer. The folder browser's hand-built delete confirmation was replaced by the shared
  `ConfirmDialog`, so workspace, request, auth, secret, editor and folder-delete confirmations all
  behave the same way.

- 2026-09-16: The developer confirmed the outer-panel resize fix works. The resize hint now
  includes every available direction in one entry: `Ctrl+H Ctrl+J Ctrl+K Ctrl+L Resize` for
  stacked detail panes, and `Ctrl+H Ctrl+L Resize` otherwise. Both variants use the same
  availability checks as the resize actions and disappear while the filter is focused.

- 2026-09-16: Fixed the leftmost panel refusing to shrink across resource screens. The outer
  list/detail grid now wraps both panels in the existing `FlexiblePane`, as the inner detail
  panes already did. Before the fix, a 120-column layout kept the list at 53 columns for shares
  from 15% through 35%; afterwards it follows those shares, from 18 to 42 columns. The resize
  guard now uses `Owns()` for the filter, since `HasFocusWithin` excludes the editor itself.
  Debug and Release builds pass without warnings; 144 shared-layout geometry checks cover
  four screen labels, empty/populated lists, three widths and six shares. Real key input
  remains a terminal check. `Ctrl+J/K` still applies only to stacked detail panes.

- 2026-09-16: The pane-resize hint names both keys. The footer advertised `Ctrl+L Resize panes` and
  nothing else, so the row read as a key that only grows a pane — the developer's report. All four
  Vim-direction keys were already bound; only one was presented. The hint now carries `Ctrl+H` as its
  keycap and `Ctrl+L` in its label, the same two-keys-one-label shape the tab hint uses, so the row
  says the divider moves both ways. The stacked pair stays silent. A key named in a label is bold
  now as well as coloured — the bar draws its own keycaps bold, and the pair read as two different
  things side by side; every other alternate key matches its keycap with it. The slash those keys
  carried is gone with it — `Enter e Edit`, `Tab t Next tab`, `h F1 Help` — because a hint that now
  styles its second key the way the bar styles the first does not also need a separator to say the
  key is a key, and two hint shapes for one idea is not a design language.

- 2026-09-16: Stored resources are JSONC, and comments in them survive. Workspace manifests,
  requests, auths and secrets now accept `//` and `/* */` comments and trailing commas, and the
  files carry `.jsonc` so an editor knows it. Keeping a comment is the whole point and was the hard
  part: reading a resource stamps `LastAccessed`, so anything the app merely tolerated would have
  been erased on the next keystroke elsewhere. `JsoncComments` lifts the comments off the old text,
  anchors each to the member it sits against, and puts them back after serialisation, so a comment
  outlives every stamp, send and save. The screens' `:json` editors hand over the file's own text
  rather than a re-serialisation, and hand it back through `CarryCommentsFrom`, so a comment written
  inside the editor session survives too. No migration: existing `.json` resources are stale, as the
  early-access decision below already provides for.

- 2026-09-16: Fixed focused buttons picking up the colour of whatever was behind the dialog. The
  inverted selection surface carried no foreground of its own, and inversion swaps whatever colour a
  cell already holds: a dialog's surface fill sets a background but leaves foregrounds alone, so
  Browse rendered green over a "last used" line, Create grey, and Cancel correctly because nothing
  lay under it. A foreground cannot be reset to the terminal's own — `Color.Default` is the same
  value as "unset" and overwrites nothing — so the inverted surface now pins one. Proved by drawing a
  focused button over three different backdrops and getting the same cells back each time.

- 2026-09-16: `:theme <name>` now changes the theme, and buttons got their colour back. The theme
  command sets as well as reports: it writes the one key into `settings.toml` through the TOML
  syntax tree, which round-trips the document exactly, so every comment survives — and then rebuilds
  the shell the way saving the file by hand already did. The shell restarts on a palette change even
  when no external editor was involved, which the loop previously only noticed after an external
  action. `:theme` on its own reports the applied theme and nothing else; naming the file to edit was
  documentation living in a footer that expires after five seconds.
  The fix behind all this: `text-bright` and `muted-bright` were `default` in the terminal theme, to
  keep the inverted selection band one tone. Those roles are also every button label, the dropdown,
  the text in a field and the badge, so all of them lost their foreground entirely and rendered as
  bare `[bold]` — which is what a stray grey character in a dialog turned out to be. Both are real
  colours again.

- 2026-09-16: The create/copy workspace form says "No default location" and stops there. It used to
  add "Choose one with Browse.", which named a button sitting two rows above it on the same dialog —
  a sentence spent telling the reader something the screen was already showing them.

- 2026-09-16: Split configuration from state properly. `options.json` is now `state.json` — the old
  name was a synonym of "settings" and told nobody which of the two files held the workspace
  registry — and `StraumrOptions`, its service and its `Options` property became `StraumrState` to
  match. The old file is read once if the new one is absent, rewritten under the new name and
  removed. The two keys in it that were never state, the default workspace and secret paths, are
  `[paths]` in `settings.toml` now, expanded for `~` and environment variables through a shared
  helper the theme resolver also uses. What is left in `state.json` is only what the program itself
  writes. `straumr config workspace-path` reports the value and names the file to set it in rather
  than writing it, since writing would strip the comments out of a hand-edited file. No migration:
  this is early access, so the rename is a rename and an existing `options.json` is stale rather
  than read. Writing the migration did surface a real gap, which is fixed — a settings file that
  would not parse, or a theme that would not resolve, used to start the app silently in the default
  colours, because the host applied the theme before there was a footer to complain on.

- 2026-09-16: A theme file now carries only what it changes. Every colour role became optional and
  is inherited from the terminal theme, which is the one palette whose roles all defer to the
  reader's scheme and so the only one it is safe to land on unasked; the terminal theme itself
  stays the file that names them all. A whole theme can now be three lines. No `extends` key —
  starting from the dark palette is `:theme export straumr` and editing the copy, which is also
  what gives export a job. Straumr now names its own `brand` and `[methods]` rather than
  inheriting them, as any theme fixing its own background must, and still renders identically. A
  theme that names a background but leaves `text` inherited now says so on the footer, since that
  is the one way the new default can leave a reader with text they cannot see. `:theme` also stops
  reporting a stale name when a new theme happens to carry the same colours as the old one.

- 2026-09-16: Gave the identity mark and the HTTP methods colours of their own. `accent` had been
  doing three jobs — the footer's key colour, the `{straumr}` wordmark and POST — so making it
  colourless for the chrome took the other two with it and the default theme came out white. There
  is now a `brand` role and a `[methods]` table, both optional and both falling back to exactly what
  the app did before them, which is why Straumr declares neither and still renders identically.
  The terminal theme declares both, in palette slots: green, blue, yellow, magenta and red for the
  methods and cyan for the wordmark, so every colour is the reader's own rather than one chosen
  here. The chrome stays colourless; the rule is now about what a colour is for rather than how
  much of it there is.

- 2026-09-16: Fixed the default theme painting a solid dark rectangle over a transparent terminal,
  and took the hue out of its chrome. The palette was right and the ground was wrong: the framework
  clears every cell it owns with its own theme background, `Theme.Default`'s is `#0f1d27`, and an
  earlier reading of the framework recorded that the theme could not be set at all — it can, through
  the generic `SetStyle(Theme.Key, theme)` rather than a property. `StraumrStyles.FrameworkTheme`
  now chooses `Theme.Terminal`, whose background and foreground are null, for any palette whose
  background is the terminal's own, and the shell applies it to `app.Root` so the layers holding
  dialogs and popups get it too. Verified by rendering the shell's ground: the terminal theme emits
  no background sequence on any cell. The terminal palette also lost its cyan accent and its blue
  bands. Chrome — keys, markers, rules, badges — is now the terminal's own foreground dimmed or
  brightened, the unfocused selection band is gone along with hover for the same reason (no tint of
  an unknown ground can be named), and the selected row of a focused list is now marked by
  inverting the terminal's own two colours rather than by any colour this repository chose —
  `selection = "invert"`, a theme value that is not a colour. That was the developer's question:
  the scheme cannot be read, but it can be swapped. The hues that remain are the ones that carry
  meaning. Straumr is
  unaffected: it keeps `Theme.Default` and its styles still dump identical to the pre-theming
  palette.

- 2026-09-16: Replaced the hardcoded blue palette with themes read from files, and made the
  terminal's own colours the default. `StraumrStyles` keeps the surface every screen already reads
  — all ~250 references are unchanged — but is now a facade over a `StraumrStyleSet` built from an
  18-role `StraumrPalette`. Two themes ship in the binary as TOML text: `terminal`, which paints
  with `Color.Default` and the terminal's sixteen palette slots so the app inherits whatever scheme
  the reader configured, and `straumr`, which is the previous palette exactly. A reader names one
  in `~/.straumr/settings.toml`, by built-in name or by a path to a theme file of their own; that
  file is opened in `$EDITOR` with `:settings` and read back when they close it. `:theme` reports
  what is applied and `:theme export <name>` writes a built-in out as a starting point. Changing
  the palette rebuilds the shell — framework styles are values handed to a control when it is
  built, so they cannot be swapped underneath one — which is why the shell and its screens became
  scoped rather than singleton registrations; the reader comes back to the screen they were on.
  Bad settings, an unresolvable theme, a missing or misspelled colour and an unparsable file all
  report on the footer and leave the previous palette standing, because a typo in a hand-edited
  dotfile must not be a refusal to start. A theme whose roles collide — accent equal to selection,
  say — still applies, and says which cue it has made invisible. Proved by dumping all 59 members
  of `StraumrStyles` before and after: `straumr` is byte-identical to the old palette. Debug,
  Release and CLI-only builds pass without warnings, and both projects are clean under the
  trim/AOT analysers. Terminal acceptance of the two themes is pending.

- 2026-09-16: A copy now says what it was copied from. Copying clears the name, and the reader had
  to leave the editor to go and read the original again. `ResourceEditorView` takes the source name
  and puts `Source <name>` on its bar beside the unsaved marker, so Requests, Auths and Secrets all
  show it, on every page of a multi-page editor and after the copy has been saved. The wording and
  the position follow the workspace copy dialog, which already carried a `Source` row. Only a copy
  passes a name — an edit and a create pass none and the bar is unchanged.

- 2026-09-16: Fixed focus falling off the screen after a delete. Confirming a delete on Secrets
  left no region titled and put Workspaces' keys — `Use`, `Import`, `Export` — on the footer:
  focus had not been lost but re-homed onto the hidden Workspaces list. Each screen claimed
  `AutoFocus` through a binding over `FocusScope.IsReachable`, which answers for the tree the
  visual is in; every reload swaps the list out for the loading message, and evaluated detached
  that binding walks no ancestors, answers true and registers nothing that can ever invalidate
  it, so a hidden screen's list became a standing claim on every stray focus in the app. The
  shell now assigns `AutoFocus` beside `IsVisible` when it shows or hides a screen, since which
  screen is on show is its own fact, and holds the invariant on every pass: focus that is not
  inside the visible screen, a modal or the prompt is put back on the screen's focus target.
  That covers the same loss on Requests, Auths and Workspaces, and after any operation that
  reloads a list, not deletion alone. Debug and Release builds pass without warnings; terminal
  acceptance is pending.

- 2026-09-16: Fixed the Secrets reference scan counting removed workspaces as unreadable.
  The developer reported four failures; read-only inspection found exactly four registry
  entries whose files were absent, alongside three readable workspaces. The scan now skips
  absent workspace files using the same rule as Workspaces. Existing corrupt workspaces and
  unreadable resources still warn. An isolated regression check covers stale-only entries
  producing no warning and a mixed scan preserving its actual corruption/resource warnings.

- 2026-09-16: Implemented S1, the global Secrets screen. It uses the shared resource layout,
  two-line list, filter, scrollable details and independent pane shares. The Secret pane masks
  values with a fixed-length mask and shows timestamps and storage; Known references names the
  request/auth, workspace and field, plus the literal placeholder. The index loads through Core
  without stamping access times and reports partial coverage instead of silently dropping errors.
  Broken and missing secrets remain visible for repair or deletion.
  Create, edit and copy use the shared editor's Name and masked Value fields, unsaved confirmation,
  save flash and repeated-save lifecycle. Delete confirms the number of directly referencing
  resources across registered workspaces. `Ctrl+E` / `:json` open the external editor, and `e`
  uses it for broken files; valid edits preserve the registered ID and go through Core.
  Every action is available through `:secret` / `:sc`, with quoted name completion and the
  existing transient return stack. All three earlier namespaces remain available on Secrets.
  Hidden screens are now primed on startup even with no active workspace, so global secret
  completion works there too. Core rejects blank and double-quoted secret names on create/save.
  Debug, Release and Release CLI-only builds pass without warnings and CLI secret help renders.
  `.tmp/s1-check` verifies masked snapshots, junctions/amber, non-stamping inspection, reference
  matching across workspaces, partial coverage, quoted completion, repeated save and missing-entry
  deletion against isolated storage. No input harness; terminal acceptance remains pending.

- 2026-09-16: Every action each screen offers under a key is now a typed command in that
  screen's namespace, so all of them cross a screen boundary rather than the four that
  happened to have been needed. Requests gained `:create`/`:new`, `:edit`, `:copy`,
  `:delete` and `:view`/`:response`; Auths gained the same four; Workspaces gained
  `:create`, `:edit`, `:copy`, `:delete`, `:import`, `:export` and `activate` as an alias
  for `use`. The verbs are the CLI's own, so `:rq copy` and `straumr request copy` are one
  vocabulary. Each takes its subject by name and acts on the selection when given none,
  which is what the key does, and each answers
  `no active workspace; use :ws use <workspace> to choose one` when there is none — the
  one wording, on every screen, where before a screen with no workspace answered
  `no request matches x` or did nothing at all. `:refresh` says it too rather than
  reporting that it reloaded nothing. Those messages now survive: an editor path that
  raised a notification while a command was running had it wiped by the result the command
  returned, so `EditAsJson` and the workspace edit answer their caller instead, and the
  keys that call them report for themselves.
  Two things had to change underneath. The return stack — what brings a reader back to the
  screen they typed on — was pushed by a flag on the command, which was a guess the moment
  `:rq edit` could open either the form or the external editor depending on whether the
  request reads. The screen now says what it actually did, through `TransientScreenOpened`
  raised beside the `Show` that opened the surface, so every push has a close to pop it and
  `OpensTransientScreen` is gone. And a dialog opened by a command is shown in the middle
  of the update pass that restores focus after navigating, while `AutoFocus` does not place
  focus until the render after that: `ConfirmDialog`, `WorkspaceFormDialog` and
  `BrowserDialog` now take focus in their own `Show`, as the full-screen views already did,
  and the shell asks the window layer rather than only the focus chain whether a modal is
  up. Workspaces raises neither transient event on purpose — its dialogs are not screens —
  so `:ws delete` from Requests leaves the reader on Workspaces, looking at what it did,
  while `:rq edit` from Auths comes back to Auths when the editor closes.
  Debug, Release and Release CLI-only builds pass with no warnings. The developer then
  took the set to a terminal, reported that it works, and accepted A1 with it, which
  completes the Auths milestone. Their pass did not single out the no-workspace wording,
  completion inside the new namespaces, or a dialog opened across a screen boundary
  holding the keyboard, so those stay on the checklist.

- 2026-09-16: The Extract page got help on `h`. The page asks for one expression whose meaning
  changes completely with the source chosen above it — `data.token` walks a JSON body, `X-Auth-Token`
  names a header, `"token":"([^"]+)"` is matched against the body as text — and the field's
  placeholder has room for one example of the three. The dialog gives each source what it does, how
  its expression is written, and worked examples: three for the JSON path, which is the one most
  likely to be guessed wrong. It is deliberately not JSONPath — a dotted walk with bare numbers
  indexing arrays, no `$`, no bracketed index, no wildcards — so the help says that outright rather
  than leaving a reader to try `$.access_token` and read a "property not found" back.
  It takes two keys for one action, which needs explaining. Every letter is a character a focused
  field swallows, which is the rule the editor's `Ctrl+S` was built around; a bare `h` therefore
  reaches the help from the Source dropdown and from nowhere else on a page whose other three fields
  are text boxes — which is exactly where a reader wondering about the syntax is standing. `F1` is
  not a character and works in all four. `Ctrl+H` looks like the obvious pairing and is unusable: a
  terminal sends it as the C0 byte for Backspace, so it would delete a character rather than explain
  one. The two share one hint, `h /F1 Help`, the shape `Enter /e Edit` already has. Both commands sit
  on the Extract page's own root rather than on the editor, so they are offered on that page alone —
  every page stays attached, and commands are collected from the focus chain.
  The text is written against `StraumrAuthService`'s three extractors and says what they actually do,
  including the parts that would otherwise be found out by failing: the header lookup falls through to
  the content headers and ignores case, and the regex takes the first match, preferring group 1 when
  the pattern has one and the whole match when it does not. Section titles come from
  `ExtractionSourceDisplayName`, so the help and the dropdown cannot drift apart. A layout capture
  confirms the columns and the wrapping; Debug, Release and CLI-only builds pass without warnings.
  The keys themselves want a terminal.

- 2026-09-16: Added the Auths screen, A1. An auth answers four questions rather than two, so its
  detail panel has four regions instead of the pair every other screen uses: Configuration is how it
  is set up, Credential is what it is holding and what the next send will do with it, Secrets is
  every reference it makes with whether the store can supply it, and Used by is the requests that
  send with it. Nothing on the screen prints credential material. A value that is a secret reference
  is named, because the name is the point of using one and is not itself the secret; everything else
  reads `set` or `not set`. That a token is not shown needs no line of its own, which is the rule the
  Requests screen's Authentication pane already follows.
  Status is said in colour rather than in a word of its own — amber when the auth holds something it
  could authenticate with right now, inert grey when it does not, red for the reason the next send
  will fail. Green was not available: it belongs to the active workspace alone, so an expired token
  that will renew itself reads amber rather than green. The wording and the colour both come from
  `AuthFormatting`, which the Requests screen now uses for the same auth, so the two screens cannot
  describe one resource differently.
  `f` fetches an OAuth2 token or a custom auth's extracted value and saves the result, which is the
  difference between fetching here and a send fetching one for itself — an auth holds its token. It
  is registered on the list and on the container the detail regions share, so it works from wherever
  the reader is, the lesson `s` on Requests had to be taught twice. It is not offered for Bearer or
  Basic, which *are* the credential they carry. The editor's pages are decided by the type — Bearer
  and Basic have only Auth, OAuth2 adds Grant, Custom adds the four that describe its request — and a
  page that does not apply is neither titled nor reachable, which is the field-level discriminator
  one level up. Each shape is kept for the life of the form, so changing the type puts away what the
  old one held rather than throwing it out, the rule a body type already follows. `d` names how many
  requests still point at the auth, because deleting it does not change them, as deleting one from
  the CLI does not, and this screen is the only place that knows which they are.
  R1 predicted an auth would be a file the size of `RequestEditor.cs`. It is not: `AuthEditor.cs` is
  364 lines to that file's 193, and `AuthScreen.cs` is 1086 to `RequestScreen.cs`'s 916. The shared
  kit worked — no layout, scrolling, focus or selection was rewritten, and the refactors it needed
  are byte-identical to `HEAD` on Requests by snapshot diff — but four configuration shapes across
  two axes and four detail regions are simply more to describe than one request is. Core also now
  refuses a double quote in an auth name, as it already did for a request, so every auth name has an
  unambiguous quoted form in the prompt. Debug, Release and CLI-only builds pass with no warnings and
  CLI `create auth --help` still renders after the shared helpers moved. None of it has been seen in
  a terminal.

- 2026-09-16: `Enter` on a request opens the editor instead of moving focus into the Request pane.
  The developer called the old behavior weird and it was: activating a row is meant to do the thing
  the row is for, and this row's thing is the request. Focusing a read-only preview was never that,
  and it was work `Tab` already does — the preview sits beside the list, not behind it, so activation
  had nothing to open. `Enter` now runs the same `RequestEdit` as `e`, so a broken request opens as
  JSON to repair by either key, and the list's activate label went from `Inspect` to `Edit`. The
  first cut of this showed `Enter Edit` beside `e Edit` and the developer was right that one action
  should not take two hints: they now share one, `Enter /e Edit`, both keys in the bar's key colour.
  The bar renders exactly one keycap per hint and renders no hint at all for a command with no
  gesture, so the second key has to ride in the label as markup and its own command goes
  unpresented — the same shape `Tab /t Next tab` already had, and the reason `ActionCommand` now
  takes a presentation.

- 2026-09-16: `s` sends from anywhere in the Requests detail, not only from the list. The developer
  was reading a Response pane and pressing the key that sends did nothing, because the command sat
  on the list alone. It is now also on the container the detail regions share, which routing reaches
  from whichever pane or page holds focus; it is deliberately not on the screen root, where it would
  claim the `s` being typed into the filter. The empty Response pane's line changed with it: it said
  "No response yet. Press s on the request to send it.", which named a place the reader no longer has
  to go, and "yet" described a session rather than the pane, which shows what this request has
  stored. Every page now reads "No saved response.", with the Body page adding "Press s to send the
  request." Builds clean; the key itself wants a terminal press from each pane.

- 2026-09-15: Added deleting a request, which the screen had never had — the developer noticed it
  missing right after accepting the screen, and they were right: `c`, `e` and `y` were all there and
  the one key that removes anything was not. `d` asks through the shared `ConfirmDialog`, red and
  with Cancel holding focus, and the removal itself runs on the update pass where every other Core
  call on this screen runs. It is offered for a request that cannot be read as well, unlike Copy and
  Send: a file that does not parse is one a reader is more likely to want rid of, not less. The
  cached response goes with it, and the row below the deleted one takes its place — on the last row,
  the row above — which is what every other list in this app does.

- 2026-09-15: The developer accepted the Requests screen. R1 is complete; A1, the Auths screen, is
  next and not started. Worth knowing before it begins: almost everything R1 grew in its last day is
  shared, so an auth should be `RequestEditor.cs`-sized over the same kit — the editor's fields and
  pages, `PreviewPane` and `PagedPane`, the `$EDITOR` handoff with its scaffolds and caret, the
  secrets region, and `SecretFormatting`. Two decisions are worth re-reading rather than
  rediscovering: visibility that hides a focusable is assigned and never bound, and anything the
  chrome reads out of a plain state object is mirrored into a `State<T>` once per pass. Four checks
  outlive the acceptance and are still open in the checklist: `Ctrl+C` against a real in-flight send,
  the AOT binary's TUI in Alacritty, whether this terminal delivers `Ctrl+Tab`, and a populated
  round trip through the multipart body.

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
