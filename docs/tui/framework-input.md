# Framework Rules: Focus, Input and Commands

Part of the [TUI implementation guide](./README.md). Read when touching a gesture, a
key, focus, modality, the command bar, the command prompt or fullscreen hosting.
Most of these are hard-won 3.9.0 constraints; check here before assuming a
framework behavior.

## Commands and gestures

- Use framework commands and gestures for actions exposed in the command bar.
- Keep key handling local to the control that owns the interaction.
- Version 3.9.0 has no typed-command surface. `Command.Name` is documented for
  "a future command prompt" and `Command.Execute` receives only the target visual,
  so a command taking an argument cannot be expressed as a framework `Command`.
  Typed commands therefore live in `TuiCommandSet`, while every gesture stays a
  framework command.
- Unique-prefix matching is a per-command policy. Screen navigation opts out: only the
  canonical `workspace` / `request` names and their explicit `ws` / `rq` aliases may
  navigate. In particular, `w` remains a local selection alias where present and never
  becomes an accidental abbreviation for `workspace` elsewhere.
- Register an app-wide gesture with `TerminalApp.AddGlobalCommand`. Command
  discovery collects global commands alongside the focus chain's, so a global
  command still shows in the `CommandBar`.
- `KeyGesture` compares modifiers for equality, and `new KeyGesture(':')` carries
  `TerminalModifiers.None`. A terminal that reports Shift for shifted punctuation
  therefore never matches it, so a character that needs Shift has to be registered
  twice, once bare and once with `Shift`. Only one of the two should be presented
  in the `CommandBar` or the hint appears twice. A control that reads `e.Char` in
  `OnKeyDown` sidesteps this, but only if no command claims the key first: see the
  case-insensitive routing rule below, which is why `ResourceList`'s `g` was claiming
  `G` on a terminal that reports no modifier for it.
- Gesture routing matches a character case-insensitively even though `KeyGesture`
  equality does not: `new KeyGesture('g')` and `new KeyGesture('G')` compare as
  different, but a routed `g` command still claims a `G` key event that carries no
  Shift. A control that wants both keys separately therefore has to keep the hint and
  handle the key itself, which is what `Command.RouteGesture = false` is for.
- A terminal sends `Ctrl` plus a letter as the single C0 byte the letter maps to, so a
  `Ctrl`+letter gesture has to carry that control character —
  `new KeyGesture((char)('L' & 0x1F), TerminalModifiers.Ctrl)`, not
  `new KeyGesture('l', TerminalModifiers.Ctrl)`, which matches nothing. The wrong form
  gives itself away in the command bar: the control character renders as `Ctrl+L` and
  the letter as a lowercase `Ctrl+l`. Measured against 3.9.0's decoder, the raw-byte
  path (Alacritty, xterm, Windows Terminal) and the CSI path (kitty keyboard protocol)
  both arrive as the control character plus `Ctrl`, so one gesture covers both; a host
  that reported the letter and the modifier separately would need the letter form
  registered beside it, unpresented.
- `Ctrl` or `Shift` plus a *named* key is not portable. A plain terminal sends
  `Ctrl+Enter` as a bare `Enter` — 3.9.0's decoder maps the byte to
  `Key=Enter, Modifiers=None` — so such a gesture works only where the kitty keyboard
  protocol or xterm's `modifyOtherKeys` is active, and is advertised everywhere else
  while doing nothing. Prefer a plain character for an accelerator.
- A plain-character gesture must be registered on the control that owns it, not on an
  ancestor. Routing walks the whole focus chain, so a character command on a dialog
  fires while a text field inside it has focus: `/` on the dialog opened the filter
  instead of reaching the path being typed.
- A global command is collected alongside the focus chain's rather than from it, so
  modality does not suppress it the way it suppresses a gesture on a visual. An
  app-wide gesture that should not reach a dialog has to say so itself, by walking up
  from `TerminalApp.FocusedElement` for an `IModalVisual`. A `CommandBar` re-collects
  on invalidation rather than every frame, so a gate that changes with focus takes
  effect on the pass focus moved and not before.
- A command on the focused control wins the gesture over a command with the same
  gesture on an ancestor. So a dialog's `Escape` does not have to be gated for a text
  field inside it to claim `Escape`; gating it is only worth doing to stop a second
  hint for the same key appearing in the command bar. Gating by `CanExecute` plus
  `ConsumesGestureWhenUnavailable = false` also works and falls through to the focused
  control's own `OnKeyDown`.

