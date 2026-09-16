# Validation Checklist

Part of the [TUI implementation guide](./README.md). Run the smallest applicable
subset for the milestone in hand, and tick items here in the same change.

- [x] Cross-screen typed-command implementation builds in Debug, Release, and the
      Release CLI-isolated configuration with no warnings.
- [x] Navigation resolution is exact-only: a focused command-set probe returns no
      command for `w` or `wo`, and resolves both `ws` and `workspace` to the canonical
      workspace command. Ordinary command prefix matching remains enabled by default.
- [x] Quoted identifier probes: a quoted name with spaces parses to one value; an
      unquoted spaced name and an unterminated quote fail; completion turns both a plain
      and already-open partial into quoted candidates; Core accepts a normal request name
      and rejects one containing a double quote.
- [ ] Terminal-check `:send <request>`, `:ws use <workspace>` /
      `:workspace use <workspace>`, and `:rq send <request>` /
      `:request send <request>`; include name/ID completion before Workspaces has been
      visited, Requests remaining visible and refreshing after `use`, bare namespace
      navigation, automatic quoting for spaced request names, full-screen response focus,
      returning to the actual calling screen on close, and the no-active-workspace footer error.
- [x] The full-screen response opens with Body focused, changes page on `Tab` and back on
      `Shift+Tab` with focus following the page, and offers `s Send again` when it is not
      sending: verified on a running in-memory app, which also showed the bar and footer
      returning to `IN FLIGHT` and `Escape Cancel` on a re-send, and the footer reading
      `b`, `y`, `Escape Back`, `s Send again`, `Tab /t Next tab` at 120 columns, with both
      `Tab` and `/t` in the key colour and only the label grey (checked against the emitted
      colour codes, since a `CommandBar` renders nothing without a running app).
- [x] Full-screen response reads as a screen: header, three-row bar, titles on the rule,
      rules meeting the frame. Captured at 120x30, 80x24 and 44x14 while sending, on
      success and on a transport failure.
- [x] The shared header and footer insets moved without moving anything: Workspaces and
      the one-, two- and three-line list snapshots are byte-identical to before.
- [ ] Terminal check of the cohesion pass: the selected title carries the focus chip and
      no second cue competes with it, clicking a title selects that page and leaves focus
      in the pane, the bar does not jump when a send lands, a failed send
      says it once in the bar and once on the pages, `Tab` and `Shift+Tab` change page, and
      `s` sends again — including a second send from a cached response and Escape
      cancelling one.
- [x] Full-screen response: Debug/Release solution and Release CLI-only builds.
- [x] Restored compact send appearance: local layout/response checks and Debug
      (separate output directory) and Release builds pass.
- [x] In-flight elapsed label advances during awaited work and stops with its clock:
      verified through the real animation scheduler in an in-memory TerminalApp.
- [x] Response layout captures at 120x28, 80x24 and 44x14: sending, success, formatted
      JSON and timeout. Preserves JSON line breaks and the method token when narrow.
- [x] Response helper checks: beautify/minify, >64 KiB full-body retention, invalid
      JSON unchanged, header/body timing consistency, cancellation propagation and
      transport failure (local diagnostic, no input harness or real clipboard writes).
- [ ] Terminal check: `s` opens full-screen, pulse/elapsed animate, resize reflows,
      `t` changes tabs, `b` formats, `y` copies, Escape cancels then returns, `v`
      reopens without sending; check slow sends and Ctrl+C too.

For each Workspaces milestone, run the smallest applicable subset:

- [x] `dotnet build src/Straumr.sln`
- [x] launch `straumr` and inspect the Workspaces screen interactively
- [x] verify resize behavior at narrow and wide terminal sizes: the folder browser now
      re-clamps its height to the viewport on every update pass rather than once on open
      (see [decisions.md](./decisions.md)), so a resize while it is open reflows instead of leaving it sized
      for the terminal it was opened in (developer confirmed in a terminal)
- [x] verify the Requests pane layout is restored and saved per screen: an in-memory
      options probe restored 42/53/61 for `PaneLayouts["Requests"]`, moved the panel
      divider once, and observed one save carrying 46/53/61
