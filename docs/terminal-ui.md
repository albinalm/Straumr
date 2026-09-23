# The terminal UI

Run `straumr` with no arguments and you land in the terminal UI. It is the same data the CLI works on — the same workspaces, requests, auths, variables, and secrets — with a screen you can move around in.

## The first launch

The first time you open it, a short quick start asks three things: which keybinding preset feels familiar, which theme to use, and which workspace to work in. If you have no workspaces yet it offers to create one, suggesting `~/.straumr/workspaces` as the location unless you have already set one.

Nothing there is permanent. Run `:quickstart` any time to go through it again.

## Getting around

There are five screens, one per kind of thing:

| Screen | Command | Short |
| --- | --- | --- |
| Requests | `:request` | `:rq` |
| Workspaces | `:workspace` | `:ws` |
| Auths | `:auth` | `:au` |
| Variables | `:variable` | `:vr` |
| Secrets | `:secret` | `:sc` |

Each screen is a list on the left and detail panes on the right. Move through the list, and the panes follow the selection. The panes have tabs — a request shows **Body**, **Headers**, and **Params**; its response shows **Body**, **Headers**, **Sent headers**, and **Network**.

**Headers** on a response is what came back. **Sent headers** is what went out, resolved as it went — variables substituted, secrets filled in, auth applied — which is not the same as the request's own **Headers** tab, where those are still written as `{{name}}`. Sent headers are not kept with the request, so the tab has them for a response sent in this session and asks you to send again otherwise.

Nothing is hidden by which header it is — `Authorization` reads like any other. What stays hidden is a secret: a header whose configured value holds a `{{secret:name}}` is listed with that reference left in place, so you see the header and its scheme without its value ever reaching the screen. Everything else is shown resolved, variables and fetched auth tokens included.

The bar along the bottom always shows the keys that do something right now, for whatever has focus. It is the fastest way to learn your own keybindings; the full list is in [keybinds.md](keybinds.md).

Drag the splits with `Ctrl+H`/`Ctrl+L`/`Ctrl+K`/`Ctrl+J` (vim preset). Where you leave them is remembered per screen.

`/` opens the filter over the current list. On requests it matches the name, the URL, or the method, so `/post` and `/users` both narrow the list; elsewhere it matches the name. The count beside the screen title shows matches over total.

Every headers tab carries a filter of its own, opened with the same `/` while the tab has focus, with its own count above it. It matches header names and values, so `/auth` finds the header and `/json` finds whatever declares it.

Headers move a header at a time, not a line at a time, on the same keys that move any list — `j`/`k`, `g`/`G` in the vim preset. The selected one is marked down its left edge, and `y` copies its value.

Each header is one line, cut off where it runs out of room, so a list of twenty stays a list of twenty. `e` unfolds the selected one over as many lines as its value needs and folds it back again. That is the setting worth knowing for tokens: a bearer token wrapped in full is thirty lines and buries everything under it, which is why it is not the default.

## The command prompt

`:` opens a command prompt at the bottom of the screen. `Tab` completes, `Up` and `Down` walk back through what you have run, `Escape` closes it.

Anywhere:

```text
:quit              also :q, :exit
:settings          open settings.toml in $EDITOR — also :set
:theme             show the active theme
:theme <name>      switch theme, by name or by path
:theme export      write a copy of the built-in straumr theme to edit
:quickstart        run the first-launch setup again
```

On a screen, against the thing it names — or against the selection, if you name nothing:

```text
:select <name>     move the selection — also :r, :w, :a, :v, :s per screen
:create            new — also :new
:edit <name>       open the editor
:copy <name>       duplicate
:delete <name>     remove, after a confirmation
:json <name>       open the raw JSONC file in $EDITOR
:refresh           re-read from disk
```

Requests add `:send` and `:view` (the last response, also `:response`). Workspaces add `:use` (make active, also `:activate`), `:import`, and `:export`. Auths add `:send`, which sends an OAuth or custom auth request and shows the extracted value in the credential pane. Auths that hold a bearer token or basic credentials have no request to send.

Use `:reset` to replace `settings.toml` with its factory template after a confirmation. The TUI reloads into quick start so you can choose a theme and keybind preset again. Workspaces and secrets stay in place.

Prefix a command with a screen name to run it from anywhere: `:rq send users` goes to Requests and sends, `:ws use my-api` switches the active workspace without leaving the screen you are on.

## Editing

Editing a request opens a form: name, method, URL, auth, and the header and parameter tables. Bodies are not edited in the form — `Enter` on the body field hands the file to your `$EDITOR`, and Straumr picks the change back up when you close it. That is deliberate: your editor already knows JSON better than any box inside a terminal UI would.

`Ctrl+E` on a list entry skips the form entirely and opens the underlying JSONC file. Comments you leave in that file survive everything Straumr writes afterwards.

Both paths need `$EDITOR` set. Without it, the commands that would hand off say so instead.

Type `{{` in any form field and a box of matching variable names opens under it; type `{{secret:` and it lists secrets instead. `Up` and `Down` move through it, `Enter` inserts the name and the closing braces, and `Escape` dismisses it — `Tab` is left alone so it still moves between fields. Keep typing to narrow the list.

A request and an auth show what they reference under **Variables & Secrets**, and whether each one resolves right now.

## Sending

`s` on a request sends it (`Ctrl+R` in the client preset). The response pane fills in as it arrives, and the divider above it carries the status and timing. `Escape` cancels a request still in flight.

On a **Body** tab — the response's and the request's alike — `b` cycles beautify and minify, `h` turns highlighting on and off, and `y` copies. Straumr works out what the body is from its `Content-Type`, falling back to reading the body itself: JSON, XML, HTML, YAML, and form-urlencoded are coloured by token, and JSON, XML, and HTML are also beautified and minified. Anything else is shown as it was sent, and `b` says so rather than changing it.

On a response **Body** tab, `e` opens the whole body in your `$EDITOR` — the full body, not the preview's truncation. It is a reader, not an editor: Straumr discards whatever you leave behind and never writes it back to the response. Use it to search, fold, or copy pieces out with the keys your own editor has trained into your fingers.

Sent responses are stored next to the request, so they are still there after a `:refresh` or a restart — up to the size limit in [settings](customize.md).

## What it stores, and where

| Path | What |
| --- | --- |
| `~/.straumr/settings.toml` | your settings |
| `~/.straumr/state.json` | active workspace, pane layouts, the secret index |
| `~/.straumr/secrets/` | secrets, one JSONC file each |
| your workspace directory | variables too, one JSONC file each |
| `~/.straumr/themes/` | themes you install or export |
| your workspace directory | one folder per workspace, `.straumr` files inside |

All of it is text, and all of it is yours to edit.
