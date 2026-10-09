# Keybindings

Every key Straumr reacts to is a named action, and every action can be rebound. Pick a preset for the overall feel, then override the handful of actions you want moved.

The bindings also cover the CLI's interactive prompts, so a choice made here follows you into `straumr create auth`, not only into the terminal UI.

## Choose a preset

Presets are `vim` (the default), `emacs`, `commander`, and `client`. They differ only in the actions listed in the tables below; everything else is shared.

```toml
keybind-preset = "emacs"
```

- **vim** — `j`/`k` to move, `g`/`G` for top and bottom, `/` to filter, `:` for the command prompt.
- **emacs** — `Ctrl+N`/`Ctrl+P` to move, `Ctrl+G` to back out of anything, `Alt+X` for the command prompt.
- **commander** — the function-key row: `F2` saves, `F4` edits, `F5` copies, `F7` creates, `F8` deletes, `F9` opens the command prompt.
- **client** — what a graphical API client trains into your fingers: `Ctrl+R` sends, `Ctrl+N` creates, `Ctrl+D` duplicates, `Delete` deletes, `Ctrl+F` finds.

All presets use the same navigation rules:

- `Tab` moves focus to the next field, pane, or button; `Shift+Tab` moves it back.
- `Ctrl+T` or `Ctrl+PageDown` switches to the next tab in the focused pane; `Ctrl+PageUp` switches to the previous tab. These work in previews and editors, including while typing.
- Movement keys select items within a list or change a choice such as the request method. Vim uses `j`/`k`, Emacs uses `Ctrl+N`/`Ctrl+P`, and commander and client use `Down`/`Up`. Arrow keys also work in lists and choices in every preset.
- Text fields accept normal typing and caret movement. Movement keys do not jump between fields.

The footer shows the actions for the current focus. Quick start includes a practice screen using your selected preset's bindings. The command prompt and file-browser location input use `Tab` for completion, as shown in their hints.

By default, `Enter` and `Space` activate the selected row on a focused list. That means use a workspace, edit a resource, open a browser folder, or choose an onboarding option. `c` creates or adds, `e` edits, and `d` deletes or removes the focused item, including within editors and the file browser. The commander preset uses `F4` to edit. These are separate actions, so each key can be changed or disabled independently.

## Override single actions

Add a `[keybinds]` table to `~/.straumr/settings.toml`. Quote the action name — the dots are part of it.

```toml
keybind-preset = "vim"

[keybinds]
"Request.Send" = "Ctrl+R"
"Editor.Save" = "F2"
"Straumr.Interrupt" = "none"
```

An override is applied on top of the preset. `"none"` disables an action; `"default"`, or removing the line, restores it. An unknown action name or an unparsable key is reported when settings load, and that one line falls back to its default — the rest of the file still applies.

Straumr reloads settings when you close the editor you opened with `:settings`. A keybinding change rebuilds the interface in place, so the new keys are live immediately.

## Writing a key

A binding is an optional run of modifiers followed by one key, joined with `+`:

```text
s            Ctrl+R          Shift+Tab        Ctrl+Alt+Delete
```

- Modifiers are `Ctrl` (or `Control`), `Alt`, and `Shift`, in any order, each at most once.
- A single printable character binds that character. It is case-sensitive: `g` and `G` are different bindings, which is how the vim preset gets both.
- Named keys are `Enter`, `Escape`, `Tab`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `Up`, `Down`, `Left`, `Right`, `PageUp`, `PageDown`, and `F1`–`F12`. `Esc` and `Return` are accepted as spellings of `Escape` and `Enter`.
- `Space` and `Plus` name the two characters you cannot write directly.

Names ignore case, so `ctrl+r` and `Ctrl+R` are the same binding.

Terminals differ in what they can deliver. `Ctrl+Shift+<letter>`, and some `Alt` combinations, never reach a terminal application on many setups — if a binding seems ignored, try it without `Shift` first.

## Every action

`·` means the preset leaves the default alone.


### Global

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Straumr.OpenCommandPrompt` | `:` | `Alt+X` | `F9` | `Ctrl+P` |
| `Straumr.Interrupt` | `Ctrl+C` | · | · | · |
| `Straumr.FocusNext` | `Tab` | · | · | · |
| `Straumr.FocusPreviousTab` | `Shift+Tab` | · | · | · |
| `Straumr.NextTab` | `Ctrl+T` | · | · | · |
| `Straumr.NextTabAlternate` | `Ctrl+PageDown` | · | · | · |
| `Straumr.PreviousTab` | `Ctrl+PageUp` | · | · | · |

### Command prompt

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `CommandPrompt.Accept` | `Enter` | · | · | · |
| `CommandPrompt.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `CommandPrompt.Complete` | `Tab` | · | · | · |
| `CommandPrompt.HistoryPrevious` | `Up` | · | · | · |
| `CommandPrompt.HistoryNext` | `Down` | · | · | · |

### Lists, scrolling, and filtering

