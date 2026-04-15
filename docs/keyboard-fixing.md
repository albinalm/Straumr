# Keyboard Fixing Guide

This document explains:

- what the keyboard/input problem was
- why it showed up only in some terminals and keyboard layouts
- what Straumr changed to fix it
- what assumptions the current solution makes
- what to do when a new keyboard layout or terminal is still unsupported

This is the long-form engineering document. Use `known-issues.md` as the shorter incident/history log.

## Scope

This document is about keyboard input inside the TUI, especially:

- typed text in `TextField` and `TextView`
- command keys such as `Esc`, `Tab`, `Enter`, arrows, and typed shortcuts
- nested prompt/edit flows where the same key can mean different things depending on the current interaction mode

When this document says "keyboard language", it really means keyboard layout/input behavior. The main issue was not Swedish specifically. Swedish was just the layout that exposed the bug clearly because `:`, `/`, and `?` are layout-shifted in ways that made the driver mismatch visible.

## Executive summary

The core bug was that Terminal.Gui does not deliver keys with identical semantics across all drivers.

On the Win32 console driver, the typed character usually arrives directly in `Key.AsRune` / `KeyCode`.

On the ANSI/Unix driver path, especially when Kitty keyboard protocol parsing is involved, the physical key and the typed character can arrive in different fields:

- the physical key may be in `KeyCode`
- the unshifted rune may be in `AsRune`
- the actual typed symbol may be in `ShiftedKeyCode`

That difference broke Straumr in two separate ways:

1. Text input was wrong because `Terminal.Gui.TextField` inserted the wrong character.
2. Prompt/navigation flows were wrong because app code sometimes assumed one exact `Key` shape.

The fix was not a screen-local patch. It was a layered architecture:

1. semantic key handling in `KeyHelpers`
2. centralized application routing and diagnostics in `KeyPrefilter` / `TuiApp`
3. text normalization in `InteractiveTextField` / `InteractiveTextView`
4. explicit prompt ownership rules for nested editors

## The original failure

The initial repro was:

- Windows Terminal worked
- Alacritty and Kitty exposed failures
- `:` and `/` could open command/filter flows
- but the same characters could not be typed into URL or other text fields

That looked contradictory, but it was real. The reason was that command handling and text insertion were using different code paths:

- Straumr command handling already used `KeyHelpers.GetCharValue`, which could read `ShiftedKeyCode`
- Terminal.Gui text insertion used its own internal path and relied on the unnormalized key

Later, a second failure showed up:

- after certain prompt/modal flows, `Esc`, `Tab`, or `Enter` could stop behaving correctly

That turned out to be a different class of problem. `Esc` was still arriving at the app boundary correctly. The failure was in prompt ownership, focus, or nested edit-state handling, not in raw key decoding.

## Driver model: why Windows Terminal behaved differently

Terminal.Gui v2 selects an input driver at startup. The driver, not just the terminal emulator name, determines the shape of the incoming `Key`.

Two practical classes mattered during debugging:

### 1. Win32 console driver

Observed with Windows Terminal on Windows.

Typical behavior:

- `:` arrives directly as a typed rune
- `/` arrives directly as a typed rune
- `Esc` and `Tab` are simple and predictable

This is the "clean" behavior Straumr was already effectively assuming in several places.

An important verified detail from debugging was that this behavior stayed the same in both debug and AOT publish. The issue was not caused by AOT. It was caused by driver semantics.

### 2. ANSI / Unix driver with Kitty keyboard protocol parsing

Observed with:

- Kitty on Linux
- Alacritty on Windows
- likely similar terminals on the ANSI path such as WezTerm/Foot unless proven otherwise

Typical behavior:

- `ShiftedKeyCode` can contain the actual typed symbol
- `AsRune` can still contain the unshifted physical key
- modifier-only events can also appear

Those modifier-only events are a side-effect of Kitty keyboard protocol flag 2, which reports event types. Straumr drops those at the prefilter boundary because they are noise for normal TUI interaction.

Example on a Swedish layout:

- `:` can arrive with `SK=58` but `R=0x2E`
- `/` can arrive with `SK=47` but `R=0x37`

That means "what key was physically pressed" and "what character the user intended to type" are not the same thing.

## Observed key matrix

The debugging was based on concrete captures from the same repro path, not just qualitative descriptions.

