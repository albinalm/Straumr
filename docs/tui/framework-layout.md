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

- Use the approved Straumr palette through shared control styles, declared as hex
  literals in `Visuals/Shared/StraumrStyles.cs`. Avoid ad hoc colors outside the
  shared palette except for semantic mappings such as HTTP methods.
- Style every framework control that paints chrome of its own. `ScrollViewer`
  defaults to a bright grey track and thumb that does not belong to the palette.

## Custom visuals and framework gaps

- `Visual.Invalidate` is obsolete. Drive every visual state change through a
  `[Bindable]` partial property so the app invalidates on its own; the framework's
  own `TreeView.HoveredIndex` is the pattern for pointer state.
- `Theme` is immutable and can only be built by `Theme.FromScheme` from a
  16-color `ColorScheme`, so the palette is applied per control style rather than
  through the theme. Unstyled filler cells therefore keep the framework theme's
  near-white foreground; it is invisible on blank cells, but a Straumr
  `ColorScheme` is the eventual fix and any new visible text must be styled
  explicitly until then.
- Introduce a custom `Visual` only after confirming that composition, templating,
  or styling cannot express the requirement.
- Version 3.9.0 has no vertical rule control, so the column divider is painted
  through a `Canvas` painter in `Visuals/Shared/StraumrSurfaces.cs` rather than
  through a new `Visual`. There is no per-visual background either, which is a
  second reason to keep one background for the whole app.
- Do not use `Header` for the screen bars. It forces bold slot text and its own
  surface color, which the approved layouts do not use. `StraumrSurfaces.Bar`
  composes the same left/right arrangement from a `Grid`.
