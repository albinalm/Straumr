# Requests Screen (R1)

Part of the [TUI implementation guide](./README.md). The active milestone: what R1
implements, what evidence exists, and where to resume.

## Implemented behavior

- Requests belong to the active workspace. Entry/refresh loads through Core without
  stamping access times, sorts by last access, and preserves selection where possible.
  A missing active workspace points back to Workspaces. Missing request files are
  skipped; unreadable requests remain as broken rows with an editor repair path.
  Request files are `{id}.json` beside the workspace's `.straumr` file.
- The shared list shows method and name. Filtering matches name, method and URL;
  `:select <name>`/`:r <name>` resolves exact names or unique prefixes and clears
  a filter to reach the selection. `:send <name>` resolves through the same selection
  path and opens the full-screen send view. `:refresh` reloads the current screen.
- `:ws <command>` and `:workspace <command>` dispatch through Workspaces' command
  table. `:ws use linkz-identity` loads that table, activates the workspace, and reloads
  Requests in place: the visible screen never changes. Workspace names and IDs are
  preloaded from the registry so `Tab` completes the identifier even when Workspaces
  has never been visited. Other workspace commands navigate before running; bare
  `:workspace` / `:ws` shows Workspaces. There is no plural `:workspaces` command.
- `Enter` inspects the selected request by focusing its content preview. The summary
  shows method, URL including configured parameters, and shortened request ID.
- Authentication displays the configured source, type, injected header and token/cache
  status. It does not fetch credentials merely to inspect them or show credential
  material. Secret-reference names are checked through Core without access stamping.
- Request preview tabs: Body, Headers, Params. Response tabs: Body, Headers, Network,
  named the same on the inline pane and on the full-screen view. Network contains actual
  status, duration, size, HTTP version, warnings and failures;
  no fictional network trace is shown. Inline text previews are bounded to 64 KiB.
  Responses are held in memory per workspace/request identity.
- `s` on the list sends through Core using its existing default HTTP/auth behavior.
  A full-screen response view owns input and stays open after completion. It is built as a
  screen: the shell header naming the request, a three-row bar carrying
  the method and URL opposite an animated pulse and live elapsed time while the send is in
  flight and the finished status, duration, size and HTTP version once it lands, then Body,
  Headers and Network titles notched into the rule that closes the bar. The bar keeps its
  three rows either way, so completing a send changes what it says rather than how tall it
  is. Network shows status, HTTP version, elapsed time, received body bytes, time to
  headers, body-download duration and average body read rate, in the `Key: value` form the
  Headers page uses. These are measured HTTP timings, not DNS/TCP/TLS phase estimates.
  A failure is reported once: short in the bar, in full on the pages. The view opens with
  Body focused, and `s` sends the request again from inside it — the same letter the list
  sends with — queued through the update loop and unavailable while one is in flight.
- A typed send from another screen opens this same full-screen response as a transient
  child. Closing it returns to the screen that issued the command; the shell keeps that
  source on a stack rather than assuming it was Workspaces.
- Request names containing spaces are entered as one quoted argument, for example
  `:send "monthly report"`. Completion inserts the quotes automatically. Core rejects
  double quotes in request names on both create and save so every persisted name has an
  unambiguous quoted command representation.
  `Escape` cancels an active send, from a re-send as much as from the first one; afterward
  it returns to Requests. Timeouts, transport errors and cancellation remain visible.
  Ctrl+C retains cancellation/exit.
- `v` on the inline response expands the cached result without sending again.
  With Body focused, `b` toggles JSON beautification/minification and `y` copies the
  entire body in its current formatting, including text beyond the inline preview.
  Invalid JSON remains unchanged with an explanation; unavailable clipboard access
  reports failure. Pages are cycled by `t` on the inline pane, where `Tab` belongs to the
  screen's regions, and by `Tab` in the full-screen view, where those titles are the only
  ones on the rule and `t` keeps working beside it, both named in the one hint. Scrolling
  retains the shared native/Vim controls in both, though the full-screen view does not
  spend footer row on advertising them.
