# Status and Milestones

Part of the [TUI implementation guide](./README.md). Read this first in any TUI
session: it says what is done, what is active, and what must not be started yet.
Update it in the same change that completes or advances a milestone.

## Current Status

- The References field is pinned to the saved name — editing the name no longer rearranges it — and
  the rename dialog's detail is a single line. Debug and Release builds pass without warnings.

- The References field says nothing about renaming any more, and the rename dialog is two short
  lines. Debug and Release builds pass without warnings.

- The rename question is now two answers — `Rename and update` or `Cancel` — on both the form and
  the `Ctrl+E` / `:sc json` route, since renaming a secret and leaving its references broken is never
  what anyone means. The JSON route asks before writing, so cancelling leaves the edit unapplied.
  Reference entries are one row each, grouped under their workspace. Debug, Release and Release
  CLI-only builds pass without warnings; **needs a terminal check** — rename a referenced secret from
  the form and from `Ctrl+E`, take both answers on each.

- Renaming a secret now offers to take its references with it. Saving a rename whose old name is
  still stored in a request or auth asks — `Update references`, `Rename only`, `Escape` to abandon
  the save — and updating rewrites every stored `{{secret:old}}` to the new name through Core, which
  carries each file's comments, stamps `Modified` and clears that request's stored response. A
  change of case alone never asks. The rename through `Ctrl+E` / `:sc json` is deliberately not
  covered: it is the repair route, and it still renames without asking. Debug, Release and Release
  CLI-only builds pass without warnings and `.tmp/rename-check` passes 26 document-rewrite checks;
  **needs a terminal check** — rename a referenced secret and take each of the three answers.

- The secret editor's References row now shows the references themselves. It draws the same
  cross-workspace index the Secrets pane draws, through one shared `SecretReferenceView`: entries
  group under the workspace they live in with a count, and each names the resource with its kind and
  field beneath. Nothing found names what was searched — no request or auth in N scanned workspaces —
  and a scan that could read no workspace says that instead. The field follows the Name field as it
  is typed, so a create or a copy shows what already references the name being given it, and a
  rename empties the list and raises an amber line naming the references that stay on the old name.
  Debug, Release and Release CLI-only builds pass without warnings; **needs a terminal check** —
  open a referenced secret, a never-referenced one, and rename one without saving.

- A sent response is now kept in the request file, so the Response pane still shows it after a
  `:refresh` and after a restart. `StraumrRequest` carries a `LastResponse` block — the status and
  reason, the HTTP version, the measured timings, the body, its byte count, the response headers,
  any warnings, a transport failure's message and the moment it was sent — written after the send
  through `IStraumrRequestService.StoreResponseAsync`, which leaves `Modified` and every other field
  alone and is ordered last in the file so a large body cannot push the name and URL out of sight in
  an editor. `[response] store-limit` in `settings.toml` is the largest body worth keeping, in KiB,
  default 1024: a larger response is still kept, without its body, and the Body page says so and
  points at `s` rather than showing an empty one. `0` keeps nothing at all. Saving a request clears
  what was stored, the same way an edit already dropped the response held in memory, because the
  response describes the request as it was sent. A response already in memory is never replaced by
  the stored copy, so a refresh cannot downgrade what is on screen. The Network page now names when
  the response was sent, since a restored one is otherwise indistinguishable from one that has just
  landed. Debug, Release and Release CLI-only builds pass without warnings, and the file format was
  round-tripped through `StraumrJsonContext` and the comment carrier — including a body above the
  limit and a failed send; the terminal check is pending.

- A JSON response body is now coloured by token. Keys, strings, numbers, `true`/`false`, `null` and
  the punctuation between them each read in their own colour, taken from a new `[code]` table a
  theme may write and both built-ins do. It applies to the Body page of the inline Response pane and
  of the full-screen view, and only to a body that parses as JSON: anything else is left alone.
  `h` turns it on and off by hand. `[response] highlight` in `settings.toml` decides what a body
  arrives with (default on) and `[response] highlight-limit` the size in KiB past which it arrives
  off regardless (default 256), because colouring walks every line that is drawn and a body of
  several megabytes is felt through it. The limit measures the text the pane will draw rather than
  the body, so the inline pane — bounded to 64 KiB — keeps its colour on a response the full-screen
  view opens plain. `h` still turns it on past the limit and says what it may
  cost. An existing `settings.toml` does not gain the new commented block, since the template is
  only written when there is no file — the defaults are what an unwritten key means, so nothing
  changes until a key is added. Debug and Release builds pass without warnings, the theme
  parser was checked against both built-ins, a theme with no `[code]` table, an unknown key and a
  bad colour, and the developer confirmed a coloured body in the terminal.

- Response bodies can now be reformatted on arrival. `[response] format` in `settings.toml` takes
  `none` (default), `beautify` or `minify`, and applies to JSON bodies on both the inline Response
  pane and the full-screen view; anything else is shown as it was sent. `b` continues to toggle by
  hand from whatever the setting produced. An existing `settings.toml` does not gain the new
  commented block, since the template is only written when there is no file — the default is `none`,
  so nothing changes until the key is added. Debug and Release builds pass without warnings;
  **needs a terminal check** — set each of the three values and send a JSON request.
