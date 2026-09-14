# Headless Verification

Part of the [TUI implementation guide](./README.md). Read before proving a change
without the developer's terminal: what snapshots can answer, what needs a running
`TerminalApp`, and when to stop automating and just ask.

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