### Windows Terminal

Observed on Windows in both debug and AOT publish:

- `/` -> `KC=47 R=0x2F SK=Null BK=Null M=-`
- `:` -> `KC=58 R=0x3A SK=Null BK=Null M=-`
- `?` -> `KC=63 R=0x3F SK=Null BK=Null M=-`
- `Esc` -> `KC=Esc R=0x1B SK=Null BK=Null M=-`
- `Tab` -> `KC=Tab R=0x9 SK=Null BK=Null M=-`
- `Shift+Tab` -> `KC=Tab, ShiftMask R=0x9 SK=Null BK=Null M=S`

### Kitty / Alacritty on the ANSI-driver path

Observed on Kitty Linux AOT and Alacritty Windows AOT:

- `/` -> `KC=D7, ShiftMask R=0x37 SK=47 BK=Null M=S`
- `:` -> `KC=268435502 R=0x2E SK=58 BK=Null M=S`
- `?` -> `KC=268435499 R=0x2B SK=63 BK=Null M=S`
- `Esc` -> `KC=Esc R=0x1B SK=Null BK=Null M=-`
- `Tab` -> `KC=Tab R=0x9 SK=Null BK=Null M=-`
- `Shift+Tab` -> `KC=Tab, ShiftMask R=0x9 SK=Null BK=Null M=S`

The important conclusion from this matrix was:

- punctuation differed by driver shape
- `Esc`, `Tab`, and `Shift+Tab` already arrived consistently, so later `Esc` failures were not raw key-decoding failures

## Evidence from the original repro

The most useful evidence was not just the raw matrix. It was the combination of the matrix with the observed behavior in the app.

### What still worked during the failing repro

- `/` opened the filter and filter typing worked
- `:` opened the command bar and command typing worked
- `j`, `k`, `g`, `G`, and other general navigation worked
- `Esc` closed the filter

That mattered because it proved the keyboard stack was not globally broken. Some interaction paths were healthy while others were not.

### Where it broke

The original high-value repro looked like this:

1. Create a request through the CLI/TUI interactive console.
2. Try to type a URL in the request editor.
3. `:` and `/` do not insert, so a valid URL cannot be entered.
4. Send the request anyway.
5. The request is invalid, which is expected given the broken URL input.
6. On the summary screen press `s` to test saving the body.
7. The app correctly reports that there is no body.
8. After that, `Esc` and `Tab` stop behaving correctly.
9. Pressing `s` again can reopen the same error.
10. Eventually even `Enter` can stop dismissing the flow correctly.

This behavior was observed on Kitty Linux and Alacritty Windows, while Windows Terminal continued to behave normally.

### Why this repro was so important

This evidence forced three important conclusions:

1. `:` and `/` failing in text input but still working for command/filter proved that command handling and text insertion were separate pipelines.
2. The invalid-request -> `s` -> "no body" -> stuck-input flow proved there was also a modal/prompt ownership bug, not just a text-input bug.
3. `Esc` still being reported as `KC=Esc R=0x1B SK=Null BK=Null M=-` even when it appeared "broken" proved that the later `Esc` failure was above raw decoding.

If future debugging produces the same pattern, classify it immediately as a multi-layer issue rather than trying to solve it inside one screen.

## Verified symptoms

The investigation established these behaviors:

### Worked even before the global fix

- `/` could open the filter
- `:` could open the command bar
- `j`, `k`, `g`, `G` navigation worked
- `Esc` could close the filter

### Failed before the global fix

- `:` `/` `?` could fail inside text fields
- some prompt flows could trap or mishandle `Esc`
- tab/navigation behavior could drift if fixed in the wrong layer
- `Tab` on the ANSI path can historically overlap with the same logical shape as `Ctrl+I`, so matching has to be semantic rather than naive

### Important conclusion

This was never one bug.

It was at least three layers of bugs:

1. command semantics
2. text insertion semantics
3. modal/prompt ownership semantics

Treating those as one issue leads to bad fixes.

## The architecture Straumr now uses

The design target is:

Make Straumr see the same logical keyboard model on every terminal, even when the raw `Key` object differs by driver.

This is implemented in four layers.

## Layer 1: semantic key handling

Primary file:

- `src/Straumr.Console.Tui/Helpers/KeyHelpers.cs`

