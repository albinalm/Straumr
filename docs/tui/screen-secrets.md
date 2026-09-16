# Secrets Screen (S1)

Part of the [TUI implementation guide](./README.md). Implemented; awaiting terminal acceptance.

## Scope and layout

Secrets belong to the global store, so this screen works without an active workspace and when
the active workspace cannot be read. The header keeps the active workspace as context only.
It uses the accepted shared shell: a two-line list and filter on the left, a three-row summary
over Secret and Known references on the right. Pane shares persist under `Secrets` independently
of the other screens.

The list sorts by last access and then name, preserves selection by registry ID, and filters by
name or ID. Inspection never stamps access times. Broken JSON, missing files, missing names,
null values and mismatched IDs remain as broken rows named by their short registry ID. An empty
store, no filter matches and a failed load each have their own message.

The Secret pane shows a fixed twelve-character mask for a populated value, `not set` for an empty
value, modified/access timestamps and the storage path. It never prints the stored value. The
summary identifies the selected name, global scope and short ID.

## Known references

The index scans requests and auths in every registered workspace through Core with
`updateLastAccessed: false`. It is rebuilt on entry, explicit refresh and after mutations;
moving the selection performs no I/O. It scans request URLs, header/parameter values, stored
bodies and auth configuration. Cached OAuth tokens and custom-auth results are excluded.
JSON string values are decoded before matching so serialized escapes do not change the names.
Matching trims the placeholder's name and ignores case, as Core's resolver does. Repeated
occurrences in a field produce one entry.

Each entry names the resource, workspace, kind and field without showing the field's value.
The pane also gives the literal `{{secret:name}}` placeholder for use in stored fields. These
are direct stored references, not a transitive graph of requests using an auth. Unregistered
workspaces are outside the scan. Registry entries whose workspace files are absent are skipped,
matching Workspaces' treatment of workspaces removed outside Straumr. Existing unreadable
workspaces and unreadable resources are counted and reported;
zero known references with incomplete coverage is not presented as proof of no dependencies.
A broken secret's name cannot be matched until repaired.

## Actions and commands

| Key | Local command | Behavior |
| --- | --- | --- |
| `Enter` / `e` | `:edit [name or ID]` | Edit a readable secret in the form; repair a broken one in `$EDITOR` |
| `c` | `:create` / `:new` | Create a global secret |
| `y` | `:copy [name or ID]` | Copy the value into a new resource with a blank name |
| `d` | `:delete [name or ID]` | Confirm deletion, including known dependent-resource count and scan limitations |
| `Ctrl+E` | `:json [name or ID]` | Edit the stored JSON through `$EDITOR` |
| `/` | — | Filter the list by name or ID |
| — | `:select <name or ID>` / `:s <name or ID>` | Select an exact or unambiguous prefix match, clearing the filter |
| — | `:refresh` | Reload the global store and known references |

Every named action works from every screen through `:secret` / `:sc`, for example
`:sc edit "oauth client"`. Names with spaces use the existing quoted argument parser and
completion. Omitted subjects act on the selection. Navigation requires the exact namespace or
alias. No secret operation requires an active workspace.

Secrets also exposes the shell's existing `:rq`, `:au` and `:ws` namespaces. A full-screen form
opened from another screen returns to that caller on close through `TransientScreenOpened` /
`TransientScreenClosed`, just as Requests and Auths do. Delete dialogs and external JSON editing
leave the destination screen visible. Missing-editor errors use the existing footer.

## Editing and persistence

`SecretEditor` supplies Name and Value to `ResourceEditorView`. The Value field is masked except
while focused, matching Auths. Name/Value changes are compared with the last saved state; `Ctrl+S`
saves and leaves the editor open, the shared marker flashes, and `Escape` confirms unsaved work.
Create transitions to update after its first save. Editing uses a separate working copy and
preserves access timestamps. Values retain whitespace. Empty values are permitted. The form and
Core reject blank names and double quotes.

Renaming does not rewrite references; the editor says so. Delete removes the global secret and
its registry entry without changing references. The confirmation counts distinct directly
referencing resources rather than repeated fields in the same resource.

JSON edits reject invalid content and changed IDs before touching storage. A valid edit goes
through Core for conflict checks and modified timestamps. Missing files open on a scaffold under
their existing registry ID and can be restored; closing the editor unchanged leaves them missing.
The external editor owns multiline values and raw JSON changes.

## Evidence and terminal checkpoint

- Debug and Release solution builds and the Release CLI-only build pass with zero warnings.
  CLI `create secret --help` renders.
- The isolated `.tmp/s1-check` diagnostic covers two workspaces, direct request/auth references,
  case/whitespace matching, repeated references, cached-token exclusion, incomplete coverage,
  empty/broken/missing entries, completion before visiting Secrets with no active workspace,
  quoted selection, fixed masking and shared amber/junctions. Before/after file comparisons
  verify that inspection writes nothing. The form save path creates once, updates the same
  identity on its next save, preserves access times and value whitespace, and rejects conflicts.
  Missing-entry deletion and access with a broken active workspace also pass.
- Screen snapshots at 120x30, 80x24, 70x20 and 44x14 and an unfocused editor snapshot contain no
  fixture credential values. The existing snapshot limitation applies: natural-size rendering
  clips narrow captures instead of proving real viewport reflow. Snapshots do not apply focus.
- No input harness was built. In a terminal, check the three focus regions and resizing, filtering,
  create/edit/copy, repeated saves and unsaved cancellation, Value reveal/remasking, delete cancel
  and confirm, JSON repair, and both directions of cross-screen commands. In particular, try
  `:rq edit <request>` from Secrets and `:sc edit <secret>` from Requests, then close each form
  and confirm the return to its caller. Check `Tab` completion inside each namespace.

The [validation checklist](./validation-checklist.md) keeps these interactive checks unticked.
