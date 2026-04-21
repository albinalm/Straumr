# Dialogs and Pickers Module

## Purpose

Define the reusable overlays and field editors used across all screens, with specific focus on replacing the current weak file-dialog UX.

## Responsibilities

- Provide reusable overlays for:
  - selection lists
  - confirmations
  - text input
  - secret input
  - key/value editing
  - structured forms
  - file/path selection
  - save/export targets
- Keep VIM-style navigation and command consistency across overlays.
- Support modal layering without losing parent-screen context.
- Follow the shared visual system so overlays feel like Straumr prompts, not generic popup boxes.

## Inputs

- Parent-screen requests to open dialogs
- Current values and validation rules
- Optional CLI-backed list data for selectors

## Outputs

- Confirm/cancel results
- Edited form values
- Selected filesystem paths

## CLI interaction

- None directly.
- Dialogs emit structured values back to parent modules, which then call `cli-client`.

## Boundaries

- Owns overlay UX and validation feedback.
- Does not call the CLI itself.
- Does not contain entity-specific persistence logic.
- Must consume semantic styles from `visual-system.md` rather than defining ad hoc colors or borders per dialog.

## File/path picker redesign

Replace the current file-system prompt with a safer, clearer picker:

- Primary mode is typed path entry plus browsable list, not a mini file manager.
- Show quick locations:
  - current working directory
  - active workspace directory when relevant
  - home directory
  - last-used directory for the same action
- Separate “save target” and “browse existing file” behaviors.
- Do not support destructive delete inside save/open dialogs.
- Keep:
  - `/` filter
  - `j/k` navigation
  - `h/l` directory movement
  - tab completion for path entry

## References

- Visual system: `docs/tui/visual-system.md`, `docs/themes.md`
- Existing prompt host: `src/Straumr.Console.Tui/Console/TuiInteractiveConsole.cs`
- Current file picker: `src/Straumr.Console.Tui/Components/Prompts/FileSave/FileSystemPromptBase.cs`
- Current prompt set: `src/Straumr.Console.Tui/Screens/Prompts/`
