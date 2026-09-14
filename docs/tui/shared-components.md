# Shared Building Blocks

Part of the [TUI implementation guide](./README.md). Read before writing any screen
visual: the list, filter, layout, panes, dialogs and the command prompt already
exist here. Extend a shared piece rather than hand-rolling a variant.

## Screen scaffold

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

## Movable dividers

`PaneSplits` holds the dividers one screen can move, and a screen owns one instance for
the life of its retained tree, so a divider a user has moved survives selection changes,
filtering and navigation. Each `PaneSplit` is the first region's share of the pair and
drives a bound `GridLength.Star` weight rather than a cell count, so it keeps its
proportion when the terminal is resized. Definitions are bound rather than assigned
because a screen that rebuilds its detail sections hands the grid new definitions each
time while the split has to outlive them.

Screens that persist their dividers seed `PaneSplits` from the entry named for that
screen in `StraumrOptions.PaneLayouts`, then save all three shares when one changes.
Keeping the entries keyed by screen prevents a Requests layout from becoming the
default for another screen when movable dividers are added there later.

`Ctrl` with a Vim direction moves a divider that way. Which divider depends on where
focus is: in the list panel the horizontal keys move the panel divider, in the detail
panel they move the one between its two sections, and the vertical keys move the rule
over a stacked pane, which only Requests has. The commands sit on the screen layout
rather than on the app, so a dialog's focus chain never reaches them, and they stand
down while the filter is being typed into — a terminal sends `Ctrl+H` as the byte
`Backspace` arrives on, so a live command there would eat the filter's own deletion.
Only one of the four is presented in the footer, because that row already carries the
screen's own actions and four hints for one family of keys would crowd them out.

`FlexiblePane` is what makes those weights mean anything. A `Star` weight is only a
weight: a child reporting a large minimum width still takes what it asks for and its
sibling shrinks to whatever it will accept. `TabControl` asks for the whole row, which is
why the 48/52 authentication and request split rendered as roughly 17/83 and the
authentication values wrapped four characters wide. `Pane` wraps every pane in one, so
the content measures at the width it is given and the declared weights decide.

## Lists and filtering

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

## Filesystem browsers

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

## Scrollable content and forms

`ScrollableContent` owns focus and scrolling for retained read-only content such as
the recent Requests preview. It exposes contextual `j`/`k`/`g`/`G` commands while
arrow, Home/End, Page and wheel input update the same bindable offset. Those commands
are hints and nothing else — `OnKeyDown` is where the keys are handled — so a region
sharing the footer with a screen's actions can take `hints: false` and keep every key.

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

## Preview panes and full-screen views

`PreviewPane` is the tabbed read-only text pane: one `ScrollableContent` per page and
`SetText`/`SetPageText` to replace a page's text without disturbing the others. Its
default form puts the page titles on a framework tab strip and cycles them with `t`,
which is what a pane inside a titled section needs, `Tab` there belonging to the screen's
regions. `PreviewPane.OnRule` puts them on a `Rule` the caller places instead, for a pane
that is a whole screen: the titles are then where every other title is, the selected one
carries the focus chip while the pane owns focus, a title is a clickable chip that hands
focus to the page it selects rather than keeping it, and `Tab` and `Shift+Tab` step
between them — nothing else on that rule can be stepped to, so `Tab` keeps meaning "move
the chip along the rule" (`t` still works, and the hint names both keys, the second one
painted in the bar's key colour through `StraumrStyles.KeyMarkup`, because a bar renders
one gesture per hint and nothing at all for a gestureless one). Only the selected page is
visible, and visibility is set outright rather than bound, because focus is revoked from a
visual that is invisible during the focus pass. Such a pane also builds its
`ScrollableContent` with `hints: false`: the footer row it shares with the screen's own
actions is one row, and four movement hints would crowd them out, as they did.

`StraumrDialog.CreateScreen` is the modal such a view sits in: the full terminal, no
frame title, no padding, so its rules run to the frame exactly as the shell's do and
the view names itself with `StraumrHeader.Create`, whose second overload takes the name to
show — the screen, or for a view of one resource, that resource.
`StraumrSurfaces.RowInset` and `ResourceScreenLayout.PaneInset` are the two paddings
involved, shared so a full-screen view lands its rows where a panel lands them.

## Field lists

`FieldList.Create` builds a detail pane's label/value grid. Use `FieldList.Count`
for quantities so they inherit the amber-when-populated rule, `Wrapped` for values
long enough to wrap such as paths, and `Text` otherwise.

## The command prompt

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
