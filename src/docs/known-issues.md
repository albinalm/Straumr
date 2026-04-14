## Known Issues

### Terminal key handling in Alacritty

Alacritty on Windows/Linux does not emit the Kitty keyboard protocol metadata and reports Tab as `Ctrl+I` plus other layout-dependent quirks. Straumr now intercepts Tab inside the Send screen panes, but there is still a global focus bug: after visiting the Send screen and toggling panes with Tab, returning to any prompt (e.g. editing a request) causes Esc to stop cancelling the prompt. The Esc key is still delivered to Terminal.Gui, yet some internal view retains focus and consumes it.

**Reproduction**

1. Open Straumr inside Alacritty.
2. Enter a prompt (e.g. edit a request) and press Esc — it closes as expected.
3. Navigate to the Send screen, press Tab a few times to switch panes, then hit Esc to return to the list.
4. Re-open a prompt: Esc no longer cancels, requiring `Ctrl+C` or closing the terminal.

**Status**

- Tab switching in the Send screen has a targeted workaround (VirtualTextView raises a navigation event).
- Esc suppression after returning from SendScreen is unresolved and only repros in Alacritty.
- Shifted symbol input (e.g. `:`, `/`, `\`, `?`) remains broken because Alacritty sends US-layout characters with no modifiers, so Terminal.Gui cannot recover the intended rune.

**Workarounds**

1. Use a terminal that implements the Kitty keyboard protocol (Windows Terminal, WezTerm, Foot, recent GNOME Terminal, etc.).
2. If Alacritty must be used, avoid the Send screen before finishing prompt edits, or restart Straumr after sending.
3. Apply targeted patches to the affected views (e.g., bubble Esc from the problematic control) knowing that each area may need bespoke fixes.