- [x] verify initial navigation with an active workspace: the retained shell selected
      Requests before its first render in the in-memory host; the no-active-workspace
      branch remains Workspaces by construction
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
- [ ] verify the Requests screen's Tab cycle in a terminal: Requests → Authentication →
      Request → Response and back to Requests, forwards and with Shift+Tab, with exactly
      one region titled at every stop and `Ctrl` plus a Vim direction moving the divider
      that region sits against (headless: the cycle, both directions, and the titles;
      the terminal check is the developer's)
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
- [x] verify `Up`/`Down` walk the prompt history (developer confirmed in a terminal)
- [x] verify typed command resolution, aliases, unique prefixes, ambiguity and
      unknown names (headless tables over the real command set)
- [x] verify `/` from both list and request-preview focus, live name/path filtering,
      match counts, no matches, Enter retention, Escape clearing, and pointer entry
- [x] verify empty workspace registry behavior
- [x] verify `straumr --help` still opens CLI help
- [x] verify `-p:IncludeTui=false` builds without the TUI dependency graph, and that it
      and a full build no longer share output: each now has its own `BaseOutputPath`, so
      alternating between them is exact without a clean (see [decisions.md](./decisions.md))
- [x] verify the full self-contained Native AOT publish: `dotnet publish -c Release
      -r win-x64` succeeds and the published `straumr.exe` runs `--help`. Required
      `vswhere.exe`'s directory on `PATH` for the linker step to find MSVC; it was not on
      `PATH` in this environment even though Visual Studio's own linker was installed.
      Not yet confirmed: the TUI screen itself under this AOT binary in an interactive
      terminal, which trimming could affect even though production code avoids reflection
- [x] verify a workspace file broken or removed from outside Straumr with a manual test:
      a corrupt one lists red and names its problem, a missing one is dropped, and neither
      takes the screen to its error state (developer confirmed by corrupting/deleting a
      `.straumr` file on disk and running `:refresh`)
- [x] verify Ctrl+C exits the app: nothing previously cancelled the token threaded through
      `LoadAsync`/`UpdateAsync` in practice. A first attempt linked a `CancellationTokenSource`
      to `Console.CancelKeyPress`; the developer tested it and Ctrl+C did nothing at all, not
      even the prior instant-kill. Replaced with a global command on the same Ctrl-plus-letter
      control-character gesture the rest of the app already uses (`(char)('C' & 0x1F)` with
      `TerminalModifiers.Ctrl`), cancelling a `CancellationTokenSource` owned by
      `StraumrTuiApp` and linked into every `UpdateAsync` call, and calling `RequestExit()`
      the same way `:q` does (see [decisions.md](./decisions.md)). Developer confirmed Ctrl+C now closes the app
- [ ] verify Ctrl+C interrupts a genuinely in-flight operation rather than only exiting once
      it finishes on its own. The linked `CancellationTokenSource` should propagate into a
      slow Core call the moment it is pressed, but nothing in the Workspaces screen is slow
      enough to test this against; the developer will check it against R1's `send`, which is
      the first genuinely slow, cancellable operation in the app
- [x] verify the resource editor builds everywhere it has to: Debug and Release across the
      solution and Release with `-p:IncludeTui=false`, all without warnings, and CLI `--help`
      still runs after the URL check, method list, body-type list and `Content-Type` mapping
      moved from the CLI's private helpers into `RequestEditingHelpers`
- [x] verify the editor in a terminal, which nothing above covers — no probe was built for it
      by request. Done over a long interactive session; every fault it found is fixed and listed in
      [changelog.md](./changelog.md). In rough order of how likely each is to be wrong:
      the form's layout at 120x30, 80x24 and a short terminal;
      whether `Ctrl+S` reaches the app or is swallowed as XOFF, and `Ctrl+E` beside it;
      the `Select` dropdown popups opening over a full-screen dialog;
      the file browser opening from the multipart dialog, which is a modal over a modal;
      `Escape` asking before discarding edits, and saying nothing when there are none;
      `c`, `e` and `y` not typing their own letter into the name field;
      no bare letter changing page while a field is being typed into (was `t`, now `Ctrl+T`);
      whether Ctrl+C inside the editor copies or quits the app
- [ ] verify a request round-trips through the form with nothing lost: create one with
      headers, params and a JSON body, save, reopen, and confirm every field came back; then
      edit one that has a `Group` set and confirm the form did not drop it
- [ ] verify each body type keeps its own content: type JSON, switch to XML and back, and
      confirm the JSON survived; confirm `Content-Type` follows the type and shows on Headers
- [x] verify writing a body in `$EDITOR` from inside the form (developer-confirmed, including the
      second run in a row and the return to the field they left): press `Enter` on the Body page,
      confirm the editor opens on a file named with the type's extension, save and quit, and
      confirm the app comes back to the Body page of the same form with the text in the preview,
      the unsaved marker set, focus back on the body rather than on the type above it, and
      everything typed on the other pages still there. Then do it
      twice in a row, which is where a dialog left parented to a finished app would be refused;
      then quit the editor without saving, and with a non-zero exit, and confirm the failure is
      reported on the editor's own footer rather than lost behind it. Finally run it with no
      `EDITOR` set and confirm the field says so instead of doing nothing
- [ ] verify `j`/`k`/`g`/`G` move through an open dropdown — the method list is the one that was
      reported, but the Auth and body-type lists take the same keys now — and confirm the frame
      around the popup is unchanged, since the style now supplies that factory itself
- [x] verify the Body page follows its type (developer-confirmed): choose JSON and confirm the "sends no body" message
      is replaced by the content preview in the same keystroke, choose None and confirm it comes
      back, and choose Form URL Encoded and Multipart and confirm each shows its own fields
- [x] verify `Tab` on every page stops only on what is on that page (developer-confirmed): no caret on a row that
      belongs to another page, and nothing typed into a field that is not being drawn. Worth
      checking on the pair dialog too, where the value box and the file picker swap
- [ ] verify what the editor opens on: an empty JSON body arrives as `{`, an indented blank line
      and `}`, an empty XML body as its declaration line, and a text body as an empty file; a
      minified JSON body arrives laid out over lines and a hand-formatted one arrives untouched.
      Then quit each without typing and confirm the request is unchanged and still reads saved
- [ ] verify the response pane's `b` still beautifies and minifies, and CLI `send --beautify`
      still formats JSON and XML, both now going through `RequestEditingHelpers.TryFormatJson`
- [ ] verify the caret lands between the braces on an empty JSON body in the developer's own
      editor (nano: `+2,3`), and that an editor outside the table — or one reached through a
      wrapper script — still opens normally with no stray argument and no second file created
- [x] verify the caret sits after the value in every pre-filled field the form opens with
      (developer-confirmed after the first attempt did nothing), that a
      pointer click still places it where it was clicked, and that leaving a field mid-word and
      coming back keeps the place rather than jumping to the end
- [x] verify the label of the focused field fills with the chip (developer-confirmed; they chose
      the chip over the accent-text version) and every other label stays inert,
      on both the one-row fields and the one that fills the page, and that nothing shifts as focus
      moves — the longest label on a page is the one to watch, since its chip exactly fills the
      label column
- [ ] verify an existing JSON body opens on the line above its closing brace, at the end of it,
      and a text body on the end of its last line
- [x] verify `a` and `e` on a header, parameter, form field and multipart part open the pair dialog
      (developer-confirmed)
      with the name empty and with the existing name unchanged, rather than with the keystroke that
      opened it typed into them
- [x] verify a multipart part survives the kind picker (developer-confirmed; this is the crash
      they hit and it is gone): open one, switch between Text and File both
      ways, type in each, and move the selection afterwards, which is where a bound visibility over
      a subtree holding a text field used to throw
- [x] verify `j`/`k`/`g`/`G` move a dropdown both closed and open (developer-confirmed on the
      multipart kind picker, which is where the closed-state gap was found) — the method, auth and body type
      fields and the multipart kind picker — and that a letter typed into a text field beside one
      still reaches the field
- [ ] verify `Ctrl+Tab` steps focus backwards, and find out whether this terminal delivers it at
      all: several keep the combination for their own tab switching. `Shift+Tab` must keep working
      either way, on a screen and inside a dialog
- [x] verify the editor's chrome follows the fields (developer-confirmed on the method, the URL
      and the header): change the method and watch the bar's token
      change text as well as colour, edit the URL and watch the bar follow, and type a name and
      watch the header follow
