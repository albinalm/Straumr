# Themes

Straumr's terminal UI can use the terminal's own colour scheme, the built-in
dark palette, or a custom TOML theme. Custom themes can be small: any property
you leave out inherits from the built-in `terminal` theme.

## Choose a theme

Open the command prompt in the TUI with `:` and use one of the built-ins:

```text
:theme terminal
:theme straumr
```

`terminal` is the default. It uses the terminal's foreground, background, and
16-colour palette, so it follows the terminal's configured scheme.
`straumr` uses fixed RGB colours and therefore looks the same in every
terminal.

Running `:theme` without an argument reports the current theme. A successful
change is saved to `~/.straumr/settings.toml` and applied immediately.
:
You can also edit the setting directly. Run `:settings` to open the file in
your configured editor, or set the value by hand:

```toml
theme = "terminal"
```

The value can be a built-in name or a theme file. Custom names are looked up in
`~/.straumr/themes`, and the `.toml` extension may be omitted:

```toml
theme = "mine"
```

Absolute paths, paths beginning with `~`, and older paths relative to the directory
containing `settings.toml` remain valid. A leading `~` and environment variables in
the path are expanded.

## Create a custom theme

The quickest option is a small theme containing only the differences you want:

```toml
name = "Mine"

[colors]
brand = "bright-magenta"
accent = "cyan"
```

Save that as `~/.straumr/themes/mine.toml`, then select it in the TUI:

```text
:theme mine
```

To start from a complete theme instead, export either built-in:

```text
:theme export terminal
:theme export straumr
```

The export is written to `~/.straumr/themes/<name>.toml` and includes comments
describing the palette. Straumr will not overwrite an existing file. Rename the
export, edit it, and select the new path with `:theme`.

After changing a selected theme file in an external editor, run the same
`:theme mine` command again to reload it.

## File format

A theme is a TOML document with a display name and up to two tables:

```toml
name = "Example"

[colors]
background         = "#10131a"
raised             = "#202635"
selection          = "#31508f"
selection-inactive = "#28354f"
hover              = "#191f2b"
border             = "#46516b"
scroll-track       = "#222938"
scroll-thumb       = "#697792"
text               = "#d8deea"
text-bright        = "#ffffff"
muted              = "#8994aa"
muted-bright       = "#b8c1d2"
accent             = "#65a7ff"
amber              = "#f2c14e"
green              = "#55d68b"
red                = "#ff6b78"
red-bright         = "#ffadb5"
purple             = "#bd9cff"
brand              = "#65a7ff"

[methods]
get    = "#55d68b"
post   = "#65a7ff"
put    = "#f2c14e"
patch  = "#bd9cff"
delete = "#ff6b78"
other  = "#b8c1d2"
```

`name` is the label shown by `:theme`. If it is missing or blank, Straumr uses
`unnamed`.

All entries in `[colors]` and `[methods]` are optional in a custom theme.
Omitted entries inherit the corresponding value from `terminal`. This makes a
small accent-only theme safe, but a theme with a fixed `background` should also
define its text, surface, selection, and border colours: terminal palette values
that work on an unknown terminal background may not be readable on a fixed one.

Role names inside `[colors]` and `[methods]` are case-insensitive and ignore
dashes and underscores. For example, `selection-inactive`, `selection_inactive`, and
`SelectionInactive` name the same property. The spelling used above is the
recommended style. Unknown properties are reported as errors instead of being
ignored.

## Colour values

Every colour accepts these forms:

| Form | Examples | Meaning |
| --- | --- | --- |
| RGB | `"#3b9eff"`, `"#39f"` | A fixed 24-bit colour; short RGB expands to `#3399ff`. |
| Terminal default | `"default"` | The terminal's own foreground or background. `terminal` and `inherit` are aliases. |
| 16-colour palette | `"blue"`, `"bright-black"` | A slot from the terminal's configured ANSI palette. |
| 256-colour palette | `"indexed:74"`, `"74"` | An indexed terminal colour from 0 through 255. |

