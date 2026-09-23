# Settings

Everything Straumr can be told lives in one file, `~/.straumr/settings.toml`. Open it from the terminal UI with `:settings`, which hands it to your `$EDITOR` and reloads it when you close it. The file is written for you on first run with every setting explained in comments, so you can also just read it.

Nothing in it is required. A setting you leave out takes its default.

## The settings

| Setting | Default | What it does |
| --- | --- | --- |
| `theme` | `"terminal"` | `terminal`, `straumr`, a theme name in `~/.straumr/themes`, or a path to a theme file. |
| `keybind-preset` | `"vim"` | `vim`, `emacs`, `commander`, or `client`. |
| `quick-start-completed` | `false` | Set once you finish the quick start. Clear it, or run `:quickstart`, to see it again. |
| `paths.workspaces` | none | Where new workspaces are created. Without it, `straumr create workspace` needs `-o <dir>`. |
| `paths.secrets` | `~/.straumr/secrets` | Where the global secret files live. |
| `response.format` | `"none"` | What to do with a response body as it arrives: `none`, `beautify`, or `minify`. JSON, XML, and HTML only. |
| `response.highlight` | `true` | Colour bodies by token — JSON, XML, HTML, YAML, and form-urlencoded, in both the response and request panes. |
| `response.highlight-limit` | `256` | KiB. A body larger than this arrives uncoloured. |
| `response.store-limit` | `1024` | KiB. The largest response body kept with the request. `0` keeps none. |

A leading `~` and environment variables are expanded in both paths.

```toml
theme = "straumr"
keybind-preset = "client"

[paths]
workspaces = "~/api-workspaces"

[response]
format = "beautify"
highlight-limit = 1024
```

Neither response limit changes what is shown by hand: `h` still turns highlighting on for a large body, and `b` still beautifies it. A response over `store-limit` is still recorded — its status, headers, and timings stay with the request, and only the body is dropped.

`format` applies to a response as it arrives. A request's own body preview is always laid out, since it is a preview of something you wrote rather than something that came back.

If something in the file cannot be understood, Straumr says so on the next load and carries on with the default for that one line. The rest of your file still applies.

## Keybindings

```toml
keybind-preset = "vim"

[keybinds]
"Request.Send" = "Ctrl+R"
"Editor.Save" = "F2"
```

Pick a preset, then override individual actions under `[keybinds]`. `"none"` disables one. The hint bar along the bottom of the terminal UI always shows what is bound right now; [keybinds.md](keybinds.md) lists every action, every preset, and how to spell a key.

## Themes

Two themes are built in. `terminal` is the default and borrows your terminal's own palette, so Straumr looks like the rest of your setup. `straumr` is a dark blue palette of its own.

Custom themes are TOML files in `~/.straumr/themes`, selected by name, or anywhere on disk and selected by path:

```text
:theme straumr
:theme midnight
:theme ~/dotfiles/midnight.toml
```

The easiest way to start one is to copy a built-in and change it:

```text
:theme export midnight
```

That writes the full `straumr` palette to `~/.straumr/themes/midnight.toml`. A theme only has to name what it wants different — anything it leaves out follows `terminal` — so a real theme can be three lines:

```toml
name = "Midnight"

[colors]
brand = "#65a7ff"
```

Colours may be a hex value (`#65a7ff`), an ANSI name (`green`, `bright-blue`), an indexed colour (`indexed:74`), or `default` to inherit the terminal's. The complete list of roles, and the `[methods]` and `[code]` tables that colour HTTP methods and JSON bodies, is in [the theme guide](../themes/README.md), alongside two example themes.

A theme that cannot be read leaves the current one in place and tells you why.
