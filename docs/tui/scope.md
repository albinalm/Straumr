# Goals, Non-Goals and Sources of Truth

Part of the [TUI implementation guide](./README.md). Read before deciding whether
something belongs in the TUI at all, or when you need the authoritative reference
for framework behavior.

## Goals

- Implement the approved TUI layouts one screen at a time.
- Start with Workspaces before Requests, Auths, and Secrets.
- Use XenoAtom.Terminal.UI as a retained, reactive UI framework.
- Keep TUI presentation independent from CLI presentation.
- Reuse Core services and models instead of duplicating business logic.
- Preserve the CLI-only build and its smaller dependency footprint.
- Keep code straightforward enough that structure and naming explain behavior.

## Non-Goals

- Do not build an in-terminal JSON editor. Structured editing continues through
  the configured external editor.
- Do not move terminal concerns into Core.
- Do not call CLI commands from the TUI.
- Do not recreate framework controls through manual measurement, rendering,
  scrolling, or focus code when a built-in control supports the behavior.
- Do not implement screens beyond the currently approved milestone.

## Sources of Truth

Use these in this order:

1. The package pinned by Straumr:
   `XenoAtom.Terminal.UI 3.9.0` and its installed XML documentation.
2. The [XenoAtom.Terminal.UI repository](https://github.com/XenoAtom/XenoAtom.Terminal.UI/tree/main/).
3. The [FullscreenDemo](https://github.com/XenoAtom/XenoAtom.Terminal.UI/tree/main/samples/FullscreenDemo)
   for composition, bindings, commands, focus, overlays, and fullscreen hosting.
4. The approved mockups under [`docs/design`](../design/).
5. Existing Straumr CLI and Core behavior.

Upstream `main` can be newer than the pinned NuGet package. Copy architectural
patterns from upstream, but verify concrete APIs against version `3.9.0` before
using them.