- Full-screen views no longer keep the size of the terminal they opened in. Resizing while the
  send/response view or a resource editor was open left it drawn at its old size over the re-laid
  shell, because their width and height bound to `TerminalApp.Terminal.Size`, which registers no
  reactive dependency. Both now read `TerminalViewport`, which answers from the app root's bindable
  `Bounds`, as do the folder browser and extraction help dialogs, whose height clamp had the same
  defect. The developer confirmed the finished view rescales; an in-flight one did not, because the
  first fix pumped the size from the shell's update pass and that pass is awaiting the send. The
  root's bounds are written by the render frame instead, which keeps running. Debug and Release
  builds pass without warnings; **needs a terminal check** — resize during a send in flight.
- Bare custom theme names now resolve from `~/.straumr/themes` with `.toml` inferred, so
  `:theme oxblood` selects `~/.straumr/themes/oxblood.toml`. Explicit paths and the previous
  settings-directory-relative form remain valid. The settings template and theme guide use the
  short form, and Debug and Release builds pass without warnings.
- The fixed dark-blue built-in theme is now named `straumr` instead of `deepocean`, including its
  display name, command completion, settings template, export filename and theme documentation.
  This is a clean early-access rename with no compatibility alias. Debug and Release builds pass
  without warnings, and a source/documentation scan leaves the old name only in the rename record.
- Confirmation dialogs now accept all four arrow keys as well as Tab for moving between Cancel and
  the confirming answer. Movement does not activate an answer, Cancel remains the initial focus,
  and the folder browser now uses the shared confirmation instead of carrying a second copy. Debug
  and Release builds pass without warnings; the input behavior needs a terminal check.
- Shared pane resizing fixed on 2026-09-16: the outer list/detail split now uses
  `FlexiblePane`, so the filter and summary minimum widths cannot pin the divider.
  The filter's resize guard also includes focus on the editor itself. Debug and Release
  builds pass without warnings, and 144 shared-layout geometry checks pass. The developer
  confirmed resizing works. The shared footer now names `Ctrl+H Ctrl+J Ctrl+K Ctrl+L Resize`
  where vertical resizing is available, and `Ctrl+H Ctrl+L Resize` elsewhere; terminal
  acceptance of the updated hint and the filter's Backspace guard remains pending.
- Phase: implementation
- Theming landed on 2026-09-16, ahead of the beta. The hardcoded blue palette is now one
  theme among others: `terminal` follows the reader's own terminal colours and is the
  default, `straumr` is the previous palette unchanged. A theme is named in
  `~/.straumr/settings.toml`, by built-in name, a custom name under `~/.straumr/themes`, or a path;
  `:settings`
  opens that file in `$EDITOR` and applies what it says on close, rebuilding the shell
  when the palette actually changed. `:theme` reports the applied theme and
  `:theme export <name>` writes a built-in out to start a custom one from. Proved
  headlessly: `straumr` is byte-identical to the old palette across all 59 members of
  `StraumrStyles`, and the resolver's error, fallback, rebuild-detection and
  role-collision paths are covered. Debug, Release and CLI-only builds pass without
  warnings and both projects are clean under the trim/AOT analysers. **Needs a terminal
  check**: how the `terminal` theme actually reads in the developer's scheme, whether
  `:settings` round-trips through their editor, and whether the rebuild lands them back
  where they were with focus intact. See the theming section of
  [validation-checklist.md](./validation-checklist.md).
- The settings file is deliberately minimal — the theme key and nothing else. Growing its
  key surface is the next piece of work, not a gap in this one.
- Active screen: S1, Secrets. Implemented on 2026-09-16; terminal acceptance is pending.
  A1 remains complete and accepted. See [screen-secrets.md](./screen-secrets.md).
- The developer reports S1 works, but the reference scan warned about four unreadable
  workspaces. All four were stale registry entries with absent files. The scan now skips
  these as Workspaces does; corrupt existing workspaces and unreadable resources still warn.
- S1 provides the global secret store with masked details, known references across registered
  workspaces, create/edit/copy through the shared editor, confirmed deletion, JSON editing and
  repair, filtering, and independent pane persistence. `:secret` / `:sc` exposes all actions
  from every screen. Existing `:rq`, `:au` and `:ws` commands are available from Secrets, and
  full-screen editors use the shell's existing return stack. Startup now primes completion even
  without an active workspace. Debug, Release and CLI-only builds pass without warnings;
  isolated storage, command-completion and snapshot checks pass. Focus, real resizing and
  cross-screen modal workflows still need a terminal check; no input harness was built.
- The developer confirmed the focus fix works in a terminal. Their next note: a copy clears the
  name and nothing said what the original was, so they had to leave the editor to look it up. The
  editor bar now carries `Source <name>` for a copy on all three resource screens, which is what
  the workspace copy dialog already did. Debug and Release build without warnings; the terminal
  check is in the S1 checklist.