The request preview's **Variables & Secrets** pane uses `ResourceList` navigation actions. `ResourceList.Activate` and `ResourceList.ActivateAlternate` appear as **Go to**, opening the selected secret or workspace variable form, including creation for a missing reference.

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `ResourceList.Next` | `j` | `Ctrl+N` | `Down` | `Down` |
| `ResourceList.Previous` | `k` | `Ctrl+P` | `Up` | `Up` |
| `ResourceList.First` | `g` | `Home` | `Home` | `Home` |
| `ResourceList.Last` | `G` | `End` | `End` | `End` |
| `ResourceList.Activate` | `Enter` | · | · | · |
| `ResourceList.ActivateAlternate` | `Space` | · | · | · |
| `ResourceList.Up` | `Up` | · | · | · |
| `ResourceList.Down` | `Down` | · | · | · |
| `ResourceList.Home` | `Home` | · | · | · |
| `ResourceList.End` | `End` | · | · | · |
| `ResourceList.PageUp` | `PageUp` | `PageUp` | · | · |
| `ResourceList.PageDown` | `PageDown` | `Ctrl+V` | · | · |
| `ScrollableContent.Next` | `j` | `Ctrl+N` | `Down` | `Down` |
| `ScrollableContent.Previous` | `k` | `Ctrl+P` | `Up` | `Up` |
| `ScrollableContent.Top` | `g` | `Home` | `Home` | `Home` |
| `ScrollableContent.Bottom` | `G` | `End` | `End` | `End` |
| `ScrollableContent.Up` | `Up` | · | · | · |
| `ScrollableContent.Down` | `Down` | · | · | · |
| `ScrollableContent.Home` | `Home` | · | · | · |
| `ScrollableContent.End` | `End` | · | · | · |
| `ScrollableContent.PageUp` | `PageUp` | `PageUp` | · | · |
| `ScrollableContent.PageDown` | `PageDown` | `Ctrl+V` | · | · |
| `Select.Next` | `j` | `Ctrl+N` | `Down` | `Down` |
| `Select.Previous` | `k` | `Ctrl+P` | `Up` | `Up` |
| `Select.First` | `g` | `Home` | `Home` | `Home` |
| `Select.Last` | `G` | `End` | `End` | `End` |
| `ResourceFilter.Open` | `/` | `Ctrl+S` | `Ctrl+S` | `Ctrl+F` |
| `ResourceFilter.Accept` | `Enter` | · | · | · |
| `ResourceFilter.Cancel` | `Escape` | `Ctrl+G` | · | · |

### Panes and tabs

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `ResourceScreen.ResizeLeft` | `Ctrl+H` | · | · | · |
| `ResourceScreen.ResizeRight` | `Ctrl+L` | · | · | · |
| `ResourceScreen.ResizeUp` | `Ctrl+K` | · | · | · |
| `ResourceScreen.ResizeDown` | `Ctrl+J` | · | · | · |

### Workspaces

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Workspace.Create` | `c` | · | `F7` | `Ctrl+N` |
| `Workspace.Edit` | `e` | · | `F4` | · |
| `Workspace.Copy` | `y` | · | `F5` | `Ctrl+D` |
| `Workspace.Delete` | `d` | · | `F8` | `Delete` |
| `Workspace.Import` | `i` | · | · | · |
| `Workspace.Export` | `x` | · | · | · |

### Requests

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Request.New` | `c` | · | `F7` | `Ctrl+N` |
| `Request.Edit` | `e` | · | `F4` | · |
| `Request.Copy` | `y` | · | `F5` | `Ctrl+D` |
| `Request.CopyToWorkspace` | `Ctrl+Y` | · | `Shift+F5` | `Ctrl+Shift+D` |
| `Request.Delete` | `d` | · | `F8` | `Delete` |
| `Request.EditJson` | `Ctrl+E` | · | · | · |
| `Request.Send` | `s` | · | · | `Ctrl+R` |
| `Request.Fullscreen` | `v` | · | `F3` | · |

### Auths

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Auth.New` | `c` | · | `F7` | `Ctrl+N` |
| `Auth.Edit` | `e` | · | `F4` | · |
| `Auth.Copy` | `y` | · | `F5` | `Ctrl+D` |
| `Auth.CopyToWorkspace` | `Ctrl+Y` | · | `Shift+F5` | `Ctrl+Shift+D` |
| `Auth.Delete` | `d` | · | `F8` | `Delete` |
| `Auth.EditJson` | `Ctrl+E` | · | · | · |
| `Auth.Send` | `s` | · | · | `Ctrl+R` |
| `Auth.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `Auth.ExtractHelp` | `h` | · | · | · |
| `Auth.ExtractHelp.Function` | `F1` | · | · | · |

