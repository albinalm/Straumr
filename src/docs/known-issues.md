## Known Issues

### Terminal key handling diverges by Terminal.Gui driver

Terminal.Gui v2 selects an input driver at startup. The driver, not the terminal or AOT/JIT mode, determines how keys are delivered to the app. Two classes emerge:

- **Win32 console driver** (Windows Terminal). The host translates keys upstream and delivers the typed rune directly in `Key.AsRune`. Layout-shifted characters such as `:` `/` `?` arrive as `KC=58 R=0x3A SK=Null` on a Swedish layout — clean.
- **ANSI / Unix driver with Kitty keyboard protocol parsing** (Kitty on Linux, Alacritty on Linux *and* Windows, and likely WezTerm/Foot). The physical key lands in `KeyCode`/`AsRune` and the typed character lands in `ShiftedKeyCode`. On a Swedish layout, `:` arrives as `KC=Shift+0x2E R=0x2E SK=58`. `AsRune` is the unshifted character. This driver also emits modifier-only events (pressing Shift alone fires a standalone key) because Kitty protocol flag 2 ("report event types") is active.

The original note attributed symptoms to Alacritty specifically. That was misattribution; the real class is "any terminal routed through the ANSI driver". AOT vs. debug is not a factor — published WT-AOT behaves the same as WT-debug.

**Symptoms on the ANSI-driver path**

