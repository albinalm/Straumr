package dialogs

import (
	"fmt"
	"path/filepath"
	"strings"
)

type PathMode string

const (
	PathModeOpen PathMode = "open"
	PathModeSave PathMode = "save"
)

type PathEntry struct {
	Name      string
	Path      string
	Directory bool
	Quick     bool
}

type PathFocus string

const (
	PathFocusPath    PathFocus = "path"
	PathFocusQuick   PathFocus = "quick"
	PathFocusEntries PathFocus = "entries"
)

type PathPickerView struct {
	OverlayState
	Title          string
	Message        string
	Help           string
	Mode           PathMode
	InputPath      string
	InputCursor    int
	CurrentDir     string
	Filter         string
	MustExist      bool
	Cursor         int
	QuickCursor    int
	Focus          PathFocus
	QuickLocations []PathEntry
	Entries        []PathEntry
}

func (v *PathPickerView) Open(title, message string, mode PathMode, inputPath, currentDir string, mustExist bool, quickLocations, entries []PathEntry) {
	v.OverlayState.Open()
	v.Title = title
	v.Message = message
	v.Mode = mode
	v.InputPath = inputPath
	v.InputCursor = len([]rune(inputPath))
	v.CurrentDir = currentDir
	v.MustExist = mustExist
	v.Cursor = 0
	v.QuickCursor = 0
	v.Focus = PathFocusPath
	v.QuickLocations = append(v.QuickLocations[:0], quickLocations...)
	v.SetEntries(entries)
}

func (v *PathPickerView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Message = ""
	v.Help = ""
	v.Mode = ""
	v.InputPath = ""
	v.InputCursor = 0
	v.CurrentDir = ""
	v.Filter = ""
	v.MustExist = false
	v.Cursor = 0
	v.QuickCursor = 0
	v.Focus = ""
	v.QuickLocations = nil
	v.Entries = nil
}

func (v *PathPickerView) SetInputPath(value string) {
	v.InputPath = value
	v.InputCursor = len([]rune(value))
}

func (v *PathPickerView) InsertText(text string) {
	if text == "" {
		return
	}
	value, cursor := insertAt(v.InputPath, v.InputCursor, text)
	v.InputPath = value
	v.InputCursor = cursor
}

func (v *PathPickerView) DeleteBackward() bool {
	value, cursor, ok := deleteBeforeCursor(v.InputPath, v.InputCursor)
	if !ok {
		return false
	}
	v.InputPath = value
	v.InputCursor = cursor
	return true
}

func (v *PathPickerView) DeleteForward() bool {
	value, cursor, ok := deleteAtCursor(v.InputPath, v.InputCursor)
	if !ok {
		return false
	}
	v.InputPath = value
	v.InputCursor = cursor
	return true
}

func (v *PathPickerView) MoveInputCursor(offset int) {
	runes := []rune(v.InputPath)
	cursor := v.InputCursor + offset
	if cursor < 0 {
		cursor = 0
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	v.InputCursor = cursor
}

func (v *PathPickerView) SetQuickLocations(items []PathEntry) {
	v.QuickLocations = append(v.QuickLocations[:0], items...)
	if v.QuickCursor >= len(v.QuickLocations) {
		v.QuickCursor = len(v.QuickLocations) - 1
	}
	if v.QuickCursor < 0 {
		v.QuickCursor = 0
	}
}

func (v *PathPickerView) SetEntries(entries []PathEntry) {
	v.Entries = append(v.Entries[:0], entries...)
	if v.Cursor >= len(v.Entries) {
		v.Cursor = len(v.Entries) - 1
	}
	if v.Cursor < 0 {
		v.Cursor = 0
	}
}

func (v *PathPickerView) Result(accepted bool) PathResult {
	result := PathResult{
		Accepted:  accepted,
		Cancelled: !accepted,
		Mode:      v.Mode,
		Path:      v.InputPath,
	}
	if entry, ok := v.selectedBrowsableEntry(); ok {
		result.Entry = entry
		if result.Path == "" {
			result.Path = entry.Path
		}
	}
	if result.Path == "" && result.Entry.Path != "" {
		result.Path = result.Entry.Path
	}
	return result
}

func (v *PathPickerView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyUp:
		v.moveActiveCursor(-1)
		return ActionMove
	case KeyDown:
		v.moveActiveCursor(1)
		return ActionMove
	case KeyLeft:
		if v.Focus == PathFocusPath {
			v.MoveInputCursor(-1)
			return ActionMove
		}
	case KeyRight:
		if v.Focus == PathFocusPath {
			v.MoveInputCursor(1)
			return ActionMove
		}
	case KeyDelete:
		if v.Focus == PathFocusPath {
			if v.DeleteForward() {
				return ActionMove
			}
		}
	case KeyBackspace:
		if v.Focus == PathFocusPath {
			if v.DeleteBackward() {
				return ActionMove
			}
		}
	case KeySearch:
		v.Focus = PathFocusPath
		return ActionMove
	case KeyTab:
		v.cycleFocus(false)
		return ActionMove
	case KeyShiftTab:
		v.cycleFocus(true)
		return ActionMove
	case KeyEnter:
		v.applySelection()
		if entry, ok := v.selectedBrowsableEntry(); ok && entry.Directory {
			return ActionOpen
		}
		return ActionAccept
	case KeyOpen:
		v.applySelection()
		if entry, ok := v.selectedBrowsableEntry(); ok && entry.Directory {
			return ActionOpen
		}
		return ActionAccept
	case KeyCancel:
		return ActionCancel
	case KeyHome:
		return ActionBack
	case KeyEnd:
		return ActionOpen
	default:
		return ActionNone
	}
	return ActionNone
}