### Variables

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Variable.New` | `c` | · | `F7` | `Ctrl+N` |
| `Variable.Edit` | `e` | · | `F4` | · |
| `Variable.Copy` | `y` | · | `F5` | `Ctrl+D` |
| `Variable.CopyToWorkspace` | `Ctrl+Y` | · | `Shift+F5` | `Ctrl+Shift+D` |
| `Variable.Delete` | `d` | · | `F8` | `Delete` |
| `Variable.EditJson` | `Ctrl+E` | · | · | · |

### Secrets

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Secret.New` | `c` | · | `F7` | `Ctrl+N` |
| `Secret.Edit` | `e` | · | `F4` | · |
| `Secret.Copy` | `y` | · | `F5` | `Ctrl+D` |
| `Secret.Delete` | `d` | · | `F8` | `Delete` |
| `Secret.EditJson` | `Ctrl+E` | · | · | · |

### Responses

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Response.Send` | `s` | · | · | `Ctrl+R` |
| `Response.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `Response.Back` | `Escape` | `Ctrl+G` | · | · |
| `ResponseBody.Format` | `b` | · | · | · |
| `ResponseBody.Highlight` | `h` | · | · | · |
| `ResponseBody.Copy` | `y` | · | `F5` | · |
| `ResponseBody.Open` | `e` | · | · | · |
| `Headers.Copy` | `y` | · | `F5` | · |
| `Headers.Expand` | `e` | · | · | · |

A headers tab moves by header rather than by line, so `ResourceList.Next`, `Previous`, `First`, and `Last` drive it — the same keys that move any list. `Headers.Copy` copies the selected header's value, and `Headers.Expand` unfolds a value too long for one line.

### Editing fields

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Editor.Save` | `Ctrl+S` | · | `F2` | · |
| `Editor.SaveAndClose` | `Ctrl+Shift+S` | · | `Shift+F2` | · |
| `Editor.Close` | `Escape` | `Ctrl+G` | · | · |
| `ContentField.Edit` | `e` | · | `F4` | · |
| `ContentField.Activate` | `Enter` | · | · | · |
| `ContentField.ActivateAlternate` | `Space` | · | · | · |
| `ContentField.Edit.Control` | `Ctrl+E` | · | · | · |
| `KeyValueField.Add` | `c` | · | `F7` | `Ctrl+N` |
| `KeyValueField.Edit` | `e` | · | `F4` | · |
| `KeyValueField.Remove` | `d` | · | `F8` | `Delete` |
| `SecretSuggestions.Next` | `Down` | · | · | · |
| `SecretSuggestions.Previous` | `Up` | · | · | · |
| `SecretSuggestions.Insert` | `Enter` | · | · | · |
| `SecretSuggestions.Dismiss` | `Escape` | `Ctrl+G` | · | · |

### Dialogs

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `StraumrDialog.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `ConfirmDialog.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `ConfirmDialog.Left` | `Left` | · | · | · |
| `ConfirmDialog.Right` | `Right` | · | · | · |
| `ConfirmDialog.Up` | `Up` | · | · | · |
| `ConfirmDialog.Down` | `Down` | · | · | · |
| `TextPromptDialog.Submit` | `Enter` | · | · | · |
| `KeyValuePairDialog.Submit` | `Enter` | · | · | · |
| `WorkspaceFormDialog.Submit` | `Enter` | · | · | · |
| `BrowserDialog.Up` | `Backspace` | · | · | · |
| `BrowserDialog.Select` | `s` | · | · | · |
| `BrowserDialog.Select.Current` | `Ctrl+Enter` | · | · | · |
| `BrowserDialog.New` | `c` | · | `F7` | `Ctrl+N` |
| `BrowserDialog.Rename` | `e` | · | `F4` | · |
| `BrowserDialog.Delete` | `d` | · | `F8` | `Delete` |
| `BrowserDialog.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `BrowserDialog.Location` | `Ctrl+L` | · | · | · |
| `BrowserDialog.Location.Go` | `Enter` | · | · | · |
| `BrowserDialog.Location.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `BrowserDialog.Location.Complete` | `Tab` | · | · | · |

### Quick start

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `QuickStart.Advance` | `Enter` | · | · | · |

Quick start uses the shared list activation, focus, scrolling, and dialog direction actions for its other shortcuts. Its screen command examples show the active command prompt key.

### CLI prompts

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Cli.Cancel` | `Escape` | `Ctrl+G` | · | · |
| `Cli.Next` | `j` | `Ctrl+N` | `Down` | `Down` |
| `Cli.NextUpper` | `J` | · | · | · |
| `Cli.Previous` | `k` | `Ctrl+P` | `Up` | `Up` |
| `Cli.PreviousUpper` | `K` | · | · | · |
| `Cli.First` | `g` | `Home` | `Home` | `Home` |
| `Cli.Last` | `G` | `End` | `End` | `End` |
| `Cli.Search` | `/` | `Ctrl+S` | · | `Ctrl+F` |

The same action name can be bound in several places — `Request.Edit` and `Workspace.Edit` are separate actions so you can move one without moving the other, and the presets change them together.
