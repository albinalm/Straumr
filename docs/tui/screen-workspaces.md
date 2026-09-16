# Workspaces Screen Specification

Part of the [TUI implementation guide](./README.md). The accepted screen and the
design baseline for every screen after it. Read when changing Workspaces, or when
you need a worked example of the shared scaffold in use.

## Layout

Header:

- left: `{straumr} workspaces`
- right: `active workspace demo`, using the actual active workspace name

Left pane:

- filter affordance and a recessed count badge in a three-row bar, mirroring the
  detail summary and its identifier badge
- title `Workspaces` on the rule closing that bar, beside the detail section titles
- one multiline item per workspace, the first starting on the same row as the first
  detail value
- a green dot on the active workspace's middle line, in the gutter between the
  selection bar and the text, so the list itself says which workspace is live. The
  middle line is what reads as centred against a three-line row
- the marker follows activation, so pressing `Enter` moves it without a reload
- workspace name, trimmed with a trailing ellipsis
- request and auth counts, amber when the workspace holds anything and inert when
  it holds nothing
- workspace directory, trimmed with a leading ellipsis so the tail stays visible
- selection band spanning the panel inset with a leading accent bar; the selected
  row resolves to the bright foregrounds

Detail header:

- workspace name
- last-accessed timestamp, relative for recent values
- shortened workspace ID aligned right

Details group:

- titled `Details` rather than by the resource type, which the screen name and the
  summary bar directly above it already carry
- path, home-shortened and wrapped, ellipsized when the region is too short
- request count
- auth count
- modified timestamp

Requests group:

- requests from the selected workspace
- ordered by `LastAccessed` descending
- HTTP method with the shared semantic color mapping
- request name
- relative last-used timestamp
- no auth entries or generic `Contents` label

Do not display:

- workspace validity labels
- a Secrets group
- invented environment or scope values

## Initial Data Flow

1. Load options once before the screen becomes interactive.
2. Load workspaces through `IStraumrWorkspaceService.ListAsync` without updating
   their access timestamps.
3. Join each loaded workspace to its `StraumrWorkspaceEntry` for its configured
   path and current-workspace identity.
4. Populate one TUI presentation item per loaded workspace.
5. Select the current workspace when present; otherwise select the first item.
6. Load requests for the selected workspace through
   `IStraumrRequestService.ListAsync`.
7. Sort the selected workspace's requests by `LastAccessed` descending.
8. Update the details reactively when selection changes.

Selection must not mutate `LastAccessed`. Activation and other explicit actions
may persist changes through the appropriate Core service.

## Interaction

- The row under the pointer shows a hover band; the selection band and its marker
  desaturate while the list does not hold focus.
- Arrow, Home/End and Page keys and pointer behavior are owned by `ResourceList`,
  which replaced `ListBox<T>` once its fixed one-row item height ruled it out.
- `j` and `k` move the workspace selection when the list owns focus.
- `Enter` activates the selected workspace through
  `IStraumrWorkspaceService.ActivateAsync`. Two clicks on the same row do the same, so
  activation is reachable without the keyboard.
- `/` focuses the inline workspace filter.
- `:` opens the shared command prompt from anywhere on the screen, including from
  inside the request preview.
- `c`, `e`, `y`, `x`, `i`, and `d` are introduced with their corresponding
  lifecycle operations, not as inert hints.
- Destructive actions require an explicit confirmation surface.

## Filtering

- `/` focuses the inline filter without typing the trigger into the query.
- Pointer selection of the filter is supported without making it the initial focus
  target or a Tab stop.
- Filtering updates as text changes and matches workspace names and configured paths,
  case-insensitively.
- The count badge shows the total when no filter is active and `matches/total` while
  filtering.
- If the selected workspace remains visible, selection stays on it. Otherwise the
  first match is selected; no matches clear the detail panes and show an empty state
  on the still-focusable list surface.
- `Enter` keeps the current filter and returns focus to the result list. `Escape`
  clears the filter and returns focus to the list.
- `:select <name>` and `:use <name>` clear an active filter when necessary so a
  command can select any workspace, not only a visible match.
- Every action the screen offers under a key is also a command, so another screen can
  reach it through `:ws …` / `:workspace …`:

  | Command | Key | Does |
  | --- | --- | --- |
  | `:select <name>` / `:w <name>` | — | Selects a workspace, clearing a filter to reach it |
  | `:use [name]` / `:activate [name]` | `Enter` | Activates it; from another screen this runs in place |
  | `:create` / `:new` | `c` | Opens the create form |
  | `:edit [name]` | `e` | Opens the workspace file in `$EDITOR` |
  | `:copy [name]` | `y` | Opens the copy form beside the source |
  | `:delete [name]` | `d` | Asks the delete confirmation |
  | `:import` | `i` | Opens the archive browser |
  | `:export [name]` | `x` | Opens the folder browser |
  | `:refresh` | — | Reloads the registry |

  Each acts on the workspace it names and on the selection when it names none, which is
  what the key does. `:copy` and `:export` refuse an unreadable workspace in the words the
  command bar uses to withdraw them. A command sent here from another screen leaves the
  reader on Workspaces: nothing this screen opens is a full-screen surface to come back
  from, and what it did is what they are now looking at. `:use` is the exception that was
  always the exception — it runs in place and never changes the visible screen.

