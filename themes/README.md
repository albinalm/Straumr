# Straumr themes

Themes are TOML files for the terminal UI. The files in this folder are examples; copy one to your Straumr theme directory or select it by an absolute path.

The default `terminal` theme uses your terminal's foreground and background for neutral text and surfaces, including transparent backgrounds. Secondary text and dividers are dimmed; badges use bold text, and keys and selection markers use your terminal's cyan slot. HTTP methods, status colours and JSON values also use your terminal's ANSI palette slots. Focused rows and selected input text reverse the foreground and background, with one consistent band across method labels and metadata. Dialogs and dropdowns clear the UI colours underneath them while retaining the terminal background.

## Create a theme

Open Straumr and use the command prompt:

```text
:theme export
:theme export midnight
:theme export midnight.toml
```

`export` writes a complete copy of the built-in `straumr` theme to `~/.straumr/themes/my-theme.toml` by default. An optional filename replaces `my-theme`; `.toml` is optional, names are saved lowercase, and existing files are never overwritten.

Edit the exported file, then select it:

```text
:theme midnight
:theme /full/path/to/midnight.toml
```

Using a name looks for `~/.straumr/themes/<name>.toml`; an absolute path can point anywhere. The selected reference is saved in `~/.straumr/settings.toml` and is applied on the next UI rebuild.

## Syntax

`name` and `layout` are top-level. Every table is optional; omitted colours inherit from the built-in `terminal` palette. Role names ignore case, hyphens, and underscores.

```toml
name = "Midnight"
layout = "v2"

[colors]
background = "#10131a"
text = "#d8deea"
accent = "bright-blue"
selection = "invert"

[methods]
get = "green"
post = "#65a7ff"

[code]
key = "purple"
string = "green"

[animations]
transitions = false

[gradient]
stops = ["#3ddc97", "#65a7ff", "#b79cff"]
```

Valid colour values are `#rgb` or `#rrggbb`, `default` (also `terminal` or `inherit`), ANSI names (`black`, `red`, `green`, `yellow`, `blue`, `magenta`, `cyan`, `white`, with an optional `bright-` prefix), or an indexed colour from `0` to `255` (`indexed:74` or `74`). `purple` is an alias for `magenta`. `selection` additionally accepts `invert`, which swaps the terminal foreground and background for a selected row.

### `[colors]`

`background`, `raised`, `selection`, `selection-inactive`, `hover`, `border`, `scroll-track`, `scroll-thumb`, `text`, `text-bright`, `muted`, `muted-bright`, `accent`, `amber`, `green`, `red`, `red-bright`, `purple`, `brand`, and `section`.

`section` colours the tagline in the v2 header and the waves around it. Left out, it follows `amber`.

### `layout`

`v2` (the default) is the modern interface: a header with a rotating tagline and live network stats; uppercase section titles whose rule lights up in the accent colour when the section has focus; a slim status line with the workspace, the screen and the most useful keys; rounded dialogs and command prompt; status dots on requests; and timing gauges on the Network page. `?` shows every key.

`v1` is the classic framed layout, with the full key list along the bottom. A theme that leaves `layout` out inherits `v2` from `terminal`. `layout` in `settings.toml`, or `:layout v1` in the app, overrides whatever the theme says.

### `[animations]`

`enabled` turns every animation off at once when `false`. Otherwise each can be switched alone: `spinner` (the busy indicator while a request or token is in flight), `pulse` (the dot on a fresh message) and `transitions` (a new tagline typing itself out, and gauges growing into place). Values a theme leaves out are inherited. `animations` in `settings.toml`, or `:animations off`, overrides the theme.

### `[gradient]`

`stops` is a list of colours the Network-page gauges and the header sparklines fill along. Hex stops blend smoothly; palette slots and `indexed:` colours cannot be blended, so the bar steps from one to the next instead. Left out, the stops are the theme's own `green`, `accent` and `purple`.

### `[methods]`

`get`, `post`, `put`, `patch`, `delete`, and `other` control HTTP-method colours.

### `[code]`

`key`, `string`, `number`, `boolean`, `null`, and `punctuation` control JSON response highlighting.

## Examples

- `oxblood.toml` and `scarlet.toml`: fixed palettes whose v2 chrome is derived from their own colours.
- `valley.toml`: a misty dusk palette with a transparent background, made for a terminal with a blurred or image background, and a full `[gradient]`.

Straumr rejects malformed TOML, invalid colours, and unknown roles. If a theme cannot be loaded, the current theme remains active; warnings may still identify unreadable foreground/background combinations.