func (v *PathPickerView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder

	title := v.Title
	if title == "" {
		title = "Path picker"
	}
	b.WriteString(title)
	b.WriteString("\n")
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}

	if modeLabel := pathModeLabel(v.Mode); modeLabel != "" {
		b.WriteString(fmt.Sprintf("Mode: %s\n", modeLabel))
	}
	if v.CurrentDir != "" {
		b.WriteString(fmt.Sprintf("Current directory: %s\n", v.CurrentDir))
	}
	b.WriteString(fmt.Sprintf("Path: %s\n", renderEditableValue(v.InputPath, "(type a path)", v.Focus == PathFocusPath, v.InputCursor)))
	b.WriteString(fmt.Sprintf("Focus: %s\n", pathFocusLabel(v.Focus)))

	if len(v.QuickLocations) > 0 {
		b.WriteString("Quick locations\n")
		for index, item := range v.QuickLocations {
			b.WriteString(renderPathEntry(item, index == v.QuickCursor, true))
			b.WriteString("\n")
		}
	}

	b.WriteString("Browsable entries\n")
	if len(v.Entries) > 0 {
		for index, item := range v.Entries {
			b.WriteString(renderPathEntry(item, index == v.Cursor, false))
			b.WriteString("\n")
		}
	} else {
		b.WriteString("(empty)\n")
	}

	if v.Filter != "" {
		b.WriteString(fmt.Sprintf("Filter: %s\n", v.Filter))
	}
	if v.MustExist {
		b.WriteString(pathRequirementLabel(v.Mode))
		b.WriteString("\n")
	}
	b.WriteString(pathPickerHelp(v.Help, len(v.QuickLocations) > 0, len(v.Entries) > 0))

	return strings.TrimRight(b.String(), "\n")
}

func renderPathEntry(entry PathEntry, selected, quick bool) string {
	prefix := "  "
	if selected {
		prefix = "> "
	}

	label := entry.Name
	if label == "" {
		label = filepath.Base(entry.Path)
	}
	if label == "" {
		label = entry.Path
	}

	suffix := ""
	if quick || entry.Quick {
		suffix = " [quick]"
	}
	if entry.Directory {
		suffix += " [dir]"
	}

	path := entry.Path
	if path == "" {
		path = entry.Name
	}
	if path != "" && path != label {
		return fmt.Sprintf("%s%s%s - %s", prefix, label, suffix, path)
	}
	return fmt.Sprintf("%s%s%s", prefix, label, suffix)
}

func (v *PathPickerView) cycleFocus(reverse bool) {
	sequence := v.focusSequence()
	if len(sequence) == 0 {
		return
	}

	index := 0
	for i, focus := range sequence {
		if focus == v.Focus {
			index = i
			break
		}
	}
	if reverse {
		index--
	} else {
		index++
	}
	if index < 0 {
		index = len(sequence) - 1
	}
	if index >= len(sequence) {
		index = 0
	}
	v.Focus = sequence[index]
}

func (v *PathPickerView) focusSequence() []PathFocus {
	sequence := []PathFocus{PathFocusPath}
	if len(v.QuickLocations) > 0 {
		sequence = append(sequence, PathFocusQuick)
	}
	if len(v.Entries) > 0 {
		sequence = append(sequence, PathFocusEntries)
	}
	return sequence
}

