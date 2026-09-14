# Validation Checklist

Part of the [TUI implementation guide](./README.md). Run the smallest applicable
subset for the milestone in hand, and tick items here in the same change.

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