- [ ] verify the unsaved marker comes back: change a field and watch it turn amber, change it back
      and watch it read saved again, then press `Escape` and confirm it closes without asking.
      Worth trying on a header and a multipart part too, which fold into the request later than a
      text field does
- [ ] verify the Secrets region: its title lights when it owns focus and Authentication's does not,
      `j`/`k` scroll it when the list is longer than its share, an unavailable secret reads red, and
      a request with no references says so rather than showing an empty region
- [ ] verify a secret reference reads as `{name}` in the request list, both summary bars and both
      headers, and still reads as `{{secret:name}}` in the editor's fields and the body preview
- [ ] verify saving keeps the editor open: `Ctrl+S` leaves the form up, the marker goes green and
      is grey a second later without touching the keyboard, and `Escape` then closes without asking
- [ ] verify a created request is only created once: `c`, fill it in, `Ctrl+S` twice, and confirm
      the list holds one request and the second save updated it. Then edit a field and confirm the
      marker goes back to unsaved
- [x] verify focus cannot fall through to the screen behind (developer-confirmed by both routes,
      key and pointer, with no flicker in between): on Requests, press `t` in the Request
      pane and confirm the page changes with the title still lit, the footer still showing this
      screen's commands and this page's `j`/`k` hints, and nothing flickering in between; then click
      the blank strip above the first row and confirm the same. The footer naming another screen's
      commands, or losing the scroll hints for a frame, is the tell