This layer defines what a key means logically, independent of the raw driver shape.

Important helpers include:

- `GetCharValue(Key key)`
- `NormalizeForTextInput(Key key)`
- `IsEscape(Key key)`
- `IsEnter(Key key)`
- `IsTabForward(Key key)`
- `IsTabBackward(Key key)`
- `IsTabNavigation(Key key)`
- `IsCursorUp(Key key)`
- `IsCursorDown(Key key)`

One specific case this layer covers is the historical `Tab`/`Ctrl+I` ambiguity on the ANSI path. The app should treat that as a logical tab/navigation question, not as a raw exact-key comparison.

### Why this exists

App code should not depend on exact `Key` equality for unstable keys. If a terminal delivers the same user intent with a different raw encoding, Straumr should still behave the same way.

### Rule

If a screen or component is interpreting keyboard meaning, it should use `KeyHelpers`.

Do not scatter raw `key == Key.X` checks through new code unless the key is known to be stable and layout-independent.

## Layer 2: application routing and diagnostics

Primary files:

- `src/Straumr.Console.Tui/TuiApp.cs`
- `src/Straumr.Console.Tui/Infrastructure/KeyPrefilter.cs`
- `src/Straumr.Console.Tui/Infrastructure/KeyDiagnostics.cs`

`KeyPrefilter` is the app-level keyboard funnel.

It:

- subscribes to `IApplication.Keyboard.KeyDown`
- maintains a stack of named handlers for screens/prompts
- lets the current top-level screen decide whether it handled a key
- records diagnostics
- drops modifier-only noise

It does not do rune normalization. Text normalization belongs in the text-wrapper layer, not in the application prefilter.

### Why this exists

When a key misbehaves, the first question is not "what should this screen do?" The first question is:

- did the key arrive at the application boundary?
- which handler owned it?
- which view had focus?
- did the current top-level handler swallow it?

Without that, it is too easy to patch the wrong layer.

### Diagnostic overlay

The F12 overlay is now a permanent tool and should be kept.

It records lines like:

```text
KC=<code> R=0x<hex> SK=<code> BK=<code> M=<CAS|-> | S=<stack depth> N=<handler> T=<top view> F=<focused view> H=<Y|N>
EVT <message> | S=<stack depth> N=<handler> T=<top view> F=<focused view>
```

Use it before attempting any local workaround.

Important field meanings:

- `S` = current handler stack depth
- `N` = current topmost handler name
- `T` = current `TopRunnableView`
- `F` = current most-focused view
- `H` = whether the top-level handler returned `true`

These are important because the same raw key can fail for different reasons:

- it never arrived
- it arrived but the wrong handler owned it
- it arrived and the correct handler declined it
- it arrived and a focused child view swallowed it

## Layer 3: text input normalization

Primary files:

- `src/Straumr.Console.Tui/Components/TextFields/InteractiveTextField.cs`
- `src/Straumr.Console.Tui/Components/TextFields/InteractiveTextView.cs`

This is the layer that fixes typed symbols such as `:` `/` `?` in editable controls.

### The key discovery

The correct fix point was not `OnKeyDown`.

Decompiling Terminal.Gui showed:

- `View.NewKeyDownEvent` carries the same original `Key` through multiple stages
- `TextField` inserts printable characters in `OnKeyDownNotHandled`

That means normalizing the key in `OnKeyDown` was too early and did not affect the actual insertion path.

### The implemented fix

Straumr now normalizes the key in:

- `InteractiveTextField.OnKeyDownNotHandled`
- `InteractiveTextView.OnKeyDownNotHandled`

That is where the ANSI-driver symbol mismatch is corrected before Terminal.Gui inserts text.

### Rule

All editable text controls in Straumr should use `InteractiveTextField` or `InteractiveTextView`.

Do not use raw `Terminal.Gui.TextField` or `TextView` for new editable UI unless you are deliberately re-solving the same normalization problem.

## Layer 4: prompt ownership and nested edit states

Primary files:

- `src/Straumr.Console.Tui/Screens/Prompts/PromptScreen.cs`
- `src/Straumr.Console.Tui/Screens/Prompts/FormPromptScreen.cs`
- `src/Straumr.Console.Tui/Screens/Prompts/KeyValueEditorScreen.cs`
- `src/Straumr.Console.Tui/Components/Prompts/Form/FormFieldsView.cs`
- `src/Straumr.Console.Tui/Components/Prompts/Form/FormPrompt.cs`
- `src/Straumr.Console.Tui/Components/Prompts/KeyValue/KeyValueEditorComponent.cs`