- A command whose gesture is `Tab` claims the key before focus traversal gets it, so a
  surface with nothing else to step to can give `Tab` its own meaning: the full-screen
  response cycles its pages with it, and `Shift+Tab` cycles back. `Shift` plus a named
  key is portable here where `Ctrl+Enter` was not, because a terminal sends back-tab as
  its own sequence and 3.9.0 decodes it as `Key=Tab, Modifiers=Shift` — which is how the
  framework's own backwards traversal works. Measured on a running app: `Tab` moved the
  selected page and focus followed it, rather than traversal moving focus somewhere else.

- A `CommandBar` hint is one keycap plus a label, and the keycap is rendered from the
  command's gesture: there is no way to give it text of its own, and a command with no
  gesture is not rendered at all — measured, it simply vanishes from the row. A hint that
  has to name a second key therefore carries it in the label, which is parsed as ANSI
  markup (`[#RRGGBB]…[/]`, `[bold]`, `[/]` closing the last tag). `StraumrStyles.KeyMarkup`
  paints such a key in the bar's own key colour, so `Tab /t Next tab` reads as two keys and
  one label. The keycap's padding still separates them by a space; it belongs to every hint
  in the app through `CommandBarStyle`, not to this one.

## Focus and modality

- `Visual.App` is null until the app is running, so anything that needs the
  `TerminalApp` (focus, global commands) has to happen from input or from the
  update loop, never from a constructor.
- Overlay visuals that must keep their identity in a `ZStack` and drive each one's
  `IsVisible`. `ContentSwitcher` looks like the control for this and is the wrong
  one: it attaches only the selected child, so the others have no `App` and cannot
  be focused. A visual the app has to focus also cannot be rebuilt by a
  `ComputedVisual` each frame.
- A binding is re-evaluated when something it read changes, and only the framework's own
  bindable values are read through the binding graph. A function bound over a plain
  object — the state a form is editing, say — is therefore evaluated once and never again:
  the editor's Body page went on saying the request sent no body after a type had been
  chosen for it, and its bar went on saying `GET` after the method had been changed.
  Two ways out, and which one depends on what is stale. Visibility, and anything else the
  focus pass depends on, is assigned outright from the update pass — `PagedPane` with its
  pages, `EditorForm.Sync` with its fields. Everything else is mirrored into a `State<T>`
  once per pass and left bound as normal, which is what the editor's bar and header do.
- A style given as a function is not a binding at all: it is stored in the visual's style
  environment and invoked by `GetStyle` during render, so it re-evaluates every frame no
  matter what it reads. A dynamic text provider is a binding. The two disagreeing over the
  same plain value is what made the stale bar read as `GET` in `PUT`'s colour.
- Focus is revoked from a visual that is not visible when the focus pass runs, and
  a `[Bindable]` computed only takes effect during that pass. So a visual that
  takes focus the moment it appears has to set its own `IsVisible` imperatively
  first; a binding that turns it visible later is too late and focus falls back to
  whatever claims `AutoFocus`.
- `Visual.HasFocus` is a bindable mirror of `TerminalApp.FocusedElement` and lags
  it by an update pass. Compare against `FocusedElement` when the answer is needed
  in the same frame focus moved.
- `Visual.HasFocusWithin` excludes the visual itself: it answers "does a *descendant*
  have focus", so a control that takes focus directly reports `false` for its own focus.
  A title bound to a focusable control's `HasFocusWithin` therefore goes dark exactly
  when that control is focused. `FocusScope.Owns` is `HasFocus || HasFocusWithin` and is
  what every focus predicate asks.
- One update pass may not both read and write the same bindable value: doing so throws
  `Cannot read and then write X within a same tracking context` out of `RunComputedProperties`.
  A computed `IsTabStop` over an ancestor's `IsVisible` is a read of it, so that ancestor's
  visibility cannot also be computed — it has to be assigned. The rule to follow is that the
  visibility of a subtree holding a focusable is assigned and never bound, which is what
  `PagedPane`, `EditorForm.Sync` and `KeyValuePairDialog` all do. Binding the visibility of text
  stays fine, since nothing reads it back.