1. *Shifted symbols missing in text input.* `/` `:` `?` `\` cannot be typed into URL fields and other `TextField`-backed inputs. Terminal.Gui's built-in text handling inserts `Key.AsRune`, which on this driver is the unshifted character. Straumr's own filter/command bar works because it calls `KeyHelpers.GetCharValue`, which prefers `ShiftedKeyCode`.
2. *Tab reported as Ctrl+I.* Alacritty's ANSI input still trips this historical quirk; covered by matching on `KeyCode.Tab` or `(Ctrl, rune 9)`.
3. *Esc stops cancelling prompts after a SendScreen modal flow.* The key still reaches `IApplication.Keyboard.KeyDown` cleanly (`KC=Esc R=0x1B`), so this is not an input-translation problem. A targeted SendScreen pane-focus workaround was attempted and then reverted because it disabled `Tab` entirely on the real ANSI-driver repro, which confirms this should be treated as an infrastructure/input-routing problem rather than a pane-local fix.

### Observed key matrix

Concrete captures from the same repro path now make the driver split explicit:

- **Windows Terminal (Windows, debug and AOT publish)** delivers typed punctuation directly in `KeyCode`/`AsRune`:
  - `/` -> `KC=47 R=0x2F SK=Null BK=Null M=-`
  - `:` -> `KC=58 R=0x3A SK=Null BK=Null M=-`
  - `?` -> `KC=63 R=0x3F SK=Null BK=Null M=-`
- **Kitty (Linux, AOT publish)** and **Alacritty (Windows, AOT publish)** both deliver the physical key in `KeyCode`/`AsRune` and the typed symbol in `ShiftedKeyCode`:
  - `/` -> `KC=D7, ShiftMask R=0x37 SK=47 BK=Null M=S`
  - `:` -> `KC=268435502 R=0x2E SK=58 BK=Null M=S`
  - `?` -> `KC=268435499 R=0x2B SK=63 BK=Null M=S`
- `Esc`, `Tab`, `Shift+Tab`, `j`, and `k` are reported identically across all tested terminals:
  - `Esc` -> `KC=Esc R=0x1B SK=Null BK=Null M=-`
  - `Tab` -> `KC=Tab R=0x9 SK=Null BK=Null M=-`
  - `Shift+Tab` -> `KC=Tab, ShiftMask R=0x9 SK=Null BK=Null M=S`

This matrix confirms:

- The punctuation failure is not terminal-specific in the narrow sense and not AOT-specific. It is the ANSI-driver path across emulators/OSes.
- The post-SendScreen `Esc` failure is not explained by malformed `Esc` payloads. The key still arrives at the application boundary with the same shape as on working hosts, so the remaining problem is above raw input decoding: handler stack, prompt lifecycle, focus routing, or a Terminal.Gui modal/session interaction.

### Repro behavior summary

What still works on the ANSI-driver repro:

- `/` opens the filter and typed filter text works.
- `:` opens the command bar and command typing works.
- `j`, `k`, `g`, `G`, and similar non-text navigation work.
- `Esc` closes the filter.

Where it breaks:

1. In request editing, typing a valid URL in `TextField`-backed inputs fails because `:` and `/` are not inserted.
2. Sending the resulting invalid request shows the expected invalid summary.
3. Pressing `s` on SendScreen triggers the expected "no body" message.
4. After that modal flow, `Esc`, `Tab`, and then eventually even `Enter` stop behaving correctly. Pressing `s` again can still reopen the same error, which implies the screen-level key path is not completely dead, but modal dismissal/input ownership is wrong.

### Current implementation state (in progress)

### Architectural direction

The clean design target is: make Straumr see the same **logical** keyboard model on every terminal that Windows Terminal already gives us.

That likely cannot be solved by one ad hoc hook because the app currently has two distinct consumers of key input:

- **Command/navigation handling** in screens and prompts.
- **Text insertion** inside `Terminal.Gui` `TextField`/`TextView`.

The intended architecture is therefore:

1. **One semantic-key layer at the application boundary.**
   - Centralize terminal-agnostic key interpretation in `KeyHelpers` / `KeyPrefilter`.
   - Screens and prompts should ask questions like `IsEscape`, `IsTabForward`, `IsTabBackward`, `IsEnter`, and `GetCharValue` instead of relying on raw `key == Key.X` comparisons wherever the key may vary by driver.
2. **One text-input wrapper layer.**
   - All editable text controls in Straumr should flow through `InteractiveTextField` / `InteractiveTextView`.
   - ANSI-driver rune normalization belongs there, because the actual character insertion happens inside `Terminal.Gui` internals rather than the screen handlers.
3. **One modal/session ownership layer.**
   - Prompt stack and focus restoration should remain centralized in `TuiApp` / `KeyPrefilter`, not in individual screens.
   - The `Esc`-stuck SendScreen path should be debugged as a handler-stack / modal-lifecycle problem unless diagnostics prove otherwise.

In other words: to make Kitty/Alacritty behave like WT, normalize **commands** at the application boundary, normalize **text entry** at the text-wrapper boundary, and debug **modal ownership** at the prompt/session boundary.

**Pillar A — application-layer routing — works.**

`TuiApp` subscribes a `KeyPrefilter` to `IApplication.Keyboard.KeyDown` and maintains a `Stack<Func<Key, bool>>` of screen handlers, pushed/popped on `LoadScreen` and `RunPrompt`. The F12 diagnostic overlay confirms keys reach the prefilter before any focused view, and the screen handler is invoked with correct stack depth. On Alacritty, typing `:` in the URL field shows `S=2 H=N` in the overlay — meaning stack is healthy and the screen handler correctly declines to swallow it. Modifier-only events (standalone Shift/Ctrl/Alt from Kitty protocol flag 2) are dropped at entry.

The Esc-stuck SendScreen repro has not been re-tested against this routing yet.

**Semantic-key layer — partial rollout.**

`KeyHelpers` now centralizes logical key interpretation for unstable cross-driver cases:

- `GetCharValue` for typed characters (`ShiftedKeyCode` first, then `AsRune`)
- `IsEscape`
- `IsEnter`
- `IsTabForward`
- `IsTabBackward`
- `IsTabNavigation`
- `IsCursorUp` / `IsCursorDown`

The shared prompt/model/file-save layers now use those helpers instead of raw `key == Key.X` checks for their cross-terminal-sensitive bindings. This is intended to make command/navigation behavior converge on WT semantics without having to patch each screen separately.

**Pillar B — rune normalization — partial progress.**

*Attempt 1: re-raise at Application level.* `KeyPrefilter` detected `ShiftedKeyCode != Null`, set `key.Handled = true` on the original, and called `_application.Keyboard.RaiseKeyDownEvent(new Key(ShiftedKeyCode))` under a reentrance guard. XML docs for `IKeyboard.RaiseKeyDownEvent` state it "raises the KeyDown event, then calls NewKeyDownEvent on all top-level views". In practice on Alacritty, the re-raised key did not reach the focused `TextField`. Suspected cause: in beta.92, `RaiseKeyDownEvent` called from a user-code `KeyDown` handler only re-fires the event and does not descend into the view tree. Unconfirmed.

*Attempt 2: in-place mutation.* Probed `Key` via reflection — `KeyCode` is init-only (setter exists but `CanWrite` reporting was misleading), so direct assignment fails at compile time with CS8852. `AsRune` has no setter at all. `ShiftedKeyCode` is writable but irrelevant since TG's `TextField` reads `AsRune`.

*Attempt 3 (failed): normalize at `OnKeyDown` in the subclass.* This did **not** work. Decompiling `Terminal.Gui.ViewBase.View.NewKeyDownEvent` and `Terminal.Gui.Views.TextField` showed why:
- `View.NewKeyDownEvent(Key key)` carries the **same original `Key` instance** through `OnKeyDown`, key bindings, and then `OnKeyDownNotHandled`.
- `TextField` performs printable-character insertion in `OnKeyDownNotHandled`, not via a `Command.Insert` binding table.
- Therefore calling `base.OnKeyDown(KeyHelpers.NormalizeForTextInput(key))` from `InteractiveTextField.OnKeyDown` does **not** affect the later key object that reaches `TextField.OnKeyDownNotHandled`; insertion still sees the unnormalized ANSI-driver key.

*Attempt 4 (current): normalize at `OnKeyDownNotHandled` in the subclass.* The subclasses now:
- keep `InteractiveTextField.OnKeyDown` only for Straumr bindings and edit-mode gating;
- override `InteractiveTextField.OnKeyDownNotHandled` to call `base.OnKeyDownNotHandled(KeyHelpers.NormalizeForTextInput(key))`;
- override `InteractiveTextView.OnKeyDownNotHandled` similarly.

This is the first approach that matches Terminal.Gui's actual insertion path. It is **not yet runtime-retested on Alacritty**, so the fix is code-complete but unverified.

### Next steps

- Retest on an ANSI-driver terminal (Alacritty/Kitty path) with Swedish layout. The specific regression to verify is `:` `/` `?` `\` in `TextField`/`TextView`.
- If insertion still fails after the `OnKeyDownNotHandled` move, instrument `KeyHelpers.NormalizeForTextInput` and/or subclass overrides to confirm the normalized `KeyCode` reaching `TextField.OnKeyDownNotHandled`.
- Fallback only if the retest still fails: bypass TG's text insertion entirely inside `InteractiveTextField.OnKeyDownNotHandled` — when the key is a printable rune and has no non-shift modifiers, mutate the text directly and return `true`. Risk: breaks IME, paste, composition, undo stack.
- The Esc-stuck SendScreen focus bug has *not* been verified against the new Pillar A routing. It may already be fixed and we would not know. Re-test after `:` is working.

### Resume context for the next session

**Files touched (uncommitted, no rollback needed):**
- `Straumr.Console.Tui/TuiApp.cs` — owns `KeyPrefilter` + `KeyDiagnostics`. `LoadScreen` and `RunPrompt` now `Push`/`Pop` screen handlers on the prefilter instead of subscribing `Window.KeyDown`. `RunPrompt` uses `try/finally` for pop.
- `Straumr.Console.Tui/Infrastructure/KeyPrefilter.cs` — subscribes `IApplication.Keyboard.KeyDown`, drops modifier-only events, invokes the named top-of-stack handler, emits diagnostic lines with handler name plus top/focused view snapshots, and records push/pop lifecycle events. **Does no rune normalization.**
- `Straumr.Console.Tui/Infrastructure/KeyDiagnostics.cs` — passive sink: `Toggle()`, `Record(string)`, `AttachTo(View)`. No event subscription of its own. F12 toggle lives in `KeyPrefilter.OnKeyDown`. Overlay capacity increased so key lines and prompt lifecycle events can be seen together.
- `Straumr.Console.Tui/Helpers/KeyHelpers.cs` — existing `GetCharValue` kept; added semantic key predicates (`IsEscape`, `IsEnter`, `IsTabForward`, `IsTabBackward`, `IsTabNavigation`, `IsCursorUp`, `IsCursorDown`) plus `NormalizeForTextInput(Key)` returning `new Key(ShiftedKeyCode)` when `SK != Null && (int)SK != AsRune.Value`.
- `Straumr.Console.Tui/Components/TextFields/InteractiveTextField.cs` — `OnKeyDown` handles Straumr bindings/edit-mode only; `OnKeyDownNotHandled` now normalizes via `KeyHelpers.NormalizeForTextInput` before delegating to `TextField`.
- `Straumr.Console.Tui/Components/TextFields/InteractiveTextView.cs` — `OnKeyDownNotHandled` now normalizes via `KeyHelpers.NormalizeForTextInput` before delegating to `TextView`.

**Reproduction baseline (Alacritty Win AOT, Swedish layout):**
- `/` `:` `?` in URL field: `S=2 H=N` in overlay, no character inserted. Screen handler declines correctly, normalization happens (verified by code path), but no glyph appears.
- Filter (`/`) and command bar (`:`) still work — they use `KeyHelpers.GetCharValue` directly and don't rely on TG insertion.
- SendScreen → invalid request → `s` → "no body" error → stuck Esc: **not retested** against Pillar A routing. May now be fixed.
- SendScreen pane focus drift after `Tab`: a targeted pane/focus patch was tried and reverted. It made `Tab` stop working entirely on the real repro, so this is not the right layer for the fix.

**Concrete next experiments, in order:**
1. Retest the new `OnKeyDownNotHandled` normalization path on Alacritty/Kitty with Swedish layout. Expected result: `:` `/` `?` `\` insert into URL fields and other `TextField`/`TextView` inputs.
2. If it still fails, add temporary diagnostics at the subclass `OnKeyDownNotHandled` boundary to log the pre/post-normalized `KeyCode` reaching TG insertion.
3. Retest Pillar A independently: on Alacritty, run the SendScreen → "no body" → Esc flow and capture the Esc line from the overlay. Expected: `KC=Esc S=N H=Y` (handled by prompt). If `H=N`, the `ShowMessage` modal is opening a TG dialog that isn't going through `RunPrompt`, so the handler stack is stale during the error popup — need to push/pop around `ShowMessage` too. See `SendScreen.cs:~536` (`ShowMessage("Save response body", ...)`).
4. Only if step 1 still fails after confirming normalized keys reach `OnKeyDownNotHandled`: bypass TG insertion directly in the Straumr subclass as a last resort.

### False lead to avoid

- Do not pursue SendScreen-only focus or pane-tab workarounds as the main fix direction. A direct attempt to lock focus to the two text panes and reinterpret `Tab`/`Esc` locally regressed the real ANSI-driver repro by making `Tab` do nothing. The underlying issue remains cross-emulator input semantics at the Terminal.Gui/application-routing layer.

**Do NOT:**
- Re-introduce `RaiseKeyDownEvent`-based re-raise in `KeyPrefilter`. Verified in this session that it fires the event but does not descend into the view tree in beta.92.
- Try to mutate `Key.KeyCode` in place. It is init-only; CS8852 at compile time.
- Touch `Window.KeyDown` subscriptions. All screen routing must go through `KeyPrefilter.Push/Pop`.

**Build check:** still blocked in this Codex sandbox. Local `dotnet build` attempts currently terminate after restore/startup with no C# compiler diagnostics surfaced; this appears to be an environment/tooling issue, not a proven source error in the TUI project.

### Diagnostic overlay

`Infrastructure/KeyDiagnostics.cs` receives formatted lines from `KeyPrefilter` and displays the last twelve entries at the bottom of the active window. Toggle with `F12`. Two line formats now exist:

```
KC=<code> R=0x<hex> SK=<code> BK=<code> M=<CAS|-> | S=<stack depth> N=<handler> T=<top view> F=<focused view> H=<Y|N> [modOnly]
EVT <message> | S=<stack depth> N=<handler> T=<top view> F=<focused view>
```

`S` is the handler stack depth when the key was processed. `N` is the current topmost handler name (for example `SendScreen` or `MessagePromptScreen`). `T` is `IApplication.TopRunnableView`, and `F` is `TopRunnableView.MostFocused`. `H` is whether the topmost screen handler returned `true` (swallowed it). `[modOnly]` marks events dropped as modifier-only. `EVT` lines record handler-stack and modal lifecycle transitions such as `PUSH`, `POP`, `RUN ... begin/end`, and `DISPOSE`. This overlay is now the primary diagnostic for prompt-stack and focus-routing failures and should be retained.