- [x] verify the hint rows wrap rather than clip at a narrow width (developer-confirmed): shrink the terminal until the
      footer needs two rows on the list, the editor and the full-screen response, and confirm every
      hint is still readable and the content above shrank to make room
- [x] verify deleting a request: `d` asks first and Cancel is focused, cancelling changes nothing,
      confirming removes the file and leaves the selection on the row below — the row above when it
      was the last one — and the footer says what went (developer-confirmed on the ordinary path).
      Not yet tried: deleting a request that cannot be read, and one with a response cached

- [ ] verify `s` sends from every detail region: Tab to Authentication, Secrets, the Request pane
      and the Response pane, on more than one page of the tabbed ones, and confirm `s` opens the
      full-screen send each time and that the footer offers it there. Then press `/`, type a name
      containing an `s`, and confirm the letter reaches the filter rather than sending
- [ ] verify the empty Response pane reads `No saved response.` on Headers and Network and
      `No saved response. Press s to send the request.` on Body
- [ ] verify `Enter` on a request opens the editor exactly as `e` does, including a broken request
      opening as JSON, that `e` still works though it has no hint of its own, and that the footer
      reads one `Enter /e Edit` with both `Enter` and `/e` in the key colour and only `Edit` grey

- [ ] verify the Auths screen's four regions at 120x30 and on a short terminal: Configuration,
      Credential, Secrets and Used by all present with their rules meeting at `┬` and `┼`, `Tab`
      reaching each of the four, and the focus chip landing on each title in turn. The snapshot
      renderer applies no focus and reflows nothing, so both the chip and real resizing are unproven
- [ ] verify `f` against a real token endpoint: the summary bar swaps its identifier badge for the
      pulse, the duration ticks rather than freezing as R1's first one did, the fetched token is
      saved so the Credential region reads it back, and `Escape` cancels a fetch in flight. Then
      confirm `f` is absent for a Bearer and a Basic auth, and that it works from a detail region and
      not only from the list
- [ ] verify the editor's pages appear and disappear with the type: start on Bearer with only Auth,
      switch to OAuth2 and watch Grant arrive, switch to Custom and watch Headers, Params, Body and
      Extract arrive, and confirm `Ctrl+T` steps over the pages that do not apply rather than landing
      on an untitled one. Then switch back and confirm the old shape's values are still there
- [ ] verify the `Grant` page for the authorization code grant, whose PKCE toggle reveals a field
      below it — the shape most likely to catch the assigned-not-bound visibility rule
- [ ] verify `d` on an auth that requests point at: the confirmation names how many, cancelling
      changes nothing, confirming removes the auth and leaves those requests untouched and still
      pointing at an auth that is gone
- [ ] verify a broken auth: one whose JSON does not parse stays on the list as a red row named after
      its file, is refused for Copy and Fetch, opens as text on `e`, and saves back for repair
- [ ] verify the Extract page's help: on a Custom auth's Extract page the footer reads `h /F1 Help`
      with both keys in the key colour, `h` opens the dialog while the Source dropdown has focus,
      and `F1` opens it from inside the Expression, Apply to and Template boxes — where `h` should
      type an `h` instead, which is the behaviour, not a bug. Then confirm the hint is absent on the
      Auth, Grant, Headers, Params and Body pages
- [ ] verify the help dialog itself: the three example columns line up, the regex examples show
      their square brackets, `j`/`k` scroll it on a terminal too short to hold it, `Escape` and the
      Close button both dismiss it, and closing returns focus to the field it was opened from