- `c` creates a request, `e` edits the selected one, and `y` copies it, all through the
  shared resource editor: a full-screen form with Request, Headers, Params and Body notched
  into the rule, `Tab` between fields, `Ctrl+T` or a click to change page, `Ctrl+S` to save and
  `Escape` to close — asking first when there are edits to lose. Every gesture that reaches
  across the form carries `Ctrl`, because a bare letter belongs to whichever field has focus. The bar shows the method and
  URL as the fields change them, with the parameters folded in, opposite an unsaved marker.
  A copy opens with its source's name cleared rather than pre-filled, because a name of its
  own is the one thing it has to be given. Saving reloads and selects the result and reports
  on the list's footer; a save Core refuses keeps the form open holding the work.
  Name and URL are checked before the save is attempted, and a name containing a double quote
  is refused here rather than by Core, since Core would refuse it anyway.
  An auth the workspace no longer holds, and a method the list does not offer, both stay
  selected rather than being quietly replaced.
- Each body type keeps its own content, so switching type puts away what the editor held and
  fetches what the new type left behind. Choosing a type writes `Content-Type` and the Headers
  page re-reads it. Form and multipart bodies are edited as the fields they are; a multipart
  part may be text or a file chosen through the shared file browser, stored as `@` plus its
  path, checked when chosen, and shown red once the file is gone. Everything else is one
  document, shown on the page the way the previews show one and written in `$EDITOR`: `Enter`
  or `Ctrl+E` opens it there under the extension its type implies, and what is saved comes
  back byte for byte. A body that does not exist yet opens on the document it is about to be —
  `{`, an indented line, `}` for JSON, the declaration line for XML — and JSON kept on one line
  opens laid out over lines; a body returned unchanged leaves the request unchanged. The form gives up the terminal for the run and takes it back afterwards
  on the page it left, with the rest of the form untouched; without `EDITOR` set it says so
  rather than offering an editor of its own, as the CLI has always done for the same work.
- `Ctrl+E` on the list, and `:json [name]`, open the request's file in the configured editor.
  That is the advanced route, for a field the form does not offer or an edit easier made as
  text; it is secondary in the footer because the form is how a request is normally changed.
  `e` on a request that cannot be read opens the same editor, since a file that does not parse
  cannot be loaded into fields at all. A valid edit saves through Core; invalid request JSON
  or identity is written back for repair rather than discarded. Editing either way clears that
  request's cached response and reloads selection.
- The three movable divider shares load from and save to the `Requests` entry in
  `StraumrOptions.PaneLayouts`. Invalid persisted shares are clamped to the layout's
  supported range.
- Startup opens Requests when options contain a valid active workspace; with no active
  workspace it continues to open Workspaces.
- Deleting a request, request import/export and separate Auths/Secrets screens are not
  implemented by this checkpoint. No inert hints advertise them.

## Evidence and resume points

- The resource editor builds in Debug and Release across the solution and in the Release
  CLI-only configuration, all without warnings, and CLI `--help` still runs after the shared
  helpers moved. Nothing about it has been seen in a terminal. No probe was built for it: the
  first `c` answers more in one keystroke than a snapshot checker would, and the developer
  asked for that trade. What needs their check, in rough order of how likely it is to be
  wrong: the form's layout at 120x30, 80x24 and a short terminal; whether `Ctrl+S` reaches the
  app or is eaten as XOFF; the `Select` dropdown popups over a full-screen dialog; the code
  editor taking `Tab` as field movement rather than indentation; the file browser opening from
  inside the multipart dialog, which is a modal over a modal over a modal; and the unsaved
  confirm on `Escape`.
- Not yet done, and deliberately: `:new` and `:edit <name>` command forms, a delete gesture,
  and any use of the kit outside Requests. The kit was written for auths and secrets but has
  only ever been driven by one resource, so treat its shape as unproven until A1 uses it.

