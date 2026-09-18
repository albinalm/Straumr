# Code Structure and Runtime Boundaries

Part of the [TUI implementation guide](./README.md). Read before adding a file,
deciding where something lives, or wiring a screen into the host.

## Straumr code rules

- Match the feature-oriented structure used by `Straumr.Console.Cli`.
- Inject Core interfaces instead of resolving services throughout the visual
  tree.
- `~/.straumr` holds two files and they are not interchangeable. `state.json` is the
  program's: the registries, the current workspace, pane layouts, rewritten whenever any
  of it moves. `settings.toml` is the reader's: written by hand, read here and never
  written back, so nothing may serialise over it. A new value belongs in whichever file
  matches who writes it.
- Keep integration setup in `Integration`.
- Keep navigation and shared application state in `Infrastructure`.
- Keep each screen and its screen-specific presentation models together.
- Put genuinely shared visuals and formatting helpers under `Visuals/Shared`.
- Use TUI presentation models when a Core model does not directly represent the
  information shown on screen.
- Do not add comments unless they explain an implicit behavior or a constraint a
  future developer could reasonably miss.
- Prefer short methods that construct one coherent region or perform one
  operation.
- Do not add interfaces or callback abstractions until there is more than one
  concrete consumer or a real test boundary.

Current structure:

```text
Straumr.Console.Tui/
  Integration/
    TuiConsoleIntegration.cs      host: DI registration, Terminal.RunAsync, exit code
  Infrastructure/
    StraumrTuiApp.cs              shell, retained screen navigation, commands, footer
    ThemeSelection.cs             applies the settings' theme; says when a rebuild is owed
    ITuiScreen.cs                 shared contract implemented by every screen
    TerminalViewport.cs           the terminal's size, read reactively from the app's root bounds
    SecretReferences.cs           which secrets a resource refers to, and which of them exist
    TuiScreen.cs                  screen enum; its name renders in the header
    CommandPrompt.cs              the `:` prompt: open, close, focus, completion
    TuiCommand.cs                 one typed command and its result
    TuiCommandSet.cs              the command table: resolution and completion
  Screens/
    Secret/
      SecretScreen.cs             global store, masked inspection, commands and editor handoff
      SecretScreenItem.cs         readable/broken secret presentation model
      SecretEditor.cs             Name and Value over the shared editor kit
      KnownSecretReferences.cs    non-stamping, cross-workspace reference index and scan coverage
    Workspace/
      WorkspaceScreen.cs          data loading and the parts unique to Workspaces
      WorkspaceScreenItem.cs      presentation model over StraumrWorkspace + entry
      WorkspaceDeleteDialog.cs    destructive confirmation for workspace deletion
      WorkspaceFormDialog.cs      create/copy form and local validation
    Auth/
      AuthScreen.cs               loading, filtering, inspection, fetching and editor handoff
      AuthScreenItem.cs           readable/broken auth presentation model
      AuthEditor.cs               an auth's editor pages and fields, over the shared kit
      ExtractionHelpDialog.cs     what the three extraction sources do, their syntax and examples
    Request/
      RequestScreen.cs            loading, filtering, inspection, sending and editor handoff
      RequestScreenItem.cs        readable/broken request presentation model
      RequestResponseView.cs      fullscreen send activity, response tabs and network metrics
      ResponseBodyActions.cs      body-scoped JSON formatting and full-text clipboard actions
      RequestAuthentication.cs    auth metadata and secret-reference availability
      RequestEditor.cs            a request's editor pages and fields, over the shared kit
  Visuals/
    Shared/
      FocusScope.cs               the two focus questions Visual answers only about itself
      ResourceScreenLayout.cs     the list-and-detail screen scaffold
      ResourceFilter.cs           inline `/` filtering and focus behavior
      ResourceList.cs             one- to three-line list with selection, hover and scrolling
      ResourceRow.cs              presentation model for one list row
      ScrollableContent.cs        focusable read-only content with Vim scrolling
      PagedPane.cs                page titles notched into a rule over one pane at a time
      PreviewPane.cs              retained, styled tabs over scrollable text previews
      FieldList.cs                label/value grid for detail panes
      FormTextBox.cs              the single-line field every form is built from
      BrowserDialog.cs            shared filesystem browser behavior
      FolderBrowserDialog.cs      folder-selection specialization
      FileBrowserDialog.cs        filtered-file selection specialization
      PathCompletion.cs           filesystem completion for browser path entry
      TextPromptDialog.cs         a modal asking for one line of text
      ConfirmDialog.cs            a modal asking one irreversible question
      StraumrDialog.cs            shared modal construction and cancellation
      StraumrHeader.cs            the screen header bar
      StraumrSurfaces.cs          dividers, bars, insets
      StraumrStyles.cs            the facade every colour is read through
      StraumrStyleSet.cs          every control style, built from one palette
      SecretList.cs               the Secrets region: references and their availability
      InFlightPulse.cs            the pulse and ticking duration a bar shows while waiting
    Shared/Editor/
      EditorField.cs              one labelled value, and the field kinds resources are made of
      EditorForm.cs               one page of fields, its layout and its validation
      KeyValueField.cs            a name/value map edited as a list
      KeyValuePairDialog.cs       adding or changing one pair, text or a file
      ContentField.cs             a body, shown here and written in $EDITOR
      BodyFields.cs               the fields a body page is made of, for anything that has one
      ResourceEditorView.cs       the full-screen editor a resource is created and changed on
    Theming/
      StraumrPalette.cs           the eighteen colour roles a theme names
      StraumrTheme.cs             a resolved theme: its name, palette and warnings
      StraumrThemes.cs            resolving a built-in name or a theme file
      BuiltInThemes.cs            `terminal` and `straumr`, as the TOML they ship as
      ThemeDocument.cs            a theme file as it is written
      ThemeColor.cs               `#rrggbb`, `default`, a palette name, `indexed:N`
  Formatting/
    TimestampFormatting.cs        relative and absolute timestamps
    CountFormatting.cs            pluralised counts
    PathFormatting.cs             home-shortened paths
    HttpMethodFormatting.cs       looks each method's colour up in the theme
    AuthFormatting.cs             how an auth reads: its type, its meta line, its status
    ContentFormatting.cs          bounded JSON/text previews, headers and response sizes
    JsonHighlighting.cs           a line of JSON split into the theme-coloured runs it draws as
```

Add a file only when it owns meaningful behavior.

The existing `RequestList` manually implements layout, scrolling, selection,
pointer input, and rendering. Treat it as prototype code, not the pattern for new
screens.

## Runtime Boundaries

`TuiConsoleIntegration` should remain a thin host:

- register the TUI's dependencies — the shell and its screens scoped, everything they
  depend on singleton, so a theme change can replace one scope and keep the rest
- read the settings and apply the theme before the shell is constructed, because every
  visual is handed its colours as it is built
- construct the application root, and rebuild it in a fresh scope when the shell reports
  that the palette changed, resuming on the screen the reader was on
- run it with `Terminal.RunAsync` and hand the loop's `TerminalApp` to the root, which
  needs it for global commands and focus
- translate application exit into the process exit code

The TUI integration must register the Core services it requires and must not
depend on CLI registration as an accidental side effect.

The application root should own:

- current screen
- active workspace display state
- command prompt visibility and text
- the command table, composed from its own commands and the current screen's
- what the footer row is showing, and expiring a command's message
- top-level commands and exit state
- focus restoration when screens or overlays change

A screen should own:

- its loading, loaded, empty, and error state
- its selected index or selected item
- data loading and refresh after screen-specific operations
- visuals and commands that belong only to that screen, gestures and typed commands
  alike
