package app

import (
	"os"
	"path/filepath"
	"sort"
	"strings"

	"straumr-tui/internal/views/dialogs"

	tea "github.com/charmbracelet/bubbletea"
)

func (m *Model) openPathFlow(flow pendingFlow, title, message, value string, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending

	currentDir, inputPath := m.resolvePathPickerStart(flow, value)
	m.pathPicker.Open(
		title,
		message,
		dialogs.PathModeSave,
		inputPath,
		currentDir,
		false,
		m.pathPickerQuickLocations(flow, currentDir),
		m.pathPickerEntries(currentDir),
	)
}

func (m *Model) handlePathPickerKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	switch msg.String() {
	case "left", "h":
		m.pathPickerGoParent()
		return m, nil
	case "right", "l":
		m.pathPickerOpenSelection()
		return m, nil
	case "tab":
		m.pathPickerCompleteSelection()
		return m, nil
	}

	if key, ok := dialogKey(msg); ok {
		switch m.pathPicker.HandleKey(key) {
		case dialogs.ActionMove:
			m.pathPickerSyncInputFromSelection()
			return m, nil
		case dialogs.ActionAccept:
			return m.acceptPathPicker()
		case dialogs.ActionCancel:
			m.clearOverlays()
			return m, nil
		case dialogs.ActionBack:
			m.pathPickerGoParent()
			return m, nil
		case dialogs.ActionOpen:
			m.pathPickerOpenSelection()
			return m, nil
		}
	}

	if len(msg.Runes) > 0 {
		m.pathPicker.InsertText(string(msg.Runes))
		m.refreshPathPickerFromInput()
	}
	m.refreshPathPickerFromInput()
	return m, nil
}

func (m *Model) acceptPathPicker() (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	path := strings.TrimSpace(m.pathPicker.InputPath)
	if path == "" {
		if result := m.pathPicker.Result(true); strings.TrimSpace(result.Entry.Path) != "" {
			if result.Entry.Directory {
				m.pathPickerOpenEntry(result.Entry)
				return m, nil
			}
			path = result.Entry.Path
		}
	}

	path = m.resolvePathPickerAbsolute(path)
	if path == "" {
		m.pathPicker.Message = "A file path is required"
		return m, nil
	}

	if info, err := os.Stat(path); err == nil && info.IsDir() {
		m.pathPickerOpenDirectory(path)
		return m, nil
	}

	pending := *m.pending
	m.lastDirs[pending.Flow] = filepath.Dir(path)
	content := pending.OutputText
	successMessage := "Saved file to " + path
	switch pending.Flow {
	case flowSendSavePath:
		successMessage = "Saved response body to " + path
	case flowSendExportPath:
		successMessage = "Exported response to " + path
	}

	m.clearOverlays()
	m.session.Busy = true
	return m, writeFileCmd(path, content, successMessage)
}

func (m *Model) resolvePathPickerStart(flow pendingFlow, value string) (string, string) {
	input := strings.TrimSpace(value)
	if input == "" {
		if last := strings.TrimSpace(m.lastDirs[flow]); last != "" {
			return last, ""
		}
		if cwd, err := os.Getwd(); err == nil {
			return cwd, ""
		}
		home, _ := os.UserHomeDir()
		return home, ""
	}

	absolute := m.resolvePathPickerAbsolute(input)
	if strings.HasSuffix(input, string(os.PathSeparator)) || strings.HasSuffix(input, "/") || strings.HasSuffix(input, "\\") {
		return absolute, absolute
	}

	dir := filepath.Dir(absolute)
	if dir == "." || dir == "" {
		if cwd, err := os.Getwd(); err == nil {
			dir = cwd
		}
	}
	return dir, absolute
}

func (m *Model) pathPickerQuickLocations(flow pendingFlow, currentDir string) []dialogs.PathEntry {
	entries := []dialogs.PathEntry{}
	if cwd, err := os.Getwd(); err == nil && strings.TrimSpace(cwd) != "" {
		entries = append(entries, dialogs.PathEntry{Name: "Working directory", Path: cwd, Directory: true, Quick: true})
	}
	if home, err := os.UserHomeDir(); err == nil && strings.TrimSpace(home) != "" {
		entries = append(entries, dialogs.PathEntry{Name: "Home", Path: home, Directory: true, Quick: true})
	}
	if last := strings.TrimSpace(m.lastDirs[flow]); last != "" && !samePath(last, currentDir) {
		entries = append(entries, dialogs.PathEntry{Name: "Last used", Path: last, Directory: true, Quick: true})
	}
	return entries
}