This layer exists because `Esc` does not always mean the same thing.

Examples:

- inside a text field, `Esc` may mean "leave edit mode"
- inside a nested item editor, `Esc` may mean "leave the nested editor"
- at the prompt level, `Esc` may mean "cancel the whole prompt"

If parent prompts cancel too eagerly, child editors never get a chance to use `Esc`.

### Implemented fix

`PromptScreen` now allows prompt-level `Esc` cancellation to be suppressed by derived screens through `ShouldCancelOnEscape(...)`.

This is used so:

- `FormPromptScreen` defers prompt-level `Esc` while a field is actively editing
- `KeyValueEditorScreen` defers prompt-level `Esc` for the whole nested item-edit session

There was also an important subtlety:

- `InteractiveTextField` swallows unbound keys while not editing

Because of that, some child forms also needed explicit non-edit-mode `Esc` bindings. Otherwise the key never reached the container.

### Rule

Whenever you build a prompt with internal modes, decide explicitly who owns `Esc` in each mode.

Do not assume parent prompt cancel is always correct.

## Current behavior expectations

The fixes above also establish a deliberate interaction model for forms and nested editors.

### Form editing workflow

Expected behavior:

- opening a form starts with the first field in edit mode
- pressing `Enter` while editing leaves the current field and starts editing the next field
- repeated `Enter` enables a fast workflow through all fields and then to `Save`
- pressing `Esc` while editing leaves edit mode but keeps focus on the field
- after that, `j/k` or arrow keys move focus between fields without entering edit mode
- pressing `Enter` on a focused field re-enters edit mode

This split between focus and edit mode is intentional and should be preserved.

## What is already solved automatically for new code

New code gets the current fix automatically if it stays inside the abstractions.

### Usually automatic

- command handling that uses `KeyHelpers`
- text entry that uses `InteractiveTextField` / `InteractiveTextView`
- prompt routing that goes through `PromptScreen`, `TuiApp`, and `KeyPrefilter`

### Not automatic

- raw `TextField` / `TextView`
- raw exact-key comparisons for unstable keys
- new nested editors with their own internal modes but no explicit `Esc` ownership model

In short:

The keyboard fix is global for code that uses the global layers. It is not magic for code that bypasses them.

## What is verified and what is not

### Verified

The solution has been verified against the failing class of environments that originally exposed the bug:

- Windows Terminal on Windows
- Alacritty on Windows
- Kitty on Linux

The important proven point is not "these exact apps forever". It is that Straumr now handles both major observed driver shapes:

- Win32-style direct typed rune input
- ANSI-driver input where the typed symbol lives in `ShiftedKeyCode`

### Not guaranteed

This is not yet proof of universal support for every keyboard layout and every input system.

Not fully proven:

- dead-key layouts
- heavy AltGr layouts
- compose-key workflows
- IME/input method editor flows
- terminals/drivers that encode text input differently from the tested set

The architecture is prepared for those cases, but each class must still be verified.

## How to investigate an unsupported language or layout

If a new layout fails, do not patch the screen first.

Use this workflow.

### Step 1: capture the environment

Record:

- OS
- terminal emulator
- whether this is debug or publish/AOT
- keyboard layout name
- exact keys that fail
- where they fail

Also record whether the same key works in:

- Windows Terminal, if available
- command/filter bars
- plain text input
- prompt navigation

This quickly tells you whether the bug is layout-wide or only one interaction path.

### Step 2: turn on the F12 overlay

Capture the exact diagnostic lines for the failing key.

For each key press, save:

- `KC`
- `R`
- `SK`
- `BK`
- `M`
- `S`
- `N`
- `F`
- `H`

If prompts are involved, also save nearby `EVT` lines.

### Step 3: classify the failure

Put the bug in exactly one of these buckets first.

#### A. Command semantics problem

Examples:

- `Esc` not cancelling the correct level
- `Tab` not switching panes
- `/` or `:` not opening expected UI

Likely fix layer:

- `KeyHelpers`
- prompt/screen ownership rules
- `KeyPrefilter` diagnostics and routing

