# Keybindings

Every key Straumr reacts to is a named action, and every action can be rebound. Pick a preset for the overall feel, then override the handful of actions you want moved.

The bindings also cover the CLI's interactive prompts, so a choice made here follows you into `straumr create auth`, not only into the terminal UI.

## Choose a preset

Presets are `vim` (the default), `emacs`, `commander`, and `client`. They differ only in the actions listed in the tables below; everything else is shared.

```toml
keybind-preset = "emacs"
```

- **vim** — `j`/`k` to move, `g`/`G` for top and bottom, `/` to filter, `:` for the command prompt.
- **emacs** — `Ctrl+n`/`Ctrl+p` to move, `Ctrl+g` to back out of anything, `Alt+x` for the command prompt.
- **commander** — the function-key row: `F2` saves, `F4` edits, `F5` copies, `F7` creates, `F8` deletes, `F9` opens the command prompt.
- **client** — what a graphical API client trains into your fingers: `Ctrl+r` sends, `Ctrl+n` creates, `Ctrl+d` duplicates, `Delete` deletes, `Ctrl+f` finds.

All presets use the same navigation rules:

- **Up and down** moves through whatever is in front of you: list rows, headers, and the fields of an editor alike. Vim uses `j`/`k`, Emacs uses `Ctrl+n`/`Ctrl+p`, and commander and client use `Down`/`Up`. Arrow keys work in every preset. At the end of a list inside an editor, the same key carries on to the next field.
- **Left and right** moves between panes on a screen: the list, the request, its response. Vim uses `h`/`l`, Emacs uses `Ctrl+b`/`Ctrl+f`, and the arrow keys work in every preset.
- **`Tab` and `Shift+Tab`** switch tabs — **Body**, **Headers**, **Params** — in whichever pane or editor has focus. `Ctrl+PageDown` and `Ctrl+PageUp` do the same.
- **`Enter`** edits. Landing on a field only selects it. `Enter` opens a popup for that one value; a list such as the headers opens a popup for its row; a body opens your `$EDITOR`; a switch flips.
- **Inside a popup** — an edit box, a pick list, a confirmation — `Tab` moves forward through everything in it, fields and buttons alike, and `Shift+Tab` moves back. In a pick list they move through the options. `Enter` selects what has focus, and `Escape` closes the popup.

The footer shows the actions for the current focus. The command prompt and file-browser location input use `Tab` for completion, as shown in their hints.

By default, `Enter` and `Space` activate the selected row on a focused list. That means use a workspace, edit a resource, open a browser folder, or choose an onboarding option. `c` creates or adds, `e` edits, and `d` deletes or removes the focused item, including within editors and the file browser. The commander preset uses `F4` to edit. These are separate actions, so each key can be changed or disabled independently.

## Override single actions

Add a `[keybinds]` table to `~/.straumr/settings.toml`. Quote the action name — the dots are part of it.

```toml
keybind-preset = "vim"

[keybinds]
"Request.Send" = "Ctrl+r"
"Editor.Save" = "F2"
"Straumr.Interrupt" = "none"
```

An override is applied on top of the preset. `"none"` disables an action; `"default"`, or removing the line, restores it. An unknown action name or an unparsable key is reported when settings load, and that one line falls back to its default — the rest of the file still applies.

Straumr reloads settings when you close the editor you opened with `:settings`. A keybinding change rebuilds the interface in place, so the new keys are live immediately.

## Writing a key

A binding is an optional run of modifiers followed by one key, joined with `+`:

```text
s            Ctrl+r          Shift+Tab        Ctrl+Alt+Delete
```

- Modifiers are `Ctrl` (or `Control`), `Alt`, and `Shift`, in any order, each at most once.
- A single printable character binds that character. Only the movement actions — `First`/`Last` and `Top`/`Bottom` — tell an uppercase letter from its lowercase, which is how the vim preset gets both `g` and `G`. Every other action treats `s` and `S` as the same key, so bind those in lowercase.
- Named keys are `Enter`, `Escape`, `Tab`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `Up`, `Down`, `Left`, `Right`, `PageUp`, `PageDown`, and `F1`–`F12`. `Esc` and `Return` are accepted as spellings of `Escape` and `Enter`.
- `Space` and `Plus` name the two characters you cannot write directly.

