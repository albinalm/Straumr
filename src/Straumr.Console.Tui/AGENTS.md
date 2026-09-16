# Straumr.Console.Tui — agent guide

The Straumr terminal UI: a retained, reactive `XenoAtom.Terminal.UI` 3.9.0 app over
Core services. The full specification lives in [`docs/tui/`](../../docs/tui/), split
by concern. **Read only the documents your task routes to** — the guide is large and
loading all of it wastes the budget the work needs.

## Always

1. Read [`docs/tui/status.md`](../../docs/tui/status.md) before anything else. It says
   which milestone is active and which are deliberately not started.
2. Read the routed documents below, not the whole guide.
3. Reuse a shared piece before writing a new one; extend it if it nearly fits.
4. Stop at a reviewable checkpoint. Do not begin the next milestone until the current
   one builds and its smallest applicable behavior is verified.
5. Record what you did: milestone status in `status.md`, a reason in `decisions.md`,
   what happened in `changelog.md`, ticks in `validation-checklist.md`.

## Route by task

| Your task | Read |
| --- | --- |
| Resuming after a break; "what's next?" | `status.md`, then `changelog.md` (top entries only) |
| Changing anything visible — bars, rules, colour, focus cues, footer | `design-contract.md`, `framework-layout.md` |
| Building or restyling a visual | `framework-layout.md`, `shared-components.md` |
| A key, gesture, focus, modality, command bar, `:` prompt, fullscreen hosting | `framework-input.md` |
| Adding a new screen | `adding-a-screen.md`, `shared-components.md`, `code-structure.md` |
| Changing the Workspaces screen | `screen-workspaces.md`, `shared-components.md` |
| Changing the Requests screen | `screen-requests.md`, `shared-components.md` |
| Changing the Auths screen (active milestone) | `screen-auths.md`, `shared-components.md` |
| Adding a file, or deciding where code lives | `code-structure.md` |
| Wiring the host, DI, exit codes, screen navigation | `code-structure.md` (Runtime Boundaries) |
| Proving a change without the developer's terminal | `headless-verification.md` |
| Closing out a change | `validation-checklist.md`, `status.md` |
| "Why is this rule here?" / wanting to reverse one | `decisions.md` |
| Deciding whether a feature belongs in the TUI at all | `scope.md` |

## Non-negotiables

These are the rules most often broken; the routed documents give the reasons.

- Do not reimplement layout, scrolling, focus, selection or clipping that a framework
  control or a `Visuals/Shared` component already owns. `RequestList` is prototype
  code, not a pattern.
- No I/O in render, measure, arrange or per-frame update paths. Load on entry; refresh
  only after an operation or an explicit `:refresh`.
- Colours come from `Visuals/Shared/StraumrStyles.cs`, which the current theme fills.
  No ad hoc colours and no hex literals outside `Visuals/Theming/` — HTTP methods are no
  longer an exception, they are a `[methods]` table in the theme. A colour may be a
  terminal default or a palette slot, not only RGB, so never call `Brush.Solid` or
  `ToHexString` on one unchecked.
- A role carries one meaning. Before reusing an existing role for something new, check it
  is the same thing: `accent`, the wordmark and POST shared a colour until a theme wanted
  colourless chrome, and two of the three disappeared.
- `Visual.Invalidate` is obsolete — drive state through `[Bindable]` partial properties.
- Verify a framework API against the pinned 3.9.0 package, not against upstream `main`.
- Ask the developer for a quick terminal check rather than building an input harness
  for something they can see in one keystroke.
- Do not start a screen the active milestone has not reached.

## Project layout

`Integration/` host and DI · `Infrastructure/` shell, navigation, prompt, command table ·
`Screens/<Name>/` one screen and its presentation models · `Visuals/Shared/` shared
visuals and styles · `Formatting/` value formatting. The annotated tree is in
[`docs/tui/code-structure.md`](../../docs/tui/code-structure.md).
