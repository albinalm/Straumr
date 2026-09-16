# Auths Screen (A1)

Part of the [TUI implementation guide](./README.md). The active milestone: what A1
implements, what evidence exists, and where to resume.

## Implemented behavior

- Auths belong to the active workspace. Entry and refresh load through Core without
  stamping access times, sort by last access, and preserve selection where possible.
  A missing active workspace points back to Workspaces. Missing auth files are skipped;
  unreadable auths remain as broken rows with an editor repair path. Auth files are
  `{id}.jsonc` beside the workspace's `.straumr` file, as request files are.
- The shared list shows the name and, on the second line, what kind of auth it is and the
  one choice within that kind that changes what it does: `OAuth 2.0 · Client Credentials`,
  `Custom · JSON path`, `Bearer`, `Basic`. A row reads amber when the auth holds something
  it could authenticate with right now and inert when it does not, which is the rule every
  populated value in this app follows. Filtering matches the name and that line.
- The detail panel has four regions rather than two, because an auth answers four separate
  questions. **Configuration** is how it is set up — the grant, the token URL, the client,
  the scope, or a custom auth's request and extraction. **Credential** is what it is
  holding and what happens on the next send — status, token type, expiry, refresh token,
  the header it injects, auto-renew. **Secrets** is every `{{secret:name}}` it refers to
  with whether the store can supply it. **Used by** is the requests that send with it.
- Nothing on the screen shows credential material. A value that is a secret reference is
  named, because the name is the point of using one and is not itself the secret; anything
  else reads `set` or `not set`. That a token is not printed on screen needs no line of its
  own, which is the rule the Requests screen's Authentication pane already follows.
- Status is said in colour rather than in a word of its own: amber for held, inert grey for
  empty, red for the reason the next send will fail. Green is not available — it belongs to
  the active workspace alone — so an expired token that will renew itself reads amber.
  The wording and the colour come from `AuthFormatting`, which the Requests screen now uses
  for the same auth, so the two screens cannot describe one resource differently.
- `f` fetches: an OAuth2 token or a custom auth's extracted value, through Core's existing
  behavior. The result is saved, because an auth holds its token — that is the difference
  between fetching here and a send fetching one for itself. It is registered on the list and
  on the container the detail regions share, so it works from wherever the reader is, and it
  is not offered for Bearer or Basic, which *are* the credential they carry. While a fetch is
  in flight the summary bar swaps its identifier badge for the pulse and a ticking duration —
  the same swap the full-screen response makes, one screen down — and `Escape` cancels.
  `:fetch [name]` is the typed form.
- `c` creates, `e` edits and `y` copies through the shared resource editor, and `Enter` opens
  it as `e` does. A copy opens with its source's name cleared. The pages are **Auth**,
  **Grant**, **Headers**, **Params**, **Body** and **Extract**, and the type decides which of
  them exist at all: a Bearer or Basic auth has only Auth, OAuth2 adds Grant, and Custom adds
  the four that describe its request. A page that does not apply shows no title and cannot be
  stepped to, which is the field-level discriminator one level up.
- `h` on the Extract page opens help: what each of the three extraction sources does, how its
  expression is written, and two or three worked examples of each. The page asks for one
  expression whose meaning changes completely with the source chosen above it — a dotted walk, a
  header name, a regular expression — and the field has room for one example of the three in its
  placeholder. `F1` opens the same help and is the key that works everywhere on the page: every
  letter is a character a focused field swallows, so `h` reaches the help from the Source dropdown
  but not from the three text boxes, which is where a reader wondering about the syntax is
  standing. `Ctrl+H` is not the pairing, because a terminal sends it as the C0 byte for Backspace
  and it would delete a character rather than explain one. One hint carries both keys, as
  `Enter e Edit` does on the Requests list. The commands sit on the Extract page's own root, so
  they are offered there and on no other page.
- The help describes `StraumrAuthService`'s three extractors and is written against them: the JSON
  path is a dotted walk with bare numbers indexing arrays and is deliberately not JSONPath, so the
  help says so rather than leaving a reader to try `$.access_token`; the header lookup falls
  through to the content headers and ignores case; and the regex takes the first match, preferring
  group 1 when the pattern has one. Section titles come from `ExtractionSourceDisplayName`, so the
  help and the dropdown cannot come to call one source two things.
- Each configuration shape is kept for the life of the form, so changing the type puts away
  what the old one held rather than throwing it out — the rule a body type already follows.
  The bar shows the type and what the auth points at, live, opposite the unsaved marker.
- `d` deletes the selected auth, asking first through the shared confirm modal. The
  confirmation names how many requests still point at it, because deleting an auth does not
  change them — as deleting one from the CLI does not — and this screen is the only place
  that knows which they are. It is offered for a broken auth too, unlike Copy and Fetch.