Names ignore case, and so does a letter after `Ctrl` or `Alt`: `ctrl+R` and `Ctrl+r` are the same binding. Hints and these docs write a letter the way you press it — `Ctrl+r`, `s`, `G` — so a capital letter in a hint always means `Shift`.

Terminals differ in what they can deliver. `Ctrl+Shift+<letter>`, and some `Alt` combinations, never reach a terminal application on many setups — if a binding seems ignored, try it without `Shift` first.

## Every action

`·` means the preset leaves the default alone.


### Global

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Straumr.OpenCommandPrompt` | `:` | `Alt+x` | `F9` | `Ctrl+p` |
| `Straumr.Interrupt` | `Ctrl+c` | · | · | · |
| `Straumr.NextTab` | `Tab` | · | · | · |
| `Straumr.NextTabAlternate` | `Ctrl+PageDown` | · | · | · |
| `Straumr.PreviousTab` | `Shift+Tab` | · | · | · |
| `Straumr.PreviousTabAlternate` | `Ctrl+PageUp` | · | · | · |
| `Straumr.NextPane` | `l` | `Ctrl+f` | `Right` | `Right` |
| `Straumr.NextPaneAlternate` | `Right` | · | none | none |
| `Straumr.PreviousPane` | `h` | `Ctrl+b` | `Left` | `Left` |
| `Straumr.PreviousPaneAlternate` | `Left` | · | none | none |
| `Straumr.NextField` | `Tab` | · | · | · |
| `Straumr.PreviousField` | `Shift+Tab` | · | · | · |

`Straumr.NextField` and `Straumr.PreviousField` move through the fields, buttons, and pick-list options of a popup. Existing overrides for `Straumr.FocusNext` and `Straumr.FocusPreviousTab` also apply here. On screens and in editors, `Tab` switches tabs. In quick start it steps between a card's content and its buttons.

### Command prompt

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `CommandPrompt.Accept` | `Enter` | · | · | · |
| `CommandPrompt.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `CommandPrompt.Complete` | `Tab` | · | · | · |
| `CommandPrompt.HistoryPrevious` | `Up` | · | · | · |
| `CommandPrompt.HistoryNext` | `Down` | · | · | · |

### Lists, scrolling, and filtering

The request preview's **Variables & Secrets** pane uses `ResourceList` navigation actions. `ResourceList.Activate` and `ResourceList.ActivateAlternate` appear as **Go to**, opening the selected secret or workspace variable form, including creation for a missing reference.

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `ResourceList.Next` | `j` | `Ctrl+n` | `Down` | `Down` |
| `ResourceList.Previous` | `k` | `Ctrl+p` | `Up` | `Up` |
| `ResourceList.First` | `g` | `Home` | `Home` | `Home` |
| `ResourceList.Last` | `G` | `End` | `End` | `End` |
| `ResourceList.Activate` | `Enter` | · | · | · |
| `ResourceList.ActivateAlternate` | `Space` | · | · | · |
| `ResourceList.Up` | `Up` | · | · | · |
| `ResourceList.Down` | `Down` | · | · | · |
| `ResourceList.Home` | `Home` | · | · | · |
| `ResourceList.End` | `End` | · | · | · |
| `ResourceList.PageUp` | `PageUp` | `PageUp` | · | · |
| `ResourceList.PageDown` | `PageDown` | `Ctrl+v` | · | · |
| `ScrollableContent.Next` | `j` | `Ctrl+n` | `Down` | `Down` |
| `ScrollableContent.Previous` | `k` | `Ctrl+p` | `Up` | `Up` |
| `ScrollableContent.Top` | `g` | `Home` | `Home` | `Home` |
| `ScrollableContent.Bottom` | `G` | `End` | `End` | `End` |
| `ScrollableContent.Up` | `Up` | · | · | · |
| `ScrollableContent.Down` | `Down` | · | · | · |
| `ScrollableContent.Home` | `Home` | · | · | · |
| `ScrollableContent.End` | `End` | · | · | · |
| `ScrollableContent.PageUp` | `PageUp` | `PageUp` | · | · |
| `ScrollableContent.PageDown` | `PageDown` | `Ctrl+v` | · | · |
| `Select.Next` | `j` | `Ctrl+n` | `Down` | `Down` |
| `Select.Previous` | `k` | `Ctrl+p` | `Up` | `Up` |
| `Select.First` | `g` | `Home` | `Home` | `Home` |
| `Select.Last` | `G` | `End` | `End` | `End` |
| `ResourceFilter.Open` | `/` | `Ctrl+s` | `Ctrl+s` | `Ctrl+f` |
| `ResourceFilter.Accept` | `Enter` | · | · | · |
| `ResourceFilter.Cancel` | `Escape` | `Ctrl+g` | · | · |

