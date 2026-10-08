# The terminal UI

Run `straumr` with no arguments and you land in the terminal UI. It is the same data the CLI works on — the same workspaces, requests, auths, variables, and secrets — with a screen you can move around in.

## The first launch

The first time you open it, a short quick start asks three things: which keybinding preset feels familiar, which theme to use, and which workspace to work in. If you have no workspaces yet it offers to create one, suggesting `~/.straumr/workspaces` as the location unless you have already set one.

Nothing there is permanent. Run `:quickstart` any time to go through it again.

After choosing a preset and theme, a practice screen lets you move between fields and tabs, change a method, and open a query-parameter dialog. Its sample values are not saved. The instructions and footer use the selected preset's actual bindings.

## Getting around

There are five screens, one per kind of thing:

| Screen | Command | Short | Plural |
| --- | --- | --- | --- |
| Requests | `:request` | `:rq` | `:requests` |
| Workspaces | `:workspace` | `:ws` | `:workspaces` |
| Auths | `:auth` | `:au` | `:auths` |
| Variables | `:variable` | `:vr` | `:variables` |
| Secrets | `:secret` | `:sc` | `:secrets` |

Each screen is a list on the left and detail panes on the right. Move through the list, and the panes follow the selection. The panes have tabs — a request shows **Body**, **Headers**, and **Params**; its response shows **Body**, **Headers**, **Sent headers**, and **Network**.

**Headers** on a response is what came back. **Sent headers** is what went out, resolved as it went — variables substituted, secrets filled in, auth applied — which is not the same as the request's own **Headers** tab, where those are still written as `{{name}}`. Sent headers are not kept with the request, so the tab has them for a response sent in this session and asks you to send again otherwise.

Nothing is hidden by which header it is — `Authorization` reads like any other. What stays hidden is a secret: a header whose configured value holds a `{{secret:name}}` is listed with that reference left in place, so you see the header and its scheme without its value ever reaching the screen. Everything else is shown resolved, variables and fetched auth tokens included.

The bar along the bottom always shows the keys that do something right now, for whatever has focus. It is the fastest way to learn your own keybindings; the full list is in [keybinds.md](keybinds.md).

`Tab` and `Shift+Tab` move focus between panes, fields, and buttons. Within a list or choice, use the movement keys for your preset: `j`/`k` with vim, `Ctrl+N`/`Ctrl+P` with emacs, or `Down`/`Up` with commander and client. Arrow keys work in lists and choices in all presets. In a text field, type normally; arrows move the caret.

`Ctrl+T` or `Ctrl+PageDown` switches to the next tab in the focused pane, and `Ctrl+PageUp` switches back. This is the same in previews and forms, including when a text field has focus. Moving through a list keeps focus in that list; use `Tab` to leave it.

Drag the splits with `Ctrl+H`/`Ctrl+L`/`Ctrl+K`/`Ctrl+J` (vim preset). Where you leave them is remembered per screen.

`/` opens the filter over the current list. On requests it matches the name, the URL, or the method, so `/post` and `/users` both narrow the list; elsewhere it matches the name. The count beside the screen title shows matches over total.

Every headers tab carries a filter of its own, opened with the same `/` while the tab has focus, with its own count above it. It matches header names and values, so `/auth` finds the header and `/json` finds whatever declares it.

Headers move a header at a time, not a line at a time, on the same keys that move any list — `j`/`k`, `g`/`G` in the vim preset. The selected one is marked down its left edge, and `y` copies its value.

Each header is one line, cut off where it runs out of room, so a list of twenty stays a list of twenty. `e` unfolds the selected one over as many lines as its value needs and folds it back again. That is the setting worth knowing for tokens: a bearer token wrapped in full is thirty lines and buries everything under it, which is why it is not the default.

## The command prompt

`:` opens a command prompt at the bottom of the screen. `Tab` completes, `Up` and `Down` walk back through what you have run, `Escape` closes it.

For resource arguments, `Tab` cycles names only. You can also type a full ID or an unambiguous ID prefix yourself. Names containing spaces are quoted automatically when completed. Unreadable resources with only an ID as their display name are omitted from completion; select them in the list or type their ID to repair them.

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

