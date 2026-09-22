# Straumr themes

Themes are TOML files for the terminal UI. The files in this folder are examples; copy one to your Straumr theme directory or select it by an absolute path.

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

Only `name` is top-level. The three tables are optional; omitted values inherit from the built-in `terminal` palette. Role names ignore case, hyphens, and underscores.

```toml
name = "Midnight"

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
```

Valid colour values are `#rgb` or `#rrggbb`, `default` (also `terminal` or `inherit`), ANSI names (`black`, `red`, `green`, `yellow`, `blue`, `magenta`, `cyan`, `white`, with an optional `bright-` prefix), or an indexed colour from `0` to `255` (`indexed:74` or `74`). `purple` is an alias for `magenta`. `selection` additionally accepts `invert`, which swaps the terminal foreground and background for a selected row.

### `[colors]`

`background`, `raised`, `selection`, `selection-inactive`, `hover`, `border`, `scroll-track`, `scroll-thumb`, `text`, `text-bright`, `muted`, `muted-bright`, `accent`, `amber`, `green`, `red`, `red-bright`, `purple`, and `brand`.

### `[methods]`

`get`, `post`, `put`, `patch`, `delete`, and `other` control HTTP-method colours.

### `[code]`

`key`, `string`, `number`, `boolean`, `null`, and `punctuation` control JSON response highlighting.

Straumr rejects malformed TOML, invalid colours, and unknown roles. If a theme cannot be loaded, the current theme remains active; warnings may still identify unreadable foreground/background combinations.