#### B. Text insertion problem

Examples:

- symbol opens a command but cannot be typed into a field
- layout-specific punctuation inserts the wrong character
- text fields behave differently from command shortcuts

Likely fix layer:

- `InteractiveTextField`
- `InteractiveTextView`
- `KeyHelpers.NormalizeForTextInput`

#### C. Modal ownership or focus problem

Examples:

- key arrives correctly in diagnostics but nothing happens
- key works until a modal/prompt transition, then stops
- one `Esc` should leave edit mode but closes the entire prompt instead

Likely fix layer:

- `PromptScreen`
- child prompt/component edit-state logic
- `TuiApp` / `KeyPrefilter`

### Step 4: compare raw key shape across terminals

If possible, compare the same key in:

- a working terminal
- a failing terminal

The high-value comparison is often:

- does the typed symbol live in `AsRune` or `ShiftedKeyCode`?
- does the key arrive at all?
- does the top-level handler swallow it?

This can tell you whether the bug is decoding, semantics, or ownership.

### Step 5: fix the correct layer only

Apply the smallest architectural fix in the correct layer.

Examples:

- if command meaning is unstable, extend `KeyHelpers`
- if typed characters are wrong, fix the text wrapper layer
- if nested `Esc` flows are wrong, fix prompt ownership

Do not patch a random screen just because that is where the bug was first noticed.

### Step 6: retest across the matrix

After a fix, test:

- the failing layout/terminal
- Windows Terminal as the reference behavior
- at least one text input path
- at least one command path
- at least one nested prompt/edit flow

This is important because a local-looking fix can easily break a different interaction layer.

## What information to collect when reporting a new unsupported layout

When opening a new issue or continuing debugging, collect:

1. terminal emulator and OS
2. keyboard layout name
3. exact key or keys pressed
4. where the failure happens
5. whether Windows Terminal behaves differently
6. the F12 diagnostic lines for those keys
7. whether the issue is text insertion, command handling, or prompt ownership

Good report example:

```text
Terminal: WezTerm on Linux
Layout: German
Failing keys: AltGr+Q (@), AltGr+7 (|)
Works in filter/command bar: yes
Fails in TextField: yes
F12: KC=... R=... SK=... BK=... M=... | S=... N=... F=... H=...
Classification: text insertion semantics
```

## Anti-patterns and false leads

Avoid these.

### Do not patch one screen unless diagnostics prove the bug is screen-local

This already caused a bad SendScreen attempt that regressed real behavior and made `Tab` stop working entirely on the real ANSI repro.

### Do not rely on raw `key == Key.X` for unstable keys

That can work on one driver and fail on another.

### Do not use raw `TextField` for editable text

That bypasses the normalization layer.

### Do not assume `Esc` always belongs to the top-level prompt

Nested editors often own it first.

### Do not re-introduce application-level re-raise hacks

The attempted `RaiseKeyDownEvent` approach did not reliably descend into the view tree and is not the right fix direction.

### Do not try to mutate `Key.KeyCode` in place

That is not a viable or clean solution.

## Developer checklist for new TUI code

Before adding a new keyboard-driven component:

1. If it accepts text, use `InteractiveTextField` or `InteractiveTextView`.
2. If it interprets key meaning, use `KeyHelpers`.
3. If it has nested modes, define who owns `Esc`, `Enter`, and `Tab`.
4. If it opens prompts, ensure the prompt participates in the normal routing stack.
5. If behavior is surprising, use the F12 overlay before changing code.

## Recommended test matrix for future changes

For any keyboard-related change, test at least:

1. Windows Terminal
2. one ANSI-driver terminal
3. one text-input flow
4. one command shortcut flow
5. one nested prompt/editor flow

If the change targets layout behavior, also test at least:

1. the originally failing layout
2. one simpler layout such as US English
3. one layout with more punctuation or AltGr pressure if available

## Bottom line

Straumr now has a real keyboard architecture instead of a set of local fixes.

The important ideas are:

- command semantics live in `KeyHelpers`
- text normalization lives in `InteractiveTextField` / `InteractiveTextView`
- routing and diagnostics live in `KeyPrefilter` / `TuiApp`
- nested mode ownership must be explicit

That means future keyboard/layout bugs should be easier to diagnose, easier to classify, and much less likely to require per-screen hacks.
