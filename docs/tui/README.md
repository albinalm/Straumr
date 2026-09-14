# TUI Implementation Guide

The living specification and progress tracker for the Straumr TUI rewrite, split so
a reader can load only the part a task needs. `src/Straumr.Console.Tui/AGENTS.md`
routes a task to the right files; this page is the plain map.

| File | Contents | Read when |
| --- | --- | --- |
| [status.md](./status.md) | Current phase, active screen, milestone table, checkpoint rules | Always, first |
| [scope.md](./scope.md) | Goals, non-goals, sources of truth | Deciding whether something belongs in the TUI |
| [design-contract.md](./design-contract.md) | What the mockups mean: shell, bars, rules, colour, focus cues, footer | Changing anything visible |
| [framework-layout.md](./framework-layout.md) | XenoAtom.Terminal.UI 3.9.0 rules for composition, visuals, palette | Building or restyling visuals |
| [framework-input.md](./framework-input.md) | Gestures, keys, focus, modality, commands, prompt, fullscreen hosting | Touching input, focus or hosting |
| [headless-verification.md](./headless-verification.md) | Snapshots, driving a real `TerminalApp`, when to ask the developer instead | Proving a change without a terminal |
| [code-structure.md](./code-structure.md) | File layout, naming and ownership rules, runtime boundaries | Adding a file or wiring a screen |
| [shared-components.md](./shared-components.md) | `ResourceScreenLayout`, lists, filter, dividers, browsers, prompt | Writing any screen visual |
| [adding-a-screen.md](./adding-a-screen.md) | The six-step procedure, and what Requests added | Starting a new screen |
| [screen-workspaces.md](./screen-workspaces.md) | The accepted Workspaces screen and its definition of done | Changing Workspaces, or as a worked example |
| [screen-requests.md](./screen-requests.md) | R1 behavior, evidence, resume points | Working the active milestone |
| [validation-checklist.md](./validation-checklist.md) | What has been verified and what has not | Closing out a change |
| [decisions.md](./decisions.md) | Dated decisions and their reasons | A rule looks arbitrary, or you want to reverse one |
| [changelog.md](./changelog.md) | Dated history, newest first | Resuming after a break |

## Keeping it current

- A completed or advanced milestone updates [status.md](./status.md) in the same change.
- A new constraint discovered in the framework goes to the matching `framework-*.md`.
- A reversible choice with a reason goes to [decisions.md](./decisions.md); what
  happened goes to [changelog.md](./changelog.md). Do not put history in a rules file.
