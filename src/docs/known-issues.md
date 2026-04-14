## Known Issues

### Terminal key handling diverges by Terminal.Gui driver

Terminal.Gui v2 selects an input driver at startup. The driver, not the terminal or AOT/JIT mode, determines how keys are delivered to the app. Two classes emerge:

- **Win32 console driver** (Windows Terminal). The host translates keys upstream and delivers the typed rune directly in `Key.AsRune`. Layout-shifted characters such as `:` `/` `?` arrive as `KC=58 R=0x3A SK=Null` on a Swedish layout — clean.
- **ANSI / Unix driver with Kitty keyboard protocol parsing** (Kitty on Linux, Alacritty on Linux *and* Windows, and likely WezTerm/Foot). The physical key lands in `KeyCode`/`AsRune` and the typed character lands in `ShiftedKeyCode`. On a Swedish layout, `:` arrives as `KC=Shift+0x2E R=0x2E SK=58`. `AsRune` is the unshifted character. This driver also emits modifier-only events (pressing Shift alone fires a standalone key) because Kitty protocol flag 2 ("report event types") is active.

The original note attributed symptoms to Alacritty specifically. That was misattribution; the real class is "any terminal routed through the ANSI driver". AOT vs. debug is not a factor — published WT-AOT behaves the same as WT-debug.

**Symptoms on the ANSI-driver path**

1. *Shifted symbols missing in text input.* `/` `:` `?` `\` cannot be typed into URL fields and other `TextField`-backed inputs. Terminal.Gui's built-in text handling inserts `Key.AsRune`, which on this driver is the unshifted character. Straumr's own filter/command bar works because it calls `KeyHelpers.GetCharValue`, which prefers `ShiftedKeyCode`.
2. *Tab reported as Ctrl+I.* Alacritty's ANSI input still trips this historical quirk; covered by matching on `KeyCode.Tab` or `(Ctrl, rune 9)`.
3. *Esc stops cancelling prompts after a SendScreen modal flow.* Reproducible on both Kitty-Linux and Alacritty-Win. The key still reaches `IApplication.Keyboard.KeyDown` cleanly (`KC=Esc R=0x1B`), so this is not an input-translation problem. Some focused child view below the Application layer consumes Esc before it reaches the screen handler. A plausible contributor is the driver's modifier-only events slipping into view-level routing during modal transitions, though this has not been proven.

**Solution: two-pillar application-wide fix**

*Pillar A — route screen handlers at the Application layer.* Terminal.Gui raises `IApplication.Keyboard.KeyDown` *before* dispatching to any top-level view or focused child (confirmed by TG docs and by our diagnostic overlay). Subscribing `TuiApp` there instead of `_window.KeyDown` lets screen-level `OnKeyDown` run unconditionally, stepping over the focus bug rather than fighting it. Esc/Tab/Enter become reliable on all drivers regardless of which child holds focus.

*Pillar B — normalize runes in the same prefilter.* When `Key.ShiftedKeyCode != KeyCode.Null` and differs from the physical `KeyCode`, suppress the original and re-raise via `_application.Keyboard.RaiseKeyDownEvent(new Key(ShiftedKeyCode))` under a reentrance guard. Downstream consumers — including Terminal.Gui's built-in `TextField` — then see a driver-normalized key, and URL input accepts `:` `/` `?` on every driver. Modifier-only events are filtered out at the same prefilter since they are noise for routing.

With these two changes, `KeyHelpers.GetCharValue` collapses into call-site-specific helpers (`PhysicalChar` for vim-style bindings, `TypedChar` for text input) and screen-level `key == Key.Esc` / `key == Key.Tab` comparisons keep working because routing is fixed at the source.

**Diagnostic overlay**

`Infrastructure/KeyDiagnostics.cs` subscribes at `IApplication.Keyboard.KeyDown` and displays the last eight keys (`KC | R | SK | BK | Modifiers`) at the bottom of the active window. Toggle with `F12`. Use it to verify driver classification on any new terminal before debugging.
