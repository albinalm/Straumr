# Framework Rules: Composition, Visuals and Styling

Part of the [TUI implementation guide](./README.md). Read when building or changing
visuals, layout or palette. For keys, focus, commands and hosting see
[Focus, Input and Commands](./framework-input.md); for proving a change see
[Headless Verification](./headless-verification.md).

## Composition and state

- Compose the application shell from `Grid`, `Padder`, `Rule`, layout containers,
  and a footer or `CommandBar` where their behavior fits.
- Use `State<T>`, bindings, and computed visuals for changing UI state.
- A binding that reads only `Stopwatch.Elapsed` has no reactive dependency and will
  not refresh as time passes. Timed labels use `IAnimatedVisual` to update retained
  text on the UI thread; the animation scheduler runs even while the screen's async
  update awaits I/O. Its timestamps use Stopwatch ticks (`Stopwatch.Frequency`).
- Use `ListBox<T>` with a `DataTemplate<T>` for single-line resource lists.
- Use a small retained-mode list visual for multiline resource rows; the pinned
  `ListBox<T>` fixes every item to one terminal row and `Button` imposes an
  unsuitable bold, filled treatment.
- Bind list selection to state and derive the detail pane from that selection.
- Let built-in controls own focus, scrolling, pointer input, clipping, selection
  styling, and invalidation.
- Perform I/O outside render, measure, arrange, and per-frame update paths.
- Load once on entry and refresh only after an operation or explicit refresh.
- Show loading, empty, and error states as visuals driven by state.

## Palette and styling

- Read every colour through `Visuals/Shared/StraumrStyles.cs`, which is a facade over
  the style set the current theme built. Avoid ad hoc colors outside it except for
  semantic mappings such as HTTP methods. Do not write a hex literal into a visual:
  the palette is data now, and a literal is a colour no theme can change.
- A style is a value, not a binding. Every framework style is handed to a control when
  it is constructed and captured there, so changing the palette cannot repaint a
  retained tree — the host rebuilds the shell instead. That is why `StraumrStyles.Apply`
  is only ever called before a visual tree is built.
- A new colour means a new role in `StraumrPalette`, and a new role means every theme
  file in existence is missing a key. Add one only when the design contract gains a
  distinction the existing eighteen cannot express, and give it to both built-ins.
- `TextStyle.Invert` paints a surface in the terminal's own colours without naming them, and
  composes: the attribute survives under text drawn over the band and fills a `TextBlockStyle`'s
  padding. It must always be paired with an explicit foreground. A `Style` cannot reset a
  foreground — `Color.Default` is the same value as "unset" — and a surface fill sets only a
  background, so an inverted style with no foreground inherits whatever colour the cell already
  carried and inverts that instead.
- A colour may be `Color.Default` or a `Color.Basic16` palette slot, not only RGB.
  Two framework calls do not accept those: `Brush.Solid` throws on `Color.Default`,
  and `Color.ToHexString()` answers a palette colour with a hardcoded xterm
  approximation and the default with black. Neither may be used on a palette colour
  without handling the kind — see `StraumrStyleSet.SolidOrNull` and `MarkupToken`.
- Style every framework control that paints chrome of its own. `ScrollViewer`
  defaults to a bright grey track and thumb that does not belong to the palette.

## Custom visuals and framework gaps

- `Visual.Invalidate` is obsolete. Drive every visual state change through a
  `[Bindable]` partial property so the app invalidates on its own; the framework's
  own `TreeView.HoveredIndex` is the pattern for pointer state.
- `Theme` is set through the keyed style system, not through a property: there is no
  `Theme` setter on `Visual`, `TerminalApp` or `TerminalRunOptions`, but
  `visual.SetStyle(Theme.Key, theme)` works and inherits down the tree. It is easy to
  miss — the setter is the generic `SetStyle(StyleKey<T>, T)` — and missing it is why
  the first terminal theme rendered as a solid dark rectangle.
- The framework theme decides what every cell no Straumr style reaches is painted with,
  the ground behind the whole app included. `Theme.Default`'s background is `#0f1d27`,
  and it is painted whether or not anything asked for it. `Theme.Terminal`'s background
  and foreground are **null**, which makes the renderer emit no colour for those cells
  at all — that is the only way a transparent terminal stays transparent.
- Straumr sets it on `app.Root` in `StraumrTuiApp.AttachTo`, not on its own tree, because
  dialogs and popups are hosted in layers that are siblings of the shell rather than
  children of it. `StraumrStyles.FrameworkTheme` chooses: `Theme.Terminal` for a palette
  whose background is the terminal's own, `Theme.Default` otherwise, where the window
  group covers the surface anyway.
- A per-control style still carries the palette. Setting the framework theme fixes the
  ground and the filler cells; it does not restyle the controls, so any new visible text
  must still be styled explicitly.
- Introduce a custom `Visual` only after confirming that composition, templating,
  or styling cannot express the requirement.
- Version 3.9.0 has no vertical rule control, so the column divider is painted
  through a `Canvas` painter in `Visuals/Shared/StraumrSurfaces.cs` rather than
  through a new `Visual`. There is no per-visual background either, which is a
  second reason to keep one background for the whole app.
- Do not use `Header` for the screen bars. It forces bold slot text and its own
  surface color, which the approved layouts do not use. `StraumrSurfaces.Bar`
  composes the same left/right arrangement from a `Grid`.
