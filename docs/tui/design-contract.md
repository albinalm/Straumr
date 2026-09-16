# Design Contract

Part of the [TUI implementation guide](./README.md). Read before changing anything
visible: the shell, a bar, a rule, a colour, focus cues or the footer. It defines
what the approved mockups mean and why the shell looks the way it does.

The mockups define information hierarchy and intended interaction, not a mandate
to reproduce browser CSS or pixel geometry. Prefer native terminal controls and
theme styles that produce the same structure.

Shared screen shell:

- A single window frame encloses the whole screen. Regions inside it are
  separated by one-cell rules, not by nested boxes with gaps between them.
- The whole app shares one background. Regions are told apart by dividers alone.
  Panel tints were tried twice and removed both times: a wide step made a boundary
  read as clipped, and a narrow one was not worth the seam it still produced.
- The background is whatever the theme says, and the accents are chosen to carry it.
  A badge is the only surface allowed to sit above it, and scroll bar chrome is kept
  below the dividers so it never competes with content. The contract below describes
  relationships between roles — brighter, fainter, inert, vivid — not particular
  colours; a theme supplies the colours and is judged on whether it keeps the
  relationships.
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
  workspace is the only green on screen. The names are roles, not instructions: a
  theme answers "amber" with whatever its scheme uses for a value that is present.
- Structure and meaning are separate decisions. Rules, bands, keys and markers may be
  colourless — the terminal theme's are — but anything that tells the reader something
  keeps its colour, and the methods keep five distinct ones. A theme drawing those from
  the terminal's own palette slots imposes nothing, because the slots are the reader's.
- The default theme paints with the terminal's own colours, so the shell must read
  on a scheme it cannot see. Two consequences bind the contract. Nothing may assume
  the background is dark, and the three row levels are only guaranteed where a theme
  names its own background — with the terminal's sixteen there is no tint of an
  unknown ground, so the pointer-hover band is the level that goes.
- The shell responds to focus and pointer. A list owns three row levels, distinct
  from each other: hover is the faintest, a selected row in an unfocused list is
  stronger and drops its accent marker to a neutral one, and a selected row in a
  focused list is the vivid band. Which resource is current is a separate question
  from which row is selected, so its marker is painted over whichever band the row
  carries and composes with all three levels instead of competing with them.
- The hint row wraps to as many rows as the focused region's hints need, and the content
  above gives up the lines. A hint that does not fit is dropped from the end of the row,
  where the least-used actions are; losing those silently is worse than a row that changes
  height. A region that cannot afford the movement pins its own bar to a fixed height, as
  the folder browser does.
- Exactly one region owns focus, and its title says so by filling with the selection
  blue as a chip. Every other title is inert grey. A title that cannot take focus is
  always inert. So there is one filled blue title on screen, and finding it is how
  the eye answers "where am I" without having to compare two similar colours.
- Because every title shares the one rule, the chip only ever travels along it. The
  rule is the screen's tab strip, and focus reads as a step sideways rather than a
  jump between levels of the hierarchy.
- The filled accent surface belongs to focus alone. Quantities and identifiers use
  the recessed badge. Do not spread either further.
- A view that fills the terminal is a screen, not a dialog, and is built from the
  pieces above: no title on its frame, the same header naming it, a three-row bar, a
  pane, and the one-row footer. A view of one resource is named by that resource
  rather than by the kind of view it is, because the kind is already evident and the
  resource is what the reader has to keep track of. It opens with a region focused, as
  a screen does. Where such a view has pages rather than sections, the page titles take
  the place the section titles take: notched into the one rule, the selected one
  carrying the chip while the pane owns focus, and `Tab` steps between them. A titled
  rule over a tab strip of its own would name the region twice and light two cues at
  once, and a page needing a letter of its own would make `Tab` mean one thing on a
  screen with regions and nothing on a screen with pages.
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

- [Requests mockup](../design/straumr-requests-layout.html)
- [Workspaces mockup](../design/straumr-workspaces-layout.html)
- [Auths mockup](../design/straumr-auths-layout.html)
- [Secrets mockup](../design/straumr-secrets-layout.html)
- [Combined secondary screens](../design/straumr-other-screens.html) for overview
  only; the individual files above are authoritative

