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

type PathPickerView struct {
	OverlayState
	Title          string
	Message        string
	Mode           PathMode
	InputPath      string
	CurrentDir     string
	Filter         string
	MustExist      bool
	Cursor         int
	QuickLocations []PathEntry
	Entries        []PathEntry
}

func (v *PathPickerView) Open(title, message string, mode PathMode, inputPath, currentDir string, mustExist bool, quickLocations, entries []PathEntry) {
	v.OverlayState.Open()
	v.Title = title
	v.Message = message
	v.Mode = mode
	v.InputPath = inputPath
	v.CurrentDir = currentDir
	v.MustExist = mustExist
	v.Cursor = 0
	v.QuickLocations = append(v.QuickLocations[:0], quickLocations...)
	v.SetEntries(entries)
}

func (v *PathPickerView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Message = ""
	v.Mode = ""
	v.InputPath = ""
	v.CurrentDir = ""
	v.Filter = ""
	v.MustExist = false
	v.Cursor = 0
	v.QuickLocations = nil
	v.Entries = nil
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
	if v.Cursor >= 0 && v.Cursor < len(v.Entries) {
		result.Entry = v.Entries[v.Cursor]
	}
	if result.Path == "" && result.Entry.Path != "" {
		result.Path = result.Entry.Path
	}
	return result
}

func (v *PathPickerView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyUp:
		if v.Cursor > 0 {
			v.Cursor--
		}
		return ActionMove
	case KeyDown:
		if v.Cursor < len(v.Entries)-1 {
			v.Cursor++
		}
		return ActionMove
	case KeyEnter, KeyOpen:
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
	if v.Mode != "" {
		b.WriteString(fmt.Sprintf("Mode: %s\n", v.Mode))
	}
	if v.CurrentDir != "" {
		b.WriteString(fmt.Sprintf("Current: %s\n", v.CurrentDir))
	}
	b.WriteString(fmt.Sprintf("Path: %s\n", displayOrPlaceholder(v.InputPath, "(type a path)")))
	if len(v.QuickLocations) > 0 {
		b.WriteString("Quick locations\n")
		for _, item := range v.QuickLocations {
			b.WriteString(renderPathEntry(item, false))
			b.WriteString("\n")
		}
	}
	if len(v.Entries) > 0 {
		b.WriteString("Entries\n")
		for index, item := range v.Entries {
			b.WriteString(renderPathEntry(item, index == v.Cursor))
			b.WriteString("\n")
		}
	} else {
		b.WriteString("(empty)\n")
	}
	if v.MustExist {
		b.WriteString("Target must exist")
	}

	return strings.TrimRight(b.String(), "\n")
}

func renderPathEntry(entry PathEntry, selected bool) string {
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
	if entry.Quick {
		suffix = " [quick]"
	}
	if entry.Directory {
		suffix += " [dir]"
	}

	return fmt.Sprintf("%s%s%s", prefix, label, suffix)
}