- Tab traversal tests the candidate's own `IsVisible` and not its ancestors', so a
  focusable inside a subtree hidden by its root stays in the rotation. `IsVisible = false`
  on the inactive screen's root did not take its list out of the Tab cycle, and neither
  did `IsEnabled = false`: both were measured. A focusable that can be hidden by an
  ancestor has to compute its own `IsTabStop` from the whole chain, which
  `FocusScope.IsReachable` does; reading each ancestor's `IsVisible` registers them all
  with the binding graph, so the computed value updates when a screen is shown or hidden.
  This applies to every focusable and not only to the app's own components: a framework
  `TextBox`, `Select` or `Switch` used raw is a Tab stop wherever it sits, so the editor's
  hidden pages answered Tab with a caret in the middle of the visible page and swallowed
  everything typed into it. `FormTextBox`, `ChoiceField` and `ToggleField` now compute it.
- `AutoFocus` needs the same answer, on a path `IsTabStop` never reaches. Every render, an
  app whose `FocusedElement` has gone null takes the first focusable claiming `AutoFocus`,
  and that search is ancestor-blind too. A screen waiting in the shell's `ZStack` therefore
  caught every stray focus in the app — focus lost to a page hidden under the caret, or to
  a click landing on nothing — and took the footer's commands with it while staying
  invisible. A screen's main region claims `AutoFocus` through `FocusScope.IsReachable`.
- A control that hides the page focus is on must take focus with it, in the same keystroke.
  The framework revokes focus from what is no longer visible and re-homes it to whatever
  claims `AutoFocus`, and a single frame of that is visible: the screen appears to flicker
  to wherever focus went and back, and the footer loses the page's keys while it is away.
- Which rules out letting a control swap the page. `TabControl` hosts the selected page
  alone and attaches the next one on the app's own next pass, and it cannot be hurried:
  `EnsureChildrenPrepared` runs only when the control is marked out of date, and that
  marking happens on the pass that would have attached it anyway. Keep every page attached
  instead and drive visibility directly — a `ZStack` of all of them, one made visible
  outright, which `PagedPane` does and `PreviewPane` now does by handing `TabControl` the
  same content visual for every tab. Assigning a content host the visual it already holds
  is a no-op, so the strip, the selection and the styling stay the control's and nothing is
  detached.
- `TabControl` is focusable, so its tab strip is a Tab stop separate from the content it
  selects. One titled region then answered two Tabs, and `IsTabStop(false)` on the strip
  is what puts one stop back in each titled region; it stays focusable for the pointer.
- A printable keystroke arrives as two independent terminal events, a
  `TerminalKeyEvent` and a `TerminalTextEvent`, and handling the key does not
  suppress the text. So a character gesture that gives focus to a text control
  hands that control the very character that triggered it. Nothing in the framework
  suppresses the pair, so the control has to ignore the echo itself.
- A surface that should own input while it is up implements
  `Input.IModalVisual`, the interface `Dialog` and `Popup` use. Declaring
  `IsModal` keeps focus traversal inside it, stops gestures on other visuals from
  firing, and swallows pointer input landing elsewhere. Without it a key the surface
  does not handle falls through to `Tab` traversal, focus leaves, and a surface that
  closes on lost focus disappears — which is what `Tab` on a prompt with no
  completion candidate did.

## Dropdowns

- A `Select<T>` has two states and the framework gives both only the arrows. They have to
  be given the Vim keys separately: the closed control directly, since it is an ordinary
  visual, and the open list through `SelectStyle.PopupTemplateFactory`. Both are
  `SelectKeys`.
- `Select<T>` builds the popup's `ListBox<T>` itself and exposes it nowhere else. That
  factory is the one place it can be reached: it is handed the list before the popup is
  shown, so keys can be attached there and the framework's own frame still returned
  around it. A factory that forgets to call the default one silently drops the border.
- The keys are handled from the `KeyDown` event rather than registered as gestures, for
  the reason every other list in this app handles them there: gesture routing matches a
  character case-insensitively while `KeyGesture` equality does not, so a routed `g`
  claims `G` with it. Assigning `SelectedIndex` is the whole of the movement — the list
  scrolls to what becomes selected, and the popup's selection is bound back to the
  dropdown, so these keys change the value exactly as the arrows do.