The eight palette names are `black`, `red`, `green`, `yellow`, `blue`,
`magenta`, `cyan`, and `white`. Prefix any of them with `bright-` for its bright
variant. `purple` is accepted as an alias for `magenta`, including
`bright-purple`.

Fixed RGB colours give a consistent appearance. Palette colours adapt to the
user's terminal scheme. `default` is the only value that preserves the
terminal's own foreground or background exactly, including transparent
backgrounds.

The `selection` property additionally accepts `"invert"`. This swaps the
terminal's foreground and background for the selected row, which is useful when
the theme does not know the terminal's actual colours. If you use inversion,
keep text drawn on the selected row close to `default`; fixed foregrounds can
produce a two-tone selection band.

## Colour properties

The properties are semantic roles. Their names describe what the colour means
in the interface, not a required hue.

| Property | Used for |
| --- | --- |
| `background` | The shared background of the whole application. |
| `raised` | The raised surface used for identifier badges and as the fallback selection colour needed by editable fields. |
| `selection` | A selected row in a focused list and the focused section-title chip. May be `invert`. |
| `selection-inactive` | A selected row when its list does not have focus. |
| `hover` | The subtle band beneath the pointer. |
| `border` | Dividers, rules, and borders. |
| `scroll-track` | The inactive part of a scroll bar. |
| `scroll-thumb` | The movable part of a scroll bar. |
| `text` | Primary body text. |
| `text-bright` | Primary text lifted for selection bands and other emphasized surfaces. |
| `muted` | Secondary text, labels, hints, and inactive values. |
| `muted-bright` | Secondary text lifted for selection bands and emphasized surfaces. |
| `accent` | Interactive emphasis such as keys, focus markers, and active controls. |
| `amber` | Values or counts that are populated/present. The colour does not have to be amber. |
| `green` | The active workspace, current-resource markers, and successful state. |
| `red` | Errors, failures, destructive actions, and unavailable values. |
| `red-bright` | Error text lifted for a selection band. |
| `purple` | Secret references and related semantic emphasis. The colour does not have to be purple. |
| `brand` | The `{straumr}` wordmark. |

`brand` is independent from `accent`, allowing a theme to keep structural UI
colourless without losing the wordmark. If it is omitted, it inherits the
`terminal` theme's brand colour like any other omitted custom-theme property.

## HTTP method properties

The `[methods]` table controls the colour used for methods in request lists and
response views:

| Property | Method |
| --- | --- |
| `get` | `GET` |
| `post` | `POST` |
| `put` | `PUT` |
| `patch` | `PATCH` |
| `delete` | `DELETE` |
| `other` | Every other HTTP method |

These properties accept the same colour values as `[colors]`. Omitted method
entries inherit from `terminal`; changing `[colors].green`, for example, does
not implicitly change `[methods].get`.

## Readability and errors

Straumr rejects malformed TOML, invalid colour values, out-of-range indexed
colours, and unknown roles in `[colors]` or `[methods]`. The `:theme` command
validates a theme before saving the setting, so an invalid theme leaves the
current theme in place. If the configured theme cannot be read during startup
or a settings reload, Straumr shows the problem and falls back to `terminal` so
the application remains usable.

Some valid themes can still hide information. Straumr warns when important
foreground/background pairs are identical, or when a fixed background is used
with terminal-default text. Check at least these states while designing a
theme:

- focused, unfocused, and hovered rows;
- primary and secondary text on each selection band;
- error text on selected rows;
- dividers and scroll bars;
- each HTTP method colour;
- both a populated value and an empty/inactive value.

Aim for at least 4.5:1 contrast for text and 3:1 for non-text UI markers. Test
palette-based themes in both a light and a dark terminal scheme because ANSI
palette slots are chosen by the terminal, not by Straumr.