- Focus no longer falls off the screen after a delete. The reported state — nothing titled on
  Secrets, Workspaces' keys on the footer — was focus re-homed onto the hidden Workspaces list,
  because each screen claimed `AutoFocus` through a binding that answers `true` and registers
  nothing whenever the list is detached, which every reload does. The shell now assigns
  `AutoFocus` beside `IsVisible` when it shows or hides a screen, and puts focus that is not
  inside the visible screen back on that screen's focus target on every pass. The fix is in the
  shell, so it covers Requests, Auths and Workspaces and every operation that reloads a list.
  Debug and Release build without warnings; the terminal check is listed in the S1 checklist.

- Implementation: W7 and W8 complete. R1's first implementation is in place and the
  developer reports that it works. Their first gripe — Tab reaching states where no
  region was titled — is fixed and awaits their check in a terminal. Requests pane
  shares now persist per screen, and an active workspace makes Requests the initial
  screen. This is a working checkpoint, not final visual acceptance or exhaustive
  workflow verification.
- R1 now replaces the small send spinner with a full-screen response view: animated
  in-flight activity, complete wrapped body/headers, measured network statistics,
  JSON beautify/minify and clipboard actions, plus cached-response expansion.
  Build and local layout/behavior checks pass; terminal acceptance is pending.
  The reported frozen in-flight duration is fixed: its label now ticks through the
  animation scheduler, verified during an awaited operation in a running terminal app.
  The developer requested reverting the separate orbital loading scene; the compact
  pulse and ticking duration are restored, and now sit in the summary bar.
- The full-screen response was then rebuilt from the shell's own pieces, from the
  developer's report that it works but does not feel cohesive: the identity header names
  it, a three-row bar carries the method and URL opposite the pulse or the finished
  measurements, and Body/Headers/Network are notched into the rule that closes the bar in
  place of a `Response` title stacked over a tab strip. The dialog frame lost its title and
  its padding so the rules run to it, a failure is reported once rather than twice, and the
  inline response's third tab was renamed to `Network` to match. Builds and layout captures
  pass and the Workspaces snapshots are unchanged; the focus chip, clicking a title and the
  animation need a terminal check.
- The developer reports the whole namespaced command set works in a terminal, and
  accepted A1 with it. What their pass did not single out, and so stays open: the
  no-active-workspace wording on each command, `Tab` completion inside the new
  namespaces, and the cancel-fetch key, which is still `Escape` only and has no command
  of its own by design.
- The namespaces now carry every action, not only the four that had been needed:
  `:rq create|edit|copy|delete|send|view|json|refresh`,
  `:au create|edit|copy|delete|fetch|json|refresh` and
  `:ws create|edit|copy|delete|import|export|use|refresh` are all reachable from any
  screen, under the CLI's verbs, each taking its subject by name or acting on the
  selection. Anything needing a workspace says so in one wording when there is none. The
  return to the calling screen is now driven by the screen reporting that it opened a
  full-screen surface rather than by a flag on the command, and the dialogs take their own
  initial focus so one opened by a command cannot be left behind the keyboard. Debug,
  Release and CLI-only builds pass with no warnings; none of it has been seen in a
  terminal.
- Typed commands now cross screen boundaries through noun namespaces. From Requests,
  `:ws use <workspace>` / `:workspace use <workspace>` runs Workspaces' existing
  activation command in place and reloads Requests without changing the visible screen;
  from Workspaces, `:rq send <request>` /
  `:request send <request>` navigates to Requests and runs the same `:send <request>`
  command available there. Nested command and request/workspace-name completion is in
  place. The canonical screen names are singular `:workspace` and `:request`, with
  `:ws` / `:rq` aliases; item selection is now `:select` instead of overloading those
  nouns. Workspace context changes preload hidden screens, so workspace names/IDs and
  request names complete even if their screen has not been visited. Debug and Release builds pass; terminal
  acceptance is pending.
- A full-screen response opened by a command from another screen now returns to its
  actual caller on close through a generic transient-screen event and return stack; no
  Workspaces return is hardcoded. Identifier completion quotes names containing spaces,
  the shared parser requires/unwraps those quotes, and Core create/save rejects request
  names containing a double quote. Parser/completion/Core validation probes and Debug,
  Release, and CLI-isolated builds pass; terminal acceptance is pending.
- The developer reports the cross-screen commands work. Their final naming gripe is
  fixed: navigation opts out of unique-prefix matching, so only `:workspace` / `:ws`
  and `:request` / `:rq` switch screens; `:w` in Requests no longer does. A focused
  resolver probe and Debug, Release, and CLI-isolated builds pass.
- The developer then reviewed that: the view now opens with Body focused, the header names
  the request alone rather than the kind of view, `s` sends again from inside the view,
  with Escape cancelling a re-send from a cached response too, and `Tab` changes page there
  the way it moves the chip everywhere else. A running in-memory app
  confirms the focus, the footer hints and the return to the in-flight state; the send
  itself still wants a terminal.
