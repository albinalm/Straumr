# Visual System Module

## Purpose

Define the visual contract and theming framework for `straumr-tui` so the Go TUI feels recognizably like the existing `Straumr.Console.Tui` instead of a generic Bubble Tea app.

This module is the source of truth for:

- baseline screen composition
- brand/banner treatment
- semantic color roles
- component-level style application
- theme loading compatibility with `~/.straumr/theme.json`

## Responsibilities

- Preserve the established Straumr TUI look from the existing screenshots:
  - top help strip at the upper left
  - top-right `STRAUMR` ASCII banner
  - dark background with light framed content panels
  - green primary emphasis for active/current/success state
  - blue info accents for methods and secondary numeric metadata
  - boxed list/detail/send layouts with roomy spacing
- Define a reusable theme object for the Go app, not ad hoc per-view color constants.
- Keep theme compatibility with the current shared `theme.json` contract where practical.
- Provide semantic style roles for shell, lists, overlays, prompts, detail panes, status bars, and send panes.
- Define how method colors and markup-style semantic text should map into Go rendering.

## Inputs

- `~/.straumr/theme.json`
- built-in default theme when the user theme file is missing or invalid
- terminal color capability:
  - ANSI 16-color
  - truecolor
- visual baseline from:
  - `README.md` screenshots
  - existing .NET TUI theme defaults

## Outputs

- parsed theme tokens
- semantic component styles
- reusable layout helpers and text styles for:
  - shell frame
  - banner
  - hints/help strip
  - framed panels
  - selected rows
  - current/damaged/missing states
  - status/error/success/info text
  - request method rendering
  - send/detail panes
  - overlays and prompts

## CLI interaction

- None.
- This module must not call the CLI.

## Boundaries

- Owns look-and-feel and theme resolution.
- Does not own entity workflows or CLI subprocess behavior.
- App shell and view modules consume semantic styles from this module rather than hardcoding color or border choices.

## Visual baseline to preserve

Future agents must preserve these visible characteristics unless the user explicitly asks to change them:

- The top-left help strip is always present and visually prominent.
- The top-right `STRAUMR` figlet/ASCII mark is part of the default shell layout.
- Primary screens use a dark canvas with framed panels instead of flat text sections.
- The selected row uses a filled highlight bar rather than only a single leading cursor marker.
- Success/current/active emphasis uses the theme primary-success family, visually close to the current green.
- Info/method/date/duration accents use the theme info/secondary family, visually close to the current blue.
- The send view is a boxed multi-pane composition, not a plain text dump.

The screenshots in `README.md` and `docs/media/` are a required reference, not optional inspiration:

- `docs/media/tui-workspaces.png`
- `docs/media/tui-requests.png`
- `docs/media/tui-send.png`

## Existing .NET theme behavior to mirror

The current TUI loads the theme from the same config directory as `options.json`, effectively `~/.straumr/theme.json`, through the shared `ThemeLoader` and falls back silently to built-in defaults when the file is missing or invalid.

Source-of-truth references:

- `src/Straumr.Core/Services/StraumrOptionsService.cs`
- `src/Straumr.Console.Shared/Helpers/ThemeLoader.cs`
- `src/Straumr.Console.Shared/Theme/StraumrTheme.cs`
- `docs/themes.md`

The current token set is:

- `Surface`
- `SurfaceVariant`
- `OnSurface`
- `Primary`
- `OnPrimary`
- `Secondary`
- `Accent`
- `Success`
- `Info`
- `Warning`
- `Danger`
- `MethodGet`
- `MethodPost`
- `MethodPut`
- `MethodPatch`
- `MethodDelete`
- `MethodHead`
- `MethodOptions`
- `MethodTrace`
- `MethodConnect`

The current TUI also detects truecolor by checking whether any theme token is a hex color, and otherwise stays in ANSI 16-color mode.

Source-of-truth references:

- `src/Straumr.Console.Tui/TuiApp.cs`
- `src/Straumr.Console.Tui/Helpers/ColorResolver.cs`

The theme is loaded once at startup/construction time and then reused across the TUI. There is no hot reload.

Source-of-truth references:

- `src/Straumr.Console.Tui/Integration/TuiConsoleIntegration.cs`
- `src/Straumr.Console.Tui/Console/TuiInteractiveConsole.cs`

## Proposed Go theming framework

Use a dedicated package under:

- `internal/ui/theme/`

Approved third-party UI libraries for this module:

- `github.com/charmbracelet/lipgloss`
  - required default styling and layout layer
  - use for borders, spacing, semantic text treatment, banner placement, panel framing, row highlighting, and shell composition
