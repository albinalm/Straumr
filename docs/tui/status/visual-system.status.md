# Visual System Status

## Scope

Visual baseline, theme loading compatibility, semantic color roles, shared component styling, and banner/frame layout guidance for `straumr-tui`.

## Current status

In progress. The Go app now has a real theme package, screenshot-aligned shell framing, and first-pass themed styling across primary screens and overlays.

## Completed work

- Captured the existing visual baseline from the README screenshots.
- Documented that the top-right `STRAUMR` figlet banner is part of the default shell layout.
- Documented that framed panels, dark surface, green primary emphasis, and blue info accents are part of the familiarity contract.
- Traced the current .NET theme system from `~/.straumr/theme.json` loading through TUI application.
- Documented that the .NET theme is loaded once at startup from the shared config directory and is not hot-reloaded.
- Documented the current shared theme token schema and truecolor-vs-ANSI behavior.
- Documented a Go-side theming framework that future agents should follow instead of styling views ad hoc.
- Documented the approved Go visual stack: `lipgloss` as the default styling layer and selective `bubbles` usage for reusable components like `viewport` and `help`.
- Added `lipgloss` to the Go module and established `internal/ui/theme` as the shared theme/defaults/style package.
- Implemented startup theme loading from `~/.straumr/theme.json` with fallback defaults.
- Added semantic style construction for shell, panels, tabs, rows, methods, status text, and first-pass overlay treatment.
- Restyled the shell with a header/tab strip plus the top-right `STRAUMR` banner.
- Restyled list screens, send panes, and overlay/dialog surfaces to use the new theme package rather than staying plain text.

## Work in progress

- Tightening semantic-style coverage so dialog-local helpers can shrink over time instead of duplicating theme behavior.
- Narrow-terminal tuning and visual polish against the screenshot baseline.
- Deciding whether additional `bubbles` primitives are actually warranted after the current Lip Gloss pass.

## Blockers

- None.

## Files touched

- `docs/tui/README.md`
- `docs/tui/app-shell.md`
- `docs/tui/dialogs-and-pickers.md`
- `docs/tui/IMPLEMENTATION_STATUS.md`
- `docs/tui/visual-system.md`
- `docs/tui/status/visual-system.status.md`
- `src/straumr-tui/go.mod`
- `src/straumr-tui/go.sum`
- `src/straumr-tui/internal/app/bootstrap.go`
- `src/straumr-tui/internal/ui/layout.go`
- `src/straumr-tui/internal/ui/theme/**`
- `src/straumr-tui/internal/views/common/list.go`
- `src/straumr-tui/internal/views/dialogs/**`
- `src/straumr-tui/internal/views/send/view.go`
- `src/straumr-tui/internal/views/workspace/view.go`
- `src/straumr-tui/internal/views/request/view.go`
- `src/straumr-tui/internal/views/auth/view.go`
- `src/straumr-tui/internal/views/secret/view.go`

## Important decisions

- Visual parity is a real product requirement, not optional polish.
- The Go TUI should reuse the existing `theme.json` contract rather than inventing a second theme file.
- Future view work should consume semantic styles from a shared visual-system package instead of hardcoding colors or borders.

## Next steps

- Tighten the semantic-style API so dialogs and send panes can stop carrying token-to-style helper duplication.
- Tune widths, spacing, and filled-selection behavior on narrower terminals.
- Validate the current palette and banner/frame composition against live manual screenshots, then iterate on any obvious mismatches.
- Add selected `bubbles` primitives only if they materially improve maintainability of scroll/help components.

## Resume notes

- Source-of-truth visual references are the README screenshots plus the .NET theme pipeline.
- The live Go visual system now starts in `src/straumr-tui/internal/ui/theme` and is mounted from `src/straumr-tui/internal/app/bootstrap.go`.
- The main implementation references are:
  - `src/Straumr.Console.Shared/Helpers/ThemeLoader.cs`
  - `src/Straumr.Console.Shared/Theme/StraumrTheme.cs`
  - `src/Straumr.Console.Tui/TuiApp.cs`
  - `src/Straumr.Console.Tui/Helpers/ColorResolver.cs`
  - `src/Straumr.Console.Tui/Helpers/MarkupText.cs`
  - `src/Straumr.Console.Tui/Components/Branding/Banner.cs`
  - `src/Straumr.Console.Tui/Screens/Base/ModelScreen.cs`