### Panes and tabs

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `ResourceScreen.ResizeLeft` | `Ctrl+h` | · | · | · |
| `ResourceScreen.ResizeRight` | `Ctrl+l` | · | · | · |
| `ResourceScreen.ResizeUp` | `Ctrl+k` | · | · | · |
| `ResourceScreen.ResizeDown` | `Ctrl+j` | · | · | · |

### Workspaces

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Workspace.Create` | `c` | · | `F7` | `Ctrl+n` |
| `Workspace.Edit` | `e` | · | `F4` | · |
| `Workspace.Copy` | `y` | · | `F5` | `Ctrl+d` |
| `Workspace.Delete` | `d` | · | `F8` | `Delete` |
| `Workspace.Import` | `i` | · | · | · |
| `Workspace.Export` | `x` | · | · | · |

### Requests

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Request.New` | `c` | · | `F7` | `Ctrl+n` |
| `Request.Edit` | `e` | · | `F4` | · |
| `Request.Copy` | `y` | · | `F5` | `Ctrl+d` |
| `Request.CopyToWorkspace` | `Ctrl+y` | · | `Shift+F5` | `Ctrl+Shift+d` |
| `Request.Delete` | `d` | · | `F8` | `Delete` |
| `Request.EditJson` | `Ctrl+e` | · | · | · |
| `Request.Send` | `s` | · | · | `Ctrl+r` |
| `Request.Fullscreen` | `v` | · | `F3` | · |

### Auths

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Auth.New` | `c` | · | `F7` | `Ctrl+n` |
| `Auth.Edit` | `e` | · | `F4` | · |
| `Auth.Copy` | `y` | · | `F5` | `Ctrl+d` |
| `Auth.CopyToWorkspace` | `Ctrl+y` | · | `Shift+F5` | `Ctrl+Shift+d` |
| `Auth.Delete` | `d` | · | `F8` | `Delete` |
| `Auth.EditJson` | `Ctrl+e` | · | · | · |
| `Auth.Send` | `s` | · | · | `Ctrl+r` |
| `Auth.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `Auth.ExtractHelp` | `i` | · | · | · |
| `Auth.ExtractHelp.Function` | `F1` | · | · | · |

### Variables

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Variable.New` | `c` | · | `F7` | `Ctrl+n` |
| `Variable.Fetch` | `s` | · | · | · |
| `Variable.Edit` | `e` | · | `F4` | · |
| `Variable.Copy` | `y` | · | `F5` | `Ctrl+d` |
| `Variable.CopyToWorkspace` | `Ctrl+y` | · | `Shift+F5` | `Ctrl+Shift+d` |
| `Variable.Delete` | `d` | · | `F8` | `Delete` |
| `Variable.EditJson` | `Ctrl+e` | · | · | · |

### Secrets

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Secret.New` | `c` | · | `F7` | `Ctrl+n` |
| `Secret.Edit` | `e` | · | `F4` | · |
| `Secret.Copy` | `y` | · | `F5` | `Ctrl+d` |
| `Secret.Delete` | `d` | · | `F8` | `Delete` |
| `Secret.EditJson` | `Ctrl+e` | · | · | · |

### Responses

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Response.Send` | `s` | · | · | `Ctrl+r` |
| `Response.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `Response.Back` | `Escape` | `Ctrl+g` | · | · |
| `ResponseBody.Format` | `b` | · | · | · |
| `ResponseBody.Highlight` | `c` | · | · | · |
| `ResponseBody.Copy` | `y` | · | `F5` | · |
| `ResponseBody.Open` | `e` | · | · | · |
| `Headers.Copy` | `y` | · | `F5` | · |
| `Headers.Expand` | `e` | · | · | · |