- R1 now covers creating, editing and copying a request through a structured form rather than
  only through the JSON. `c`, `e` and `y` open one full-screen editor with Request, Headers,
  Params and Body notched into the rule; `Ctrl+E` and `:json` keep the external editor as the
  advanced route, and `e` on an unreadable request still opens the text to repair it. The form
  is built on a new shared kit in `Visuals/Shared/Editor/`, designed from what the CLI's
  request, auth and secret flows have in common, so Auths and Secrets will supply only their
  own fields. `PagedPane`, `FormTextBox` and `ConfirmDialog` were extracted while doing it,
  and the URL check, method list, body-type list and `Content-Type` mapping moved into
  `RequestEditingHelpers` with the CLI repointed at them so the two cannot drift. Debug,
  Release and CLI-only builds pass without warnings and CLI `--help` runs. None of it has been
  seen in a terminal, and no probe was built for it by request; the Requests Screen Checkpoint
  lists what to check and in what order.
- The developer's first terminal pass over the editor produced three reports, all addressed:
  the method dropdown could not be moved through with `j`/`k`, the text of a JSON body was
  invisible while the caret moved through it, and they wanted intelligent JSON typing. The body
  is now written in `$EDITOR` at their direction rather than in an editor of our own — the
  framework's `CodeEditor` and its JSON highlighter are gone from the form, `ContentField` shows
  the document the way the previews do and opens it in the reader's editor on `Enter` or
  `Ctrl+E`, and the view suspends and re-shows itself around the run. `j`/`k`/`g`/`G` now move
  through every dropdown in the app, attached through `SelectStyle.PopupTemplateFactory`, which
  is the only hook into a popup `Select` builds itself. Debug, Release and CLI-only builds pass
  without warnings; none of it has been seen in a terminal.
- Their next pass found two more, both fixed: the Body page kept saying the request sent no body
  after a type was chosen, because a field's visibility was bound over the plain state object and
  so evaluated once — `EditorForm.Sync` now assigns it outright; and `Tab` walked into the hidden
  Request page's Name and URL boxes, which lay out over the `Type` row and under `Content`,
  because a raw framework focusable is a Tab stop wherever it sits. `FormTextBox`, `ChoiceField`
  and `ToggleField` now compute `IsTabStop` from the whole ancestor chain as `ResourceList` and
  `ScrollableContent` already did. That second one is the likely explanation of the original
  invisible-body-text report as well.
- They then asked the editor to open on a baseline rather than a blank file: an empty JSON body now
  arrives as `{`, an indented line and `}`, an empty XML body as its declaration, and single-line
  JSON arrives laid out; a document returned unchanged is not an edit, so none of it can alter a
  request nobody typed into. The JSON formatter is now one shared `RequestEditingHelpers` method
  with the response pane's toggle and the CLI's `--beautify` repointed at it.
- The editor is also asked to open with the caret where the writing starts, from a table of the
  documented per-editor syntaxes in `ExternalEditor`; there is no universal mechanism, and an editor
  not in the table is opened with no extra argument. Only a scaffold carries a position.
- Three further requests from the same pass are in: a field entered by `Tab` puts its caret after
  its value, done by `FormTextBox.SetText` at the point the field is written, since a bindable
  `Text` raises no document change for a value the form assigned;
  the focused field's label fills with the focus chip, which the developer
  chose over the accent-text version they were shown first; and the caret position now applies to a body that already exists,
  opening at the end of the line a reader would carry on from — for JSON the line above the
  closing brace.