- `github.com/charmbracelet/bubbles`
  - allowed selectively, not by default for everything
  - preferred first use cases:
    - `viewport`
    - `help`
    - `textarea` only if the current custom multiline input becomes a maintenance burden

Do not introduce additional UI or styling libraries unless there is a clear deficiency in the approved stack.

Recommended structure:

- `theme/types.go`
  - `ThemeOptions`
  - `Theme`
  - Go-compatible representation of the shared token schema
- `theme/defaults.go`
  - built-in defaults matching the current .NET defaults first, not a new design
- `theme/load.go`
  - load `~/.straumr/theme.json`
  - derive the path from the same config root as `options.json`
  - tolerate missing/invalid files by falling back to defaults
  - load once during startup; do not implement hot reload first
- `theme/color.go`
  - parse `#RRGGBB`, `#RGB`, and ANSI color names
  - detect truecolor-vs-ANSI mode
- `theme/styles.go`
  - derive semantic Lip Gloss styles from tokens
- `theme/methods.go`
  - resolve HTTP method colors from theme tokens

## Proposed component style API

The Go app should not style components by reaching into raw theme tokens everywhere. It should expose semantic styles such as:

- `ShellBackground`
- `HelpText`
- `Banner`
- `Frame`
- `FrameTitle`
- `ListRow`
- `ListRowSelected`
- `ListRowCurrent`
- `ListRowDamaged`
- `ListRowMissing`
- `StatusSuccess`
- `StatusInfo`
- `StatusWarning`
- `StatusError`
- `PromptFrame`
- `PromptField`
- `PromptFieldFocused`
- `DetailPane`
- `DetailKey`
- `DetailValue`
- `MethodGET`
- `MethodPOST`
- `MethodPUT`
- `MethodPATCH`
- `MethodDELETE`

The shell and views should depend on semantic style constructors, not on `Primary`/`Surface` literals.

Lip Gloss should be the implementation layer for these semantic styles. Future agents should not build a parallel in-house styling abstraction on top of raw strings when Lip Gloss already covers the layout and color primitives required here.

The Go implementation should also mirror the .NET layering model:

- one base application/shell scheme
- one shared list selection scheme
- one shared button/action scheme
- one shared semantic text styling path for markup-like tokens

## Markup compatibility guidance

The current .NET TUI supports semantic markup tags that resolve through the theme:

- `secondary`
- `success`
- `info`
- `warning`
- `danger`
- `primary`
- `accent`
- `surface`
- `method-get`
- `method-post`
- `method-put`
- `method-patch`
- `method-delete`
- `method-head`
- `method-options`
- `method-trace`
- `method-connect`

Source-of-truth reference:

- `src/Straumr.Console.Tui/Helpers/MarkupText.cs`

The Go TUI does not need to copy the .NET parser literally, but it should preserve the same semantic rendering vocabulary for any reusable styled text helpers so future component work stays consistent.

## Implementation guidance for future agents

- Do not ship a plain Bubble Tea shell once the visual-system module exists; use semantic styles everywhere.
- Use `lipgloss` as the default styling and layout engine for shell, lists, frames, overlays, and semantic text.
- Use `bubbles` only where it materially improves maintainability of a concrete component.
- Do not remove the top-right banner unless the user asks.
- Do not invent a new theme file format. Start by reading the existing `theme.json` shape from `docs/themes.md`.
- Do not implement per-screen theme loading or runtime theme drift. Load once and pass the resolved theme through the shell and views.
- Do not hardcode colors in views or overlays. Add or extend semantic style roles centrally.
- Do not make the Go TUI “more modern” by flattening away frames, spacing, or the banner. Familiarity to existing Straumr users is the primary constraint.
- If the current .NET TUI and the screenshots disagree, prefer the existing screenshot/layout behavior for shell composition and the current .NET code for theme token behavior.

## References

- `README.md`
- `docs/themes.md`
- `src/Straumr.Core/Services/StraumrOptionsService.cs`
- `src/Straumr.Console.Shared/Helpers/ThemeLoader.cs`
- `src/Straumr.Console.Shared/Theme/StraumrTheme.cs`
- `src/Straumr.Console.Tui/Integration/TuiConsoleIntegration.cs`
- `src/Straumr.Console.Tui/Console/TuiInteractiveConsole.cs`
- `src/Straumr.Console.Tui/TuiApp.cs`
- `src/Straumr.Console.Tui/Helpers/ColorResolver.cs`
- `src/Straumr.Console.Tui/Helpers/MarkupText.cs`
- `src/Straumr.Console.Tui/Components/Branding/Banner.cs`
- `src/Straumr.Console.Tui/Screens/Base/ModelScreen.cs`
