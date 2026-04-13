# Themes

Straumr loads its terminal theme from:

```text
~/.straumr/theme.json
```

If that file does not exist, or if it contains invalid JSON, Straumr falls back to the built-in default theme.

## Installing a Theme

The repository includes ready-made presets under:

```text
themes/<theme-name>/theme.json
```

Current presets:

- `themes/dracula/theme.json`
- `themes/gruvbox-dark/theme.json`
- `themes/nord/theme.json`
- `themes/solarized-dark/theme.json`
- `themes/tokyo-night/theme.json`

To install one, copy it into your Straumr config directory as `theme.json`.

Example on Linux/macOS:

```sh
mkdir -p ~/.straumr
cp themes/dracula/theme.json ~/.straumr/theme.json
```

On Windows, use the same file contents and place them in the `theme.json` file inside your Straumr config directory.

## Theme File Format

The file must match the `StraumrThemeOptions` shape:

```json
{
  "Theme": {
    "Surface": "#282A36",
    "SurfaceVariant": "#44475A",
    "OnSurface": "#F8F8F2",
    "Primary": "#BD93F9",
    "OnPrimary": "#282A36",
    "Secondary": "#6272A4",
    "Accent": "#FF79C6",
    "Success": "#50FA7B",
    "Info": "#8BE9FD",
    "Warning": "#F1FA8C",
    "Danger": "#FF5555",
    "MethodGet": "#8BE9FD",
    "MethodPost": "#50FA7B",
    "MethodPut": "#F1FA8C",
    "MethodPatch": "#FF79C6",
    "MethodDelete": "#FF5555",
    "MethodHead": "#6272A4",
    "MethodOptions": "#6272A4",
    "MethodTrace": "#6272A4",
    "MethodConnect": "#6272A4"
  }
}
```

Every value is a string. Straumr accepts either:

- hex colors such as `#282A36`
- ANSI color names such as `BrightBlue`

## Truecolor vs ANSI

As soon as any theme color is set to a hex color like `#282A36`, Straumr treats the theme as truecolor and disables the ANSI 16-color palette mode for the TUI.

If you want to stay strictly ANSI-only, make sure every color value in `theme.json` is one of the supported ANSI color names below.

## ANSI Color Names

These are the supported 16-color names accepted by Straumr via Terminal.Gui:

| Name | Typical meaning |
|---|---|
| `Black` | standard black |
| `Blue` | standard blue |
| `Green` | standard green |
| `Cyan` | standard cyan |
| `Red` | standard red |
| `Magenta` | standard magenta |
| `Yellow` | standard yellow |
| `Gray` | standard bright black / light gray |
| `DarkGray` | dark gray |
| `BrightBlue` | bright blue |
| `BrightGreen` | bright green |
| `BrightCyan` | bright cyan |
| `BrightRed` | bright red |
| `BrightMagenta` | bright magenta |
| `BrightYellow` | bright yellow |
| `White` | white |

## Notes

- Color names are parsed case-insensitively, but using the exact names above keeps files easier to read.
- Mixed themes are allowed, but a single hex value is enough to switch the TUI into truecolor mode.
- Method colors only affect HTTP method styling such as `GET`, `POST`, and `DELETE`.