func (m *Model) pathPickerEntries(currentDir string) []dialogs.PathEntry {
	items, err := os.ReadDir(currentDir)
	if err != nil {
		return nil
	}

	entries := make([]dialogs.PathEntry, 0, len(items))
	for _, item := range items {
		fullPath := filepath.Join(currentDir, item.Name())
		entries = append(entries, dialogs.PathEntry{
			Name:      item.Name(),
			Path:      fullPath,
			Directory: item.IsDir(),
		})
	}

	sort.Slice(entries, func(i, j int) bool {
		if entries[i].Directory != entries[j].Directory {
			return entries[i].Directory
		}
		return strings.ToLower(entries[i].Name) < strings.ToLower(entries[j].Name)
	})
	return entries
}

func (m *Model) refreshPathPickerFromInput() {
	resolved := m.resolvePathPickerAbsolute(m.pathPicker.InputPath)
	targetDir := m.pathPicker.CurrentDir

	switch {
	case resolved == "":
		targetDir = m.pathPicker.CurrentDir
	case strings.HasSuffix(m.pathPicker.InputPath, string(os.PathSeparator)) || strings.HasSuffix(m.pathPicker.InputPath, "/") || strings.HasSuffix(m.pathPicker.InputPath, "\\"):
		targetDir = resolved
	default:
		targetDir = filepath.Dir(resolved)
	}

	if targetDir == "." || strings.TrimSpace(targetDir) == "" {
		targetDir = m.pathPicker.CurrentDir
	}
	if info, err := os.Stat(targetDir); err == nil && info.IsDir() {
		m.pathPicker.CurrentDir = targetDir
		m.pathPicker.SetEntries(m.pathPickerEntries(targetDir))
		m.pathPicker.SetQuickLocations(m.pathPickerQuickLocations(m.pending.Flow, targetDir))
	}
}

func (m *Model) pathPickerGoParent() {
	parent := filepath.Dir(m.pathPicker.CurrentDir)
	if parent == "." || samePath(parent, m.pathPicker.CurrentDir) {
		return
	}
	m.pathPickerOpenDirectory(parent)
}

func (m *Model) pathPickerOpenSelection() {
	result := m.pathPicker.Result(true)
	m.pathPickerOpenEntry(result.Entry)
}

func (m *Model) pathPickerOpenEntry(entry dialogs.PathEntry) {
	if strings.TrimSpace(entry.Path) == "" {
		return
	}
	if entry.Directory {
		m.pathPickerOpenDirectory(entry.Path)
		return
	}
	m.pathPicker.SetInputPath(entry.Path)
}

func (m *Model) pathPickerOpenDirectory(path string) {
	absolute := m.resolvePathPickerAbsolute(path)
	if absolute == "" {
		return
	}
	if info, err := os.Stat(absolute); err != nil || !info.IsDir() {
		return
	}
	m.pathPicker.CurrentDir = absolute
	m.pathPicker.SetInputPath(ensureTrailingSeparator(absolute))
	m.pathPicker.SetEntries(m.pathPickerEntries(absolute))
	m.pathPicker.SetQuickLocations(m.pathPickerQuickLocations(m.pending.Flow, absolute))
}

func (m *Model) pathPickerSyncInputFromSelection() {
	result := m.pathPicker.Result(true)
	if strings.TrimSpace(result.Entry.Path) == "" {
		return
	}
	if result.Entry.Directory {
		m.pathPicker.SetInputPath(ensureTrailingSeparator(result.Entry.Path))
		return
	}
	m.pathPicker.SetInputPath(result.Entry.Path)
}

func (m *Model) pathPickerCompleteSelection() {
	result := m.pathPicker.Result(true)
	if strings.TrimSpace(result.Entry.Path) == "" {
		return
	}
	m.pathPickerSyncInputFromSelection()
	if result.Entry.Directory {
		m.pathPickerOpenDirectory(result.Entry.Path)
	}
}

func (m *Model) resolvePathPickerAbsolute(value string) string {
	trimmed := strings.TrimSpace(value)
	if trimmed == "" {
		return ""
	}
	if strings.HasPrefix(trimmed, "~") {
		if home, err := os.UserHomeDir(); err == nil {
			remainder := strings.TrimPrefix(trimmed, "~")
			remainder = strings.TrimPrefix(remainder, `\`)
			remainder = strings.TrimPrefix(remainder, "/")
			trimmed = filepath.Join(home, remainder)
		}
	}
	if !filepath.IsAbs(trimmed) {
		base := m.pathPicker.CurrentDir
		if strings.TrimSpace(base) == "" {
			if cwd, err := os.Getwd(); err == nil {
				base = cwd
			}
		}
		trimmed = filepath.Join(base, trimmed)
	}
	return filepath.Clean(trimmed)
}

func ensureTrailingSeparator(path string) string {
	if strings.HasSuffix(path, string(os.PathSeparator)) {
		return path
	}
	return path + string(os.PathSeparator)
}

func samePath(left, right string) bool {
	if left == "" || right == "" {
		return false
	}
	return strings.EqualFold(filepath.Clean(left), filepath.Clean(right))
}