Requests, auths and variables also offer **Copy to workspace**: `Ctrl+Y` with vim/emacs, `Shift+F5` with commander, or `Ctrl+Shift+D` with client. Choose another workspace in the selector; `/` filters its names. The copy keeps its name unless that name is already taken, in which case a prompt asks for a new unique name. Cancel either step to leave everything as it was. The source and active workspace stay in place.

The same flow is available as `:copy-to <name>` on those screens, or `:rq copy-to users` from elsewhere. Each copy gets a new ID and keeps the entity's configuration and JSONC comments; saved request responses are not copied. Secrets are global and do not need a workspace copy.

When copying a request with a bound auth or variable references, choose **Carry dependencies** or **Request only**. Carrying includes variables referenced by the request and its auth. A conflicting request name always needs a new name. For a conflicting auth or variable, choose **Use existing**, **Replace existing**, or **Copy with new name**. Replacing preserves the destination entity's ID and updates its configuration, which also affects other requests using it. Renamed variables are rewired in the copied request and auth; the copied request is bound to the chosen auth's destination ID. Reusing a destination auth keeps its configuration and skips variables used only by the source auth.

For several variable conflicts, **Apply this choice to remaining variable conflicts** repeats that choice; choosing new names still asks for each name separately. A single conflict has no checkbox. All choices are collected before saving, so cancelling any dialog creates no copies and replaces nothing. If a dependency is missing in the source, repair it before carrying dependencies, or copy only the request. With **Request only**, references stay as configured and variables resolve from the destination workspace.

Copying an auth directly also checks its variable references. Choose **Carry variables** to copy them with the same conflict choices and reference remapping, or **Auth only** to leave its configuration unchanged and use the destination's variables. This works for bearer, basic, OAuth and custom auth configuration. Global secret references, cached auth results and custom auth's `{{value}}` placeholder are excluded from the variable-copy check.

## Editing

Editing a request opens a form: name, method, URL, auth, and the header and parameter tables. Bodies are not edited in the form — `Enter` on the body field hands the file to your `$EDITOR`, and Straumr picks the change back up when you close it. That is deliberate: your editor already knows JSON better than any box inside a terminal UI would.

In request, auth, variable and secret forms, `Ctrl+S` saves and keeps the form open; `Ctrl+Shift+S` saves and closes it. The commander preset uses `F2` and `Shift+F2`. Save and close returns to the list only after a successful save; validation or save errors keep your edits open. Both actions can be changed in [keybind settings](keybinds.md).

Request URLs can use variable and secret references, including `{{baseUrl}}/orders`. If a request URL is invalid or empty, either save action offers **Save anyway** so you can keep your other edits, including query parameters. **Cancel** keeps the form open with all edits intact. Fix the URL before sending the request; name validation still needs to pass before saving.

Switching a form tab focuses its first field. The shared `Straumr.NextTab`, `Straumr.NextTabAlternate`, and `Straumr.PreviousTab` actions control tab switching everywhere; `Tab` and `Shift+Tab` move between fields, including in header and query-parameter dialogs.

`Ctrl+E` on a list entry skips the form entirely and opens the underlying JSONC file. Comments you leave in that file survive everything Straumr writes afterwards.

Both paths need `$EDITOR` set. Without it, the commands that would hand off say so instead.

Type `{{` in any form field and a box of matching variable names opens under it; type `{{secret:` and it lists secrets instead. `Up` and `Down` move through it, `Enter` inserts the name and the closing braces, and `Escape` dismisses it — `Tab` is left alone so it still moves between fields. Keep typing to narrow the list.

A request and an auth show what they reference under **Variables & Secrets**, and whether each one resolves right now.

On Requests, focus **Variables & Secrets** with `Tab` and use the list navigation keys to highlight a reference (`j`/`k` or `Up`/`Down` with the vim preset). **Go to** (`Enter` or `Space`) opens its secret or workspace variable form on the value field. Existing references have their saved value; missing ones have the name prefilled so you can enter a value and create them. Saving refreshes availability when you return, and closing the form returns focus to the selected reference.

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