## Unreadable Workspaces

A registry entry whose file is not a workspace stays on the list instead of vanishing
from it. It is named after the folder Core created for it, since there is no name
inside the file to read, and it reads red on every row level so it is recognisable
without being selected.

- Two things make an entry unreadable: its file is not valid JSON, or the ID inside it
  is not the ID the registry has. Both are shown the same way and both are repaired the
  same way.
- An entry whose file is simply gone is not shown. That is a workspace removed from
  outside Straumr, not one in trouble, and Core's own listing drops it too.
- The summary bar reads the name and `cannot be read`; the Details pane names the path,
  states the problem in red, and says `e` opens the file for repair. The Requests pane
  says only that it is unavailable, because nothing can be read to list.
- It cannot be activated. `Enter`, a double-click and `:use` all report why instead,
  and `Copy` and `Export` withdraw from the command bar. `Edit` and `Delete` stay,
  because repairing it and removing it are the two things left to do with it.
- One unreadable workspace never fails the load. Each registry entry is read on its own,
  so the failure is scoped to the row it belongs to.

An edit that produces one of the two is written to the workspace file rather than
discarded, and the workspace is listed as unreadable until it is repaired. `e` on it
reopens exactly the text that needs fixing, so a mistyped brace costs a keystroke rather
than the edit.

## Command Prompt

The commands the Workspaces screen answers, in addition to the app's own:

- `select <name>` selects a workspace without activating it. A name resolves by
  exact match, then unique prefix; an ambiguous prefix names the workspaces it
  matched.
- `use [name]` activates a workspace through `IStraumrWorkspaceService.ActivateAsync`,
  the named one or the selected one. `Enter` on the list does the same thing.
- `refresh` reloads the registry through Core, drops the cached request previews and
  keeps the selected workspace selected. It is the explicit refresh the load-once
  rule refers to.
- `rq <command>` and `request <command>` navigate to Requests and run one of that
  screen's commands after it loads. In particular, `rq send <name>` and
  `request send <name>` use Requests' own send path; without an active workspace the
  footer reports the problem instead of starting a send.
- A successful namespaced send opens its full-screen response as a transient child;
  closing that view reloads and returns here. The return target is recorded by the shell,
  not encoded as Workspaces behavior.
- Bare `request` / `rq` shows Requests. Bare `workspace` / `ws` identifies this screen;
  the plural command spellings are deliberately absent.

`quit`, aliased `q` and `exit`, belongs to the application root and is the only way
out of the app; the framework's own quit gesture is removed on startup.

`Tab` completes: command names in the first token, workspace names after `select`
and `use`. Names containing spaces are quoted automatically. `Up` and `Down` walk the prompt's history. A command that succeeds and
has nothing to report says nothing, because the list, the dot and the header already
show what changed; only `refresh` and failures produce a message.

Commands that would duplicate a gesture are deliberately absent. There is no `next`,
`first` or `last`, because `j`, `k`, `g` and `G` already move the selection, and no
screen-switching commands until there is a second screen to switch to.

## Loading and Failure Behavior

- Show a spinner and concise loading text during initial I/O.
- Show a clear empty state when no workspaces exist.
- Keep the selected workspace visible while its request preview loads.
- Show recoverable operation failures in the screen or a framework toast/dialog.
- Do not swallow unexpected exceptions.
- Cancellation exits or abandons the operation without presenting it as a
  failure.

## W7 Workflow Checkpoints

W7 is split by interaction shape so each reusable surface is reviewed before the
next one builds on it:

1. W7a: delete confirmation, async mutation, refresh, notification, and focus
   restoration.
2. W7b: create and copy input forms.
3. W7c: import and export path forms.
4. W7d: external-editor edit orchestration and terminal focus restoration.

Gesture callbacks only capture intent or close their surface. Core I/O runs from
the screen update path, then the screen reloads through the same retained state used
by `refresh`. Dialogs and forms use framework controls and shared Straumr styles.

## Definition of Done for Workspaces

- The implemented screen matches the approved information hierarchy.
- All displayed values come from Core or derived TUI presentation state.
- No data access occurs during rendering or the terminal update loop.
- Native controls own selection, focus, scrolling, and invalidation.
- All visible shortcuts perform their advertised action.
- The TUI, CLI help path, CLI-only build, and full build remain functional.
- Loading, empty, error, cancellation, and resize behavior have been exercised.
- [status.md](./status.md) records completed milestones and any accepted deviations.
- Everything not specific to workspaces lives in `Visuals/Shared` or `Formatting`,
  and a second screen can be built without touching layout or palette code.