A headers tab moves by header rather than by line, so `ResourceList.Next`, `Previous`, `First`, and `Last` drive it — the same keys that move any list. `Headers.Copy` copies the selected header's value, and `Headers.Expand` unfolds a value too long for one line.

### Editing fields

`EditorField` actions open the popup for a single-value field — a name, a URL, a method. `ContentField` actions open a body in `$EDITOR`. In an editor, `ResourceList.Next` and `ResourceList.Previous` (and the arrow keys) move between fields.

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Editor.Save` | `Ctrl+s` | · | `F2` | · |
| `Editor.SaveAndClose` | `Ctrl+Shift+s` | · | `Shift+F2` | · |
| `Editor.Close` | `Escape` | `Ctrl+g` | · | · |
| `EditorField.Activate` | `Enter` | · | · | · |
| `EditorField.ActivateAlternate` | `Space` | · | · | · |
| `EditorField.Edit` | `e` | · | `F4` | · |
| `ContentField.Edit` | `e` | · | `F4` | · |
| `ContentField.Activate` | `Enter` | · | · | · |
| `ContentField.ActivateAlternate` | `Space` | · | · | · |
| `ContentField.Edit.Control` | `Ctrl+e` | · | · | · |
| `KeyValueField.Add` | `c` | · | `F7` | `Ctrl+n` |
| `KeyValueField.Edit` | `e` | · | `F4` | · |
| `KeyValueField.Remove` | `d` | · | `F8` | `Delete` |
| `SecretSuggestions.Next` | `Down` | · | · | · |
| `SecretSuggestions.Previous` | `Up` | · | · | · |
| `SecretSuggestions.Insert` | `Enter` | · | · | · |
| `SecretSuggestions.Dismiss` | `Escape` | `Ctrl+g` | · | · |

### Dialogs

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `StraumrDialog.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `ConfirmDialog.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `TextPromptDialog.Submit` | `Enter` | · | · | · |
| `KeyValuePairDialog.Submit` | `Enter` | · | · | · |
| `WorkspaceFormDialog.Submit` | `Enter` | · | · | · |
| `BrowserDialog.Up` | `Backspace` | · | · | · |
| `BrowserDialog.Select` | `s` | · | · | · |
| `BrowserDialog.Select.Current` | `Ctrl+Enter` | · | · | · |
| `BrowserDialog.New` | `c` | · | `F7` | `Ctrl+n` |
| `BrowserDialog.Rename` | `e` | · | `F4` | · |
| `BrowserDialog.Delete` | `d` | · | `F8` | `Delete` |
| `BrowserDialog.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `BrowserDialog.Location` | `Ctrl+l` | · | · | · |
| `BrowserDialog.Location.Go` | `Enter` | · | · | · |
| `BrowserDialog.Location.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `BrowserDialog.Location.Complete` | `Tab` | · | · | · |

### Quick start

Quick start has no actions of its own; it follows the same rules as the rest of the app. A card reads as one column: up and down move through its items and carry on past the last one to **Back** and **Next**, and back up again. `Tab` and `Shift+Tab` step between the content, **Back**, and **Next**. `Enter` chooses or presses. Its screen command examples show the active command prompt key.

### CLI prompts

| Action | vim | emacs | commander | client |
| --- | --- | --- | --- | --- |
| `Cli.Cancel` | `Escape` | `Ctrl+g` | · | · |
| `Cli.Next` | `j` | `Ctrl+n` | `Down` | `Down` |
| `Cli.NextUpper` | `J` | · | · | · |
| `Cli.Previous` | `k` | `Ctrl+p` | `Up` | `Up` |
| `Cli.PreviousUpper` | `K` | · | · | · |
| `Cli.First` | `g` | `Home` | `Home` | `Home` |
| `Cli.Last` | `G` | `End` | `End` | `End` |
| `Cli.Search` | `/` | `Ctrl+s` | · | `Ctrl+f` |

The same action name can be bound in several places — `Request.Edit` and `Workspace.Edit` are separate actions so you can move one without moving the other, and the presets change them together.