func (v *PathPickerView) moveActiveCursor(offset int) {
	switch v.Focus {
	case PathFocusQuick:
		v.QuickCursor += offset
		if v.QuickCursor < 0 {
			v.QuickCursor = 0
		}
		if v.QuickCursor >= len(v.QuickLocations) {
			v.QuickCursor = len(v.QuickLocations) - 1
		}
	case PathFocusEntries:
		v.Cursor += offset
		if v.Cursor < 0 {
			v.Cursor = 0
		}
		if v.Cursor >= len(v.Entries) {
			v.Cursor = len(v.Entries) - 1
		}
	default:
		v.MoveInputCursor(offset)
	}
}

func (v *PathPickerView) applySelection() {
	switch v.Focus {
	case PathFocusQuick:
		if entry, ok := v.selectedQuickLocation(); ok {
			v.applyPathEntry(entry)
		}
	case PathFocusEntries:
		if entry, ok := v.selectedEntry(); ok {
			v.applyPathEntry(entry)
		}
	}
}

func (v *PathPickerView) applyPathEntry(entry PathEntry) {
	if entry.Path == "" {
		return
	}
	v.SetInputPath(entry.Path)
	if entry.Directory {
		v.CurrentDir = entry.Path
	}
}

func (v *PathPickerView) selectedQuickLocation() (PathEntry, bool) {
	if v.QuickCursor < 0 || v.QuickCursor >= len(v.QuickLocations) {
		return PathEntry{}, false
	}
	return v.QuickLocations[v.QuickCursor], true
}

func (v *PathPickerView) selectedEntry() (PathEntry, bool) {
	if v.Cursor < 0 || v.Cursor >= len(v.Entries) {
		return PathEntry{}, false
	}
	return v.Entries[v.Cursor], true
}

func (v *PathPickerView) selectedBrowsableEntry() (PathEntry, bool) {
	switch v.Focus {
	case PathFocusQuick:
		return v.selectedQuickLocation()
	case PathFocusEntries:
		return v.selectedEntry()
	default:
		return PathEntry{}, false
	}
}

func pathModeLabel(mode PathMode) string {
	switch mode {
	case PathModeOpen:
		return "browse existing path"
	case PathModeSave:
		return "choose save target"
	default:
		return ""
	}
}

func pathFocusLabel(focus PathFocus) string {
	switch focus {
	case PathFocusQuick:
		return "quick locations"
	case PathFocusEntries:
		return "browsable entries"
	default:
		return "typed path"
	}
}

func pathRequirementLabel(mode PathMode) string {
	switch mode {
	case PathModeSave:
		return "The target path must be valid for saving."
	default:
		return "The selected path must already exist."
	}
}

func pathPickerHelp(extra string, hasQuick, hasEntries bool) string {
	parts := []string{
		"Enter accept",
		"Esc cancel",
		"Tab cycle focus",
		"Shift+Tab reverse focus",
		"/ focus path",
		"j/k move",
		"h/l directory movement",
		"Backspace delete",
		"Del remove forward",
	}
	if hasQuick {
		parts = append(parts, "quick locations stay selectable")
	}
	if hasEntries {
		parts = append(parts, "browse entries below")
	}
	if extra != "" {
		parts = append(parts, extra)
	}
	return "Help: " + strings.Join(parts, " | ")
}

func renderEditableValue(value, placeholder string, focused bool, cursor int) string {
	if value == "" {
		if focused {
			return "| " + placeholder
		}
		return placeholder
	}
	rendered := value
	if !focused {
		return rendered
	}

	runes := []rune(rendered)
	if cursor < 0 {
		cursor = 0
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	left := string(runes[:cursor])
	right := string(runes[cursor:])
	if right == "" {
		return left + "|"
	}
	return left + "|" + right
}

func insertAt(value string, cursor int, text string) (string, int) {
	runes := []rune(value)
	if cursor < 0 {
		cursor = 0
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	insert := []rune(text)
	next := append(append([]rune(nil), runes[:cursor]...), append(insert, runes[cursor:]...)...)
	return string(next), cursor + len(insert)
}

func deleteBeforeCursor(value string, cursor int) (string, int, bool) {
	runes := []rune(value)
	if cursor <= 0 || len(runes) == 0 {
		return value, cursor, false
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	next := append([]rune(nil), runes[:cursor-1]...)
	next = append(next, runes[cursor:]...)
	return string(next), cursor - 1, true
}

func deleteAtCursor(value string, cursor int) (string, int, bool) {
	runes := []rune(value)
	if cursor < 0 || cursor >= len(runes) {
		return value, cursor, false
	}
	next := append([]rune(nil), runes[:cursor]...)
	next = append(next, runes[cursor+1:]...)
	return string(next), cursor, true
}