- Two more since: a dropdown now takes `j`/`k`/`g`/`G` closed as well as open (the earlier change
  reached only the popup, through the style's factory), and `Ctrl+Tab` steps focus backwards as a
  global command beside the framework's own `Shift+Tab` — unpresented, because many terminals
  either flatten it to a bare `Tab` or keep it for their own tab switching.
- Secrets moved out of the Authentication pane into a region of its own in the same column, with
  its own title, share of the height and scrolling, and each reference on a row with its
  availability; a reference now reads as `{name}` in labels. Both were the developer's call, the
  second after seeing a request name trimmed mid-reference.
- Saving now keeps the editor open, with the bar's marker going green for a second and settling to
  grey; a created request stops being new so a second save updates it, and results are said on the
  editor's own footer.
- Fixed focus falling through to the hidden screen, reported twice: `t` on the Request pane and a
  click on empty list space. `AutoFocus` is ancestor-blind like `Tab` traversal, so both screens now
  claim it through `IsReachable`, and `PreviewPane` takes focus with the tab it selects.
- The developer accepted the Requests screen on 2026-09-15, after a session of terminal passes that
  produced the fixes listed in the entries above and in `changelog.md`. They then noticed the screen
  had never had a delete; `d` was added the same day, through the shared confirm modal and the
  update pass. R1 is complete, with that one check unticked.
- Post-acceptance, 2026-09-16: `s` now sends from any detail region, not only from the list — the
  developer pressed it while reading a Response pane and nothing happened. The command is registered
  on the container the detail regions share as well as on the list, and the empty Response pane says
  "No saved response." on every page instead of pointing back at the row. Builds clean; a terminal
  press from each pane is unticked in the checklist. `Enter` on a row opens the editor in the same
  pass, replacing the focus move into the Request pane that they called weird.
- A1 is implemented and documented in [screen-auths.md](./screen-auths.md): the four-region detail
  panel, colour-carried status, `f` to fetch and save a token, a type-driven editor whose pages
  appear and disappear with the shape being configured, `d` naming the requests that still point at
  the auth, and the `:auth` / `:au` namespace. It did not come out the size R1 predicted — the guess
  was a file the size of `RequestEditor.cs`, and `AuthEditor.cs` is 364 lines to that file's 193,
  with `AuthScreen.cs` at 1086 against Requests' 916. The shared kit did its job; an auth simply has
  more to say. Four configuration shapes across two axes drive which pages exist, and the detail
  panel answers four questions rather than two.
- What A1 needs now is the developer's terminal, in the order given at the end of
  [screen-auths.md](./screen-auths.md): the four regions at 120x30 and on a short terminal, `f`
  against a real token endpoint with its pulse and its `Escape`, the editor's pages appearing and
  disappearing as the type changes, the authorization-code grant's PKCE toggle, and `d` on an auth
  with dependents. Added since: `h` and `F1` opening the Extract page's help, and `h` correctly
  typing itself into the three text boxes rather than opening anything.
- Two checks carry forward from W8: Ctrl+C interrupting a genuinely in-flight send,
  and the AOT-published binary's TUI in Alacritty. Neither has explicit confirmation yet.
- Shared building blocks are in place; see [Shared Building Blocks](./shared-components.md)
  and [Adding a Screen](./adding-a-screen.md) before adding one
- Last updated: 2026-09-18

## Implementation Milestones

| ID | Milestone | Status | Evidence |
| --- | --- | --- | --- |
| P0 | Create implementation guide and tracker | Complete | [`docs/tui/`](./README.md) |
| W1 | Replace prototype root with the shared application shell | Complete | Reactive header/content and framework `CommandBar`; solution and CLI-only builds pass; fullscreen start/exit and CLI help verified. W2 later replaced the `DockLayout` root with a rule-separated `Grid` inside one window frame |
| W2 | Add read-only Workspaces list and selected-workspace details | Complete | Screen accepted interactively after several passes over layout, palette, vibrancy, cohesion and header. Extracted into `ResourceScreenLayout`/`ResourceList`/`FieldList`; refactor proved render-identical by snapshot diff at 120x28, 70x20 and 90x16, populated and empty. Solution and CLI-only builds pass; broader resilience checks remain in W8 |
| W3 | Add selected workspace's recently used Requests pane | Complete | Non-stamping request loading, per-workspace caching, recency ordering, loading/empty/error states and semantic method colours implemented. Release build passes; initial load, workspace switching, cache reuse and clean exit verified in an 80x24 populated terminal. User directed work to continue with W4 |
| W4 | Add focus, arrow, pointer, `j`/`k`, and activation behavior | Complete | Implemented: Tab/Shift+Tab focus traversal, contextual command hints, arrows/Home/End/Page plus `j`/`k`/`g`/`G` on both the list and the request preview, wheel support, Core activation, and double-click activation. Framework finding: `PointerEventArgs.ClickCount` counts a click sequence by time and not by position, so a click anywhere followed by one click on a row arrived as a pair; the gesture therefore also requires both clicks on the same row, and a pointer leaving the list voids the sequence. Focus cues were reworked twice after review: the focused section title fills with the selection blue while every other title is inert, the permanently bright left detail title was fixed, all titles moved onto one rule so the chip travels sideways rather than diagonally, the first detail pane was retitled `Details`, and the active workspace gained a green dot that follows activation. Release and CLI-only builds pass. Cell dumps cover the chip states at exact hex, the mirrored panel geometry, and the dot across plain, hovered and both selected bands. Accepted interactively: focus cues, keyboard selection, hover band, pointer selection, the focused and unfocused selection bands, both focus directions, long-preview scrolling, top/bottom jumps, paging, activation moving the dot, and clean exit |
| W5 | Add command prompt integration and workspace navigation commands | Complete | `PromptEditor` overlaid on the footer row in a `ZStack`, the `:` gesture registered globally both bare and with `Shift`, `TuiCommandSet` with exact/alias/unique-prefix resolution and per-token completion, `quit`/`q`/`exit`, and the screen's `workspace`, `use` and `refresh`. `WorkspaceScreen`'s activation was split out so `Enter`, a double-click and `:use` share one method, and `LoadAsync` became re-runnable for `refresh`. Eleven framework findings, all recorded in Framework Rules: `PromptEditor`'s prompt column has a two-cell minimum, so `" :"` is what aligns the colon with the text column; `PromptEditorStyle` cannot colour the editor's own text, so the palette goes through the `Highlighter` delegate; `Visual.App` is null until the app runs; `ContentSwitcher` attaches only its selected child, which is why it cannot host a visual the app must focus; focus is revoked from a visual that is invisible during the focus pass, so the prompt sets its own `IsVisible` before asking for focus; `HasFocus` lags `FocusedElement` by a pass; and a printable keystroke emits a key event and a text event independently, so the gesture that opens the prompt also types its own character into it unless the prompt discards the echo; the completion handler is re-asked on every `Tab` and the framework keeps no cycle state, so the prompt has to hold the candidate list itself; neither `PromptEditorEscapeBehavior` gives `Escape` one meaning, so the prompt clears `CancelCommand.Gesture` and handles the key itself; and a key a surface does not handle becomes focus traversal, so a surface that must own input has to declare `IModalVisual` as `Dialog` and `Popup` do; and the framework's own quit command comes off through `RemoveGlobalCommand(DefaultQuitCommandId)`, gesture and hint together. Solution, Release and CLI-only builds pass. Evidence: command resolution and completion tables over 15 inputs and 14 caret positions; footer cell dumps at exact hex for hints, message, error and prompt states; full-screen dumps at 96x24, 70x20 and 44x14; and a full round trip driven through the real input path on a running `TerminalApp` — `:` opens and focuses the prompt, typed text reaches the editor, `Enter` runs `:use dashboards` through Core and returns focus to the list, a single `Escape` closes and clears even with a completion on screen, `:bogus` reports `unknown command: bogus`, nothing behind the modal prompt reacts to `Tab`, `Shift+Tab`, a screen gesture or a click, and `:q` is the only exit now that the framework's `Ctrl+Q` is removed. The developer confirmed `:` opens the prompt in a terminal, reported the stray colon that the echo discard now fixes, reported that `Tab` could not cycle between two workspaces sharing a prefix, which the held candidate list now fixes, and reported the three fall-through bugs that modality now fixes. Not covered: `Up`/`Down` history, which needs a terminal |
| W6 | Add filtering | Complete | Added the shared retained `ResourceFilter`, live case-insensitive workspace-name/path filtering, match/total badge, stable selection by workspace identity, a focusable no-match state, and `Enter`/`Escape` result focus behavior. The `/` gesture is registered in bare and Shift forms and its paired text echo is discarded. `ResourceList.SetRows` keeps list identity and focus stable while rows change. Debug, Release and CLI-only builds pass with no warnings; CLI help and an empty-registry launch/`:q` exit pass. Accepted interactively by the developer: filtering worked as intended |
| W7 | Add create, edit, copy, import, export, and delete workflows | Complete | W7a accepted interactively: the `d` modal, cancellation, deletion, refresh, notification, and focus restoration work as intended. W7b implemented: `c` and `y` open one shared styled form for create and copy, with Name validation, an optional location using the configured default as its placeholder, a tab-reachable folder browser, keyboard/pointer controls, queued Core I/O, reload, selection of the result, and footer reporting. The folder browser is composed from the framework's modal, one-line list, scrolling, and button controls because version 3.9.0 and current upstream provide no ready-made directory picker. The empty registry keeps the focusable retained list mounted so Create remains reachable. The folder browser then became `FolderBrowserDialog`, a reusable component that takes a start path, a title and a confirm label and knows nothing about workspaces: navigation on a `ResourceList` of one-line rows, a parent row named after the folder it leads to, walking up landing on the folder just left, a front-trimmed breadcrumb swapping for an editable path on `Ctrl+L`, `s` or the button confirming the highlighted folder — a native picker's meaning, falling back to the folder being browsed on the parent row, with the resolved path shown beside the button — `Ctrl+Enter` confirming the folder being browsed outright, unpresented because only some terminals report the modifier, `n`/`r`/`d` to create, rename and delete, and one notice line for a failed read, an empty folder or a query with no matches. Delete is recursive with a confirmation naming the folders and files inside, because a portable implementation has no recycle bin; rename and delete are unguarded by design, the registry being equally exposed to any file manager. Ten defects were found and fixed by probes driving a real `TerminalApp`, and three by the developer in a terminal. From the probes: the path field stole initial focus so no list hints were reachable, `Escape` in it closed the whole picker, walking up lost the cursor's place, the count badge counted the parent row, `Ctrl+L` echoed its own character, a wrapped validation message, `Rename`/`Delete` advertised but inert on the parent row, and the browser opening on the parent row. From the developer: `Ctrl+L` did nothing, because `Ctrl`+letter arrives as the letter's C0 control byte and the gesture has to carry it — the probe had been synthesising an event shape no decoder produces; and `:` was offered while browsing, a global command being collected alongside the focus chain rather than from it. That audit also condemned `Ctrl+Enter` for Select, which a plain terminal cannot distinguish from `Enter`, and moved character gestures off the dialog onto the list, where they no longer fire while a text field has focus. Four pre-existing shared bugs came out with them, all fixed: `ResourceFilter` never reported a cleared query because `PromptEditor.Text`'s setter raises no event; `G` jumped to the top of a list in both `ResourceList` and `ScrollableContent` because gesture routing matches characters case-insensitively; and `ResourceRow.Meta` became optional for one-line rows. `ResourceList` proved render-identical for the three-line and two-line shapes by snapshot diff against `HEAD`. Debug, Release and CLI-only builds pass with no warnings; CLI help passes; 25 headless behavior assertions pass. The developer accepted the browser over four rounds of interactive review, which produced the remaining fixes: the `Ctrl+L` control-character gesture, `:` gated out of modals, Copy starting beside its source, the resolved location moved out of the placeholder, and the two confirms split so `s` takes the highlight and `Ctrl+Enter` the folder being browsed. W7b is complete but for a populated create and copy end to end, which has no developer confirmation of its own yet. W7c adds `i` import and `x` export through the shared `BrowserDialog` specializations: `FileBrowserDialog` lists and completes only `.straumrpak` archives beside navigable folders, while `FolderBrowserDialog` retains folder selection. Both operations queue Core I/O through `UpdateAsync`, reload and select an imported workspace, restore modal focus, and report through the footer. Debug, Release, CLI-only, and CLI help checks pass; the developer accepted the W7c UX interactively. W7d adds `e` editing through `$EDITOR`: the host ends fullscreen and stops the framework's persistent input loop before launching the process, then re-enters with the retained tree, reloads and reselects the edited workspace, restores list focus, and reports success or discarded changes in the footer. Quoted executable paths and editor arguments such as `--wait` are supported without a shell. A headless end-to-end probe verified command parsing, JSON edit/save, two consecutive fullscreen hosts over one retained tree, root detachment, and focus restoration. The developer then rejected discarding an edit that produced invalid JSON, so `ExternalEditor` now returns text and the screen owns what happens to text it cannot read: the edit is written to the workspace file and the workspace is listed as unreadable, named after its folder, red on the list, refused for activation, withdrawn from `Copy` and `Export`, and repaired by pressing `e` again. A mismatched ID is treated the same way rather than rejected and thrown away. Loading moved from `ListAsync` to one read per registry entry so one unreadable file scopes its failure to its own row instead of taking the screen to its error state, which also covers a file broken from outside Straumr. `ResourceRow.IsBroken`, `FieldList.Problem` and a `RedBright` palette entry carry it in the shared pieces. Debug, Release, solution and CLI-only builds pass with no warnings. The developer then accepted W7d interactively, and finally a populated create and copy end to end, which was W7b's own outstanding confirmation. All four checkpoints are accepted and W7 is complete |
| W8 | Validate resizing, empty/error states, CLI isolation, and Native AOT | Complete | CLI-only and full builds use separate `BaseOutputPath` values, so alternating them no longer mixes stale outputs (see [decisions.md](./decisions.md)). The folder browser dialog's height is a computed value re-clamped every update pass instead of one set once in `Show`, so a resize while it is open reflows; developer-confirmed. A workspace file corrupted or removed from outside Straumr behaves correctly on `:refresh`; developer-confirmed. `dotnet publish -c Release -r win-x64` (self-contained, single-file, Native AOT) succeeds and the published `straumr.exe` runs `--help`; the developer's environment needed `vswhere.exe`'s directory added to `PATH` for the Native AOT linker step, which was otherwise missing even though Visual Studio's own linker was present. Ctrl+C now exits the app; it needed a redo after a `Console.CancelKeyPress`-based attempt produced no observable effect in a terminal, replaced with a global command on the Ctrl+C control-character gesture matching how every other gesture in this app is delivered (see [decisions.md](./decisions.md)); developer-confirmed. Two checks carry forward rather than block completion, since neither is answerable yet: whether that same Ctrl+C cancellation interrupts a genuinely in-flight operation rather than only exiting once it finishes on its own, which needs R1's `send` to give the app a slow, cancellable operation to test against; and the AOT-published binary's TUI screen itself (not just CLI `--help`), which the developer will check separately with Alacritty |
| R1 | Implement Requests screen | Complete | Structured create/edit/copy through the shared `Visuals/Shared/Editor/` kit: one full-screen form, Request/Headers/Params/Body on the rule, `Tab` between fields, `Ctrl+S` to save, `Escape` to close with an unsaved confirm. Key-value maps are a `ResourceList`; a multipart part can be a file chosen through `FileBrowserDialog` and stored as `@path`; the body is shown as the previews show one and written in `$EDITOR` on `Enter` or `Ctrl+E`, the view suspending and re-showing itself around the run, and each body type retains its own content and its own file extension. `Ctrl+E` / `:json` keep the external editor as the advanced route; `e` on a broken request still opens it. `PagedPane`, `FormTextBox` and `ConfirmDialog` extracted; URL/method/body-type/`Content-Type` helpers moved to `RequestEditingHelpers` and the CLI repointed at them. Debug, Release, CLI-only builds and CLI `--help` pass. No terminal check yet and no probe built, by request. Cohesion pass over the full-screen response: shell header, three-row bar, page titles on the rule, no frame title, one report of a failure, `Network` on both surfaces. Navigation, method-token rows, filtering, authentication/secret-reference details, request/response preview tabs, cancellable Core sending, external-editor editing, per-screen pane persistence, active-workspace startup navigation, direct `:send <request>`, and cross-screen `:ws …` / `:rq …` command namespaces implemented. Debug and Release solution builds and the Release CLI-only build pass. A focused probe restored, saved, and JSON-round-tripped pane shares and confirmed Requests as the initial active-workspace screen. Shared-list snapshots are identical; shared detail-grid alignment corrected. Developer reports that it works; their reported ghost focus states and the new typed-command paths await a terminal check. Their first pass over the editor reported the method dropdown ignoring `j`/`k`, invisible body text, and wanting intelligent JSON typing: the dropdown keys are attached through `SelectStyle.PopupTemplateFactory` so every dropdown takes them, and the body moved out to `$EDITOR` at their direction, which retires the code editor the invisible text was in and gives the JSON behaviour to the editor that already has it. A long closing round of terminal passes then produced, in order: the Body page's discriminator assigned rather than bound; `Tab` no longer reaching the hidden pages' fields; `$EDITOR` opened on a scaffold, at a caret, under the type's extension; the caret left after a value in every field; the focused field's label chipped; the unsaved marker decided by comparison; the bar and header mirrored out of the plain state; saving keeping the editor open with the marker flashing green; Secrets given its own region; references reading as `{name}` in labels; the pair dialog's echo and its bound-visibility crash; focus no longer falling through to the screen behind, by `AutoFocus` and by `PreviewPane` keeping every page attached; the hint rows wrapping instead of clipping; and `d` to delete a request, which the screen had never had. Debug, Release and CLI-only builds pass without warnings throughout, and CLI `send --help` renders. The developer accepted the screen on 2026-09-15. Not covered by that acceptance and carried into A1's checkpoint: `Ctrl+C` interrupting a genuinely in-flight send, the AOT-published binary's TUI in Alacritty, whether this terminal delivers `Ctrl+Tab` at all, and the unticked lines in [validation-checklist.md](./validation-checklist.md) |
| A1 | Implement Auths screen | Complete | Auths belong to the active workspace, load without stamping access times, sort by last access, keep selection, and keep an unreadable auth as a broken row with an editor repair path. The detail panel is four regions — Configuration, Credential, Secrets, Used by — because an auth answers four separate questions. No credential material is printed: a secret reference is named, everything else reads `set` or `not set`. Status is carried in colour rather than a word of its own, amber for held, grey for empty, red for the reason the next send will fail, with green left to the active workspace. `f` fetches an OAuth2 token or a custom auth's extracted value and saves it, from the list or from any detail region, with the summary bar swapping its badge for the pulse and `Escape` cancelling; it is not offered for Bearer or Basic. `c`/`e`/`y`/`Enter` open the shared resource editor, whose Auth, Grant, Headers, Params, Body and Extract pages exist only when the type calls for them, each shape retained across a type change. `d` confirms with a count of the requests still pointing at the auth. `Ctrl+E` and `:json` keep the external editor; `:select`/`:a`, `:refresh`, `:fetch` and the `:auth`/`:au` namespace are in. Core now refuses a double quote in an auth name as it already did for a request. Debug, Release and CLI-only builds pass with no warnings and CLI `create auth --help` renders after the shared helpers moved. A layout diagnostic (`.tmp/a1-check`) over a fixture of one auth per type confirms the four-region geometry, the `┬` and `┼` junctions, `{name}` secret references and amber `valid`/`on` at `#ffc857`; the shared refactors are byte-identical to `HEAD` by snapshot diff on Requests at four sizes and all three list shapes. The Extract page then gained help on `h`, with `F1` beside it because a focused field swallows every letter and three of that page's four fields are text boxes; the two share one hint, `h /F1 Help`, and sit on that page's root so no other page offers them. Its dialog describes `StraumrAuthService`'s three extractors — the dotted walk that is not JSONPath, the header lookup that ignores case and falls through to the content headers, and the first-match regex preferring group 1 — with worked examples, and renders as its own capture. No input harness, by standing request. The developer then took it to a terminal with the full namespaced command set in place and accepted it, which completes A1. Not covered by that acceptance and carried forward: `Ctrl+C` interrupting a genuinely in-flight send or fetch, the AOT-published binary's TUI in Alacritty, whether this terminal delivers `Ctrl+Tab` at all, and the unticked lines in [validation-checklist.md](./validation-checklist.md) |
| S1 | Implement Secrets screen | Implemented; terminal acceptance pending | Global list including broken/missing entries; masked value and metadata; non-stamping known-reference scan across registered workspaces with incomplete-scan reporting; shared create/edit/copy form and save lifecycle; confirmed delete with dependent counts; JSON edit/repair; filtering and persisted pane shares. `:secret` / `:sc` commands and no-workspace completion are wired into the existing shell. Debug, Release and CLI-only builds pass without warnings; isolated fixture verifies storage immutability on inspection, reference matching, masking, completion and create/update/delete behavior. See [screen-secrets.md](./screen-secrets.md) for evidence and terminal checks. |

Only one milestone should be active at a time unless a prerequisite must be
completed with it. Update the status and Evidence column in the same change that
completes a milestone.

## Collaboration and Checkpoints

- Stop at a reviewable checkpoint after each milestone instead of attempting the
  entire TUI rewrite in one pass.
- Keep this guide current so implementation can resume safely after context
  compaction or a later session.
- Ask the user to run an interactive check, platform-specific build, or publish
  when their local environment can provide better evidence than automation.
- Record unresolved framework behavior and user verification results in the
  active milestone's Evidence entry before moving on, and the constraint itself in
  the matching `framework-*.md`.
- Do not begin the next milestone until the current milestone builds and its
  smallest applicable behavior has been verified.