- The framework turns an unhandled `Tab` into focus traversal itself, forwards or backwards on
  `Shift`, and exposes neither direction as a method. An app that wants a second gesture for
  "go back" has to walk the tree for itself: the window holding focus is the scope, and every
  visual that is focusable, visible, enabled and a tab stop is a stop, in tree order.
  `Visual.EnumerateVisualsDepthFirst` takes a parent before its children where the framework's own
  walk takes children first; the two agree unless a focusable contains a focusable.

## The prompt editor

- Build the command prompt on `PromptEditor`, which already owns prompt prefixes,
  history, completion hooks, accept and cancel. Three of its constraints matter:
  its prompt column has a minimum width of two cells, so a one-character prompt
  renders a trailing blank and `" :"` is what puts the colon in the text column;
  `PromptEditorStyle` has no foreground for the editor's own text, so the palette
  reaches it through the `Highlighter` delegate rather than the style; and
  `PromptEditorCompletionPresentation.InlineCycle` keeps completion inside the
  prompt row, where `PopupList` would float a surface over the layout.
- The completion handler is called once per `Tab` and not while the user types, and
  the framework keeps no cycle state of its own: it re-asks on every trigger. So a
  handler that derives candidates from the current text can only ever offer the one
  it already inserted, and `InlineCycle` cycles nowhere. Cycling means holding the
  candidate list across triggers and recognising a repeat by the text and caret the
  previous completion produced. Because no request arrives between them, that
  recognition is exact.
- Neither `PromptEditorEscapeBehavior` gives `Escape` one meaning: the default
  spends the first press dismissing an active completion and only the second closes
  the prompt, and `CancelCompletionOnly` stops it closing the prompt at all. To make
  one press always close, clear `CancelCommand.Gesture` in the `PromptEditorConfig`
  so no command claims the key, then handle `Escape` in `OnKeyDown` and call
  `Cancel()` before closing so the framework's own completion state is reset too.
- `PromptEditor.Text`'s setter does not raise `OnDocumentChanged`. Only a user edit
  does, so any programmatic change to a prompt's text is invisible to a handler
  hanging off that hook and has to report itself.

## Quitting and fullscreen hosting

- The framework registers its own quit command, `Ctrl+Q` in fullscreen hosting and
  `Escape` inline. `TerminalApp.RemoveGlobalCommand(TerminalApp.DefaultQuitCommandId)`
  takes it out, and takes both the gesture and its command bar hint with it, so an
  app that owns its own exit does not have to live beside a second one. Removing it
  is also what frees `Escape` inline; in fullscreen it was already free.
- A fullscreen `Terminal.RunAsync` owns one `TerminalApp` and tears down its raw-mode,
  cursor, mouse, paste, and alternate-screen scopes when the loop stops. It does not
  stop the underlying `TerminalInstance` input loop, so an external process that needs
  the terminal must run only after `RunAsync` returns and `StopInputAsync` completes.
- Re-entering fullscreen with the same retained tree requires the supplied root to be
  a `WindowLayer`. When given an ordinary visual, `TerminalApp` wraps it in an internal
  `WindowLayer` whose child relationship survives disposal, and the next hosted run
  rejects that visual as already parented. An explicit `WindowLayer` stays parentless,
  while its content retains all screen state and selection across runs.
- Every hosted run creates a new `TerminalApp`. App-wide commands therefore have to be
  registered for each new instance, and focus restoration has to happen after the
  retained tree attaches to that instance. Holding a visual as the restoration target
  is safe; trying to focus it between runs is not, because `Visual.App` is null then.
- Nothing in the `EDITOR` convention says where an editor should open. Only the program is
  agreed on; a caret position is per-editor syntax — `+L,C`, `+L`, `+L:C`, `-l`/`-c`, or the
  position appended to the path — and an editor that does not take one treats the argument as
  another file to create or exits on it. So a position is only ever offered to an editor whose
  syntax is known, and every other one is launched exactly as before.
- A dialog shown with `Dialog.Show` is added to that run's window layer and stays
  parented to it when the run ends; only the app's root is detached on teardown. A
  dialog that has to survive an external program therefore has to be closed before the
  loop stops and shown again on the run that follows, or the next `ShowWindow` refuses
  it as already part of a UI tree. Closing it costs nothing that matters: the state
  being edited belongs to the screen, and the retained pages come back as they were.
