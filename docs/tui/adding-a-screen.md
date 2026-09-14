# Adding a Screen

Part of the [TUI implementation guide](./README.md). The procedure for a new screen,
and what the Requests screen had to add beyond the Workspaces shell.

## The procedure

1. Add the screen to `TuiScreen`; the header renders its lowercased name.
2. Add `Screens/<Name>/<Name>Screen.cs` and, when a Core model does not directly
   represent what is shown, `<Name>ScreenItem.cs` beside it.
3. Load through Core services in a `LoadAsync`, into `State<T>` fields.
4. Map each item to a `ResourceRow` and call `ResourceScreenLayout.Create`.
5. Register the screen in `TuiConsoleIntegration.ConfigureServices` and navigate to
   it from `StraumrTuiApp`.
6. Expose the screen's typed commands as `PromptCommands` so the shared prompt picks
   them up.

Nothing in steps 1-6 touches layout, palette, dividers or row styling. If a screen
needs to, that is a signal to extend the shared piece rather than to hand-roll a
variant.

## Where the Requests screen differs

Requests reuses the accepted Workspaces shell, palette, filter, list, focus cues and
scrolling. Its detail panel adds a response preview below the authentication and
request-content panes through `ResourceScreenLayout.StackedSections`. The shared
`TwoPaneSections` now puts headings and content in one grid so their column divider
cannot drift when the contents have different minimum widths.

`ResourceRow.LeadingToken` carries a `ResourceToken` with text and normal/selected
styles. Requests uses it for HTTP methods in single-line rows; existing rows without
a token retain their rendering. `PreviewPane` composes the framework's borderless
`TabControl` with retained `ScrollableContent` surfaces. It is used for both request
and response content, with `t` to cycle tabs and native clickable tab headers.
Text is composed one line per `TextBlock`, because one wrapped `TextBlock` does not
preserve a JSON document's line breaks and indentation.

`ITuiScreen` gives the shell a concrete second consumer for shared screen hosting:
root, focus target, active workspace name, commands, loading/update and notification/
external-action events. Both roots remain mounted in a `ZStack`; navigation changes
visibility, reloads the destination and restores list focus. The command table is
rebuilt for the current screen. `:request`/`:rq` and `:workspace`/`:ws` navigate
explicitly and can dispatch a destination-screen command after loading it. Nothing
else changes screens: a gesture that did so was removed, leaving the typed vocabulary
as the only way between windows. Each screen keeps `:select` for typed item selection,
with `:r` on Requests and `:w` on Workspaces.

A destination command may opt into running in place from another screen. The shell then
loads the hidden destination, executes that same command handler, and reloads the visible
source instead of changing visibility. Use this only when the source has a meaningful
refreshed view of the result; workspace activation from Requests is the current example.

A command that opens a full-screen transient child marks that fact on its `TuiCommand`.
When it is dispatched from another screen, the shell pushes the source onto a return
stack. The child reports its close through `ITuiScreen.TransientScreenClosed`, and the
shell reloads and returns to whatever screen was pushed. Neither the child nor the
destination screen names Workspaces, so the same close-to-previous behavior composes
with future screens and nested transient views.