- `Ctrl+E` on the list, and `:json [name]`, open the auth's file in the configured editor:
  the advanced route, for a field the form does not offer or an edit easier made as text.
  `e` on an auth that cannot be read opens the same editor. A valid edit saves through Core;
  invalid auth JSON or identity is written back for repair rather than discarded.
- `:select <name>` / `:a <name>` resolves exact names or unique prefixes and clears a filter
  to reach the selection. `:auth` / `:au` reaches the screen from anywhere and can dispatch
  one of its commands after loading it. Every action the screen offers under a key is also
  a command, so another screen can reach it through `:au …`:

  | Command | Key | Does |
  | --- | --- | --- |
  | `:select <name>` / `:a <name>` | — | Selects an auth, clearing a filter to reach it |
  | `:create` / `:new` | `c` | Opens the editor on a new auth |
  | `:edit [name]` | `e`, `Enter` | Opens the editor; an auth that cannot be read opens as JSON |
  | `:copy [name]` | `y` | Opens the editor on a copy, its name cleared |
  | `:delete [name]` | `d` | Asks the delete confirmation, naming what still points at it |
  | `:fetch [name]` | `f` | Fetches and saves the token or extracted value |
  | `:json [name]` | `Ctrl+E` | Opens the auth file in `$EDITOR` |
  | `:refresh` | — | Reloads the screen |

  Each acts on the auth it names and on the selection when it names none, which is what
  the key does. Every one of them answers
  `no active workspace; use :ws use <workspace> to choose one` when there is none, in
  those words on every screen. `:au edit` and `:au copy` opened from another screen return
  to it when the editor closes; `:au delete`, `:au fetch` and `:au json` leave the reader
  on Auths, where what they did is what is now on show.
- Core now refuses a double quote in an auth name, as it already did for a request name, so
  every auth name has an unambiguous quoted command representation.
- The movable dividers load from and save to the `Auths` entry in `StraumrOptions.PaneLayouts`.
  The stack divider opens at 65 rather than the even split the other screens use: Configuration
  and Credential carry up to seven rows each and the two below them are usually two.

## Evidence and resume points

- Debug and Release solution builds and the Release CLI-only build pass with no warnings, and
  CLI `create auth --help` still renders after the shared helpers moved.
- A layout diagnostic (`.tmp/a1-check`) renders the screen over a fixture with one auth of each
  type, two requests pointing at the OAuth2 one, and one resolvable and one missing secret. It
  confirms the four-region geometry, the `┬` and `┼` junctions on both rules, secret references
  reading as `{name}`, and `valid` and `on` at the palette's amber (`#ffc857`). Two caveats
  carried from R1's checkers: the snapshot renderer does not apply focus, so the chip is not in
  the capture, and it renders the tree's natural size rather than the requested viewport, so the
  narrow captures are clipped rather than reflowed — real resizing needs the terminal.
- The shared refactors were proved by snapshot diff against `HEAD`: Requests at 120x28, 80x24,
  70x20 and 160x40 and the one-, two- and three-line lists are byte-identical, and Workspaces
  differs only in the fixture path the two runs used.
- The Extract page's help renders as its own capture (`auth-extract-help`): the three example
  columns line up at 20 cells, the prose wraps inside the dialog rather than running past it, and
  the brackets in the regex examples survive — `TextBlock` is plain text, and markup is a control
  of its own. Beware the `-plain.txt` captures when reading that one: the diagnostic strips markup
  with a naive `[...]` regex, which eats the literal brackets too. The `.txt` beside it is what
  actually renders.
- No input harness was built, by standing request. What needs the developer's terminal, in
  rough order of how likely it is to be wrong: `h` opening the help from the Source dropdown and
  `F1` opening it from inside the Expression box, with one hint reading `h F1 Help`; the four regions at 120x30 and on a short
  terminal, `Tab` reaching all four and the chip landing on each; `f` against a real token
  endpoint, its pulse animating and `Escape` cancelling it; the editor's pages appearing and
  disappearing as the type changes, and `Ctrl+T` skipping the ones that do not apply; the
  `Grant` page for the authorization code grant, whose PKCE toggle reveals a field below it;
  and `d` on an auth with dependents.
- Known and deliberate: the rule closing the upper regions meets the panel divider without a
  junction glyph, because its row is decided by a `Star` split at layout time and
  `StraumrSurfaces.VerticalDivider` takes fixed offsets. Requests has the same gap on its
  Secrets and Response rules. Fixing it means teaching the divider to ask where the rules
  landed, which is a change to a shared piece that both screens would want.
- Not done, and deliberately: `:new` and `:edit <name>` command forms, import/export of a
  single auth, and anything that would change a request when its auth is deleted.