- Focus and footer hints were taken from a running in-memory app (`.tmp/response-focus`),
  since a snapshot renders the unfocused state and a `CommandBar` renders empty without one:
  opening a cached response leaves the Body page holding focus, the footer reads
  `b Beautify / minify`, `y Copy body`, `Escape Back` and `s Send again` at 120 columns, and
  a re-send returns the bar to `IN FLIGHT` and the footer to `Escape Cancel`. The probe
  drives no send, so nothing there reaches the network. At 120 columns the shared scroll
  hints take the head of that row and `t Next tab` falls off the end; the titles on the rule
  carry the same meaning, so the hint was left where it is rather than promoted past the
  view's own actions.
- The cohesion pass over the full-screen view builds in Debug across the solution without
  warnings. A layout-only checker (`.tmp/response-layout`) captured it at 120x30, 80x24 and
  44x14 while sending, on success and on a transport failure; the method token keeps its
  width at every size and the URL is what gives way. Re-running `.tmp/r1-check` left the
  Workspaces screen and the one-, two- and three-line list snapshots byte-identical, so the
  shared header and footer insets moved without moving anything. The snapshot renderer does
  not apply focus, so the focus chip on the selected title, clicking a title, and the pulse
  still need the developer's terminal. `.tmp/response-check` predates the view's new
  workspace argument and needs its constructor call updated before it will run again.
- The full-screen response change builds in Debug and Release, including CLI-only.
  A local snapshot/behavior checker (`.tmp/response-check`) covers sending, success,
  formatted body and timeout at 120x28, 80x24 and 44x14; JSON toggling, >64 KiB body
  retention, invalid JSON, metrics consistency, cancellation propagation and transport
  failure passed. Terminal animation, full-screen resize, focus, tab switching and
  clipboard integration still need the developer's interactive check.
- Developer feedback: "It seems to work. I got some gripes but it works." Checkpoint
  requested to conserve usage. The first gripe was described on 2026-09-14 and is fixed:
  Tab reached two positions where no region was titled, and the resize keys there moved a
  divider belonging to somewhere else. See [changelog.md](./changelog.md) for the three causes. Awaiting
  their terminal check of the new cycle and the rest of the gripes. This feedback is general functional acceptance,
  not separate confirmation of every send, cancellation and editor failure path.
- Debug solution build passed without warnings. The final shared-grid adjustment also
  built successfully through the local snapshot checker; rebuild the host before the
  next interactive pass so it includes that last adjustment.
- A focused in-memory-host probe confirmed that startup selects Requests for an active
  workspace and that a configured 42/53/61 layout is restored; moving the panel divider
  saved 46/53/61 exactly once through the options service, and the generated JSON metadata
  round-tripped that per-screen entry. Debug and Release solution builds and the Release
  CLI-only build pass without warnings after these changes.
- One local checker, `.tmp/r1-check`, captured the existing one-, two- and three-line
  lists before/after: identical. Workspaces remained identical at 120x28 and 80x24;
  its heading alignment changed at 70x20 when the two grids became one. Requests was
  rendered at 120x28, 80x24, 70x20 and 160x40, with framework SVG captures as well.
  The checker is a local diagnostic, not a committed test suite or an input harness.
- Still to verify: send success and HTTP/transport failures, cancellation of a slow
  send by Escape and Ctrl+C, external-editor save/repair/focus, retained state when
  switching workspaces/screens, and populated narrow/short layouts. Timeout handling
  now distinguishes user cancellation from HTTP timeout and keeps either in the view.
- Terminal-check direct `:send`, in-place `:ws use` / `:workspace use` with identifier
  completion and refreshed Requests, plus `:rq send` / `:request send`, including the
  no-active-workspace footer error. Their Debug and Release builds pass without warnings.
- Review edit recovery when Core refuses a syntactically valid edit (for example a
  duplicate name): the current code reports the refusal but does not retain that draft.
- A fresh Native AOT publish has not been run. W8's successful publish predates these
  changes and must not be counted as R1 evidence.
- Continue with small, reviewable changes. Prefer the developer's quick terminal checks
  over adding input probes. Preserve the accepted Workspaces design and extend shared
  components when the behavior is common.
