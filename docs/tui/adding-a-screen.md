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
rebuilt for the current screen. `:requests`/`:rq` and `:workspaces`/`:ws` navigate
explicitly, and nothing else does: a gesture that changes screens was removed rather
than left beside them, so the typed vocabulary is the only way between windows. Workspaces keeps `:w` as an explicit alias
for `workspace`, avoiding ambiguity with the new `workspaces` command.
