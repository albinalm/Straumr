package common

import (
	"strings"

	"straumr-tui/internal/ui/theme"
)

// Row is a presentation-only item used by the workspace and request views.
// The app shell is expected to map domain DTOs into these rows and keep the
// domain model out of this package.
type Row struct {
	Key     string
	Title   string
	Summary string
	Details []string
	Current bool
	Damaged bool
	Missing bool
}

// ListState stores the local screen state needed by the owned view layer.
// The Bubble Tea root model can translate its own messages and key events into
// these fields without this package knowing about tea.Msg or the app shell.
type ListState struct {
	Title     string
	EmptyText string
	Hints     string
	Message   string
	Cursor    int
	Offset    int
	Width     int
	Height    int
}

type ListView struct {
	State ListState
	Rows  []Row
}

func (v *ListView) SetRows(rows []Row) {
	selectedKey := ""
	if current, ok := v.Current(); ok {
		selectedKey = current.Key
	}

	v.Rows = append(v.Rows[:0], rows...)
	if selectedKey != "" {
		v.SelectKey(selectedKey)
	}
	v.ClampCursor()
}

func (v *ListView) ClampCursor() {
	if len(v.Rows) == 0 {
		v.State.Cursor = 0
		v.State.Offset = 0
		return
	}

	if v.State.Cursor < 0 {
		v.State.Cursor = 0
	}

	if v.State.Cursor >= len(v.Rows) {
		v.State.Cursor = len(v.Rows) - 1
	}

	if v.State.Offset < 0 {
		v.State.Offset = 0
	}

	if v.State.Offset > v.State.Cursor {
		v.State.Offset = v.State.Cursor
	}
}

func (v *ListView) MoveCursor(delta int) {
	if len(v.Rows) == 0 || delta == 0 {
		return
	}

	v.State.Cursor += delta
	v.ClampCursor()
}

func (v *ListView) Home() {
	if len(v.Rows) == 0 {
		return
	}

	v.State.Cursor = 0
	v.State.Offset = 0
}

func (v *ListView) End() {
	if len(v.Rows) == 0 {
		return
	}

	v.State.Cursor = len(v.Rows) - 1
}

func (v *ListView) Current() (Row, bool) {
	if len(v.Rows) == 0 || v.State.Cursor < 0 || v.State.Cursor >= len(v.Rows) {
		return Row{}, false
	}

	return v.Rows[v.State.Cursor], true
}

func (v *ListView) SelectKey(key string) bool {
	if key == "" {
		return false
	}

	for index, row := range v.Rows {
		if row.Key == key {
			v.State.Cursor = index
			return true
		}
	}

	return false
}

func Render(state ListState, rows []Row) string {
	styles := theme.CurrentStyles()
	sections := make([]string, 0, 3)

	if state.Hints != "" {
		sections = append(sections, styles.HelpText.Render(state.Hints))
	}
	if state.Message != "" {
		sections = append(sections, styles.Message.Render(state.Message))
	}

	content := state.EmptyText
	if len(rows) > 0 {
		rendered := make([]string, 0, len(rows))
		for index, row := range rows {
			rendered = append(rendered, renderRow(row, index == state.Cursor, state.Width))
		}
		content = strings.Join(rendered, "\n")
	}

	panelTitle := styles.PanelTitle.Render(state.Title)
	panelBody := styles.Panel.Render(content)
	sections = append(sections, panelTitle+"\n"+panelBody)

	return strings.TrimRight(strings.Join(sections, "\n\n"), "\n")
}

func rowStatus(row Row) string {
	switch {
	case row.Damaged:
		return "damaged"
	case row.Missing:
		return "missing"
	case row.Current:
		return "current"
	default:
		return ""
	}
}

func renderRow(row Row, selected bool, width int) string {
	styles := theme.CurrentStyles()
	lines := make([]string, 0, 2+len(row.Details))

	title := renderRowTitle(row)
	status := rowStatus(row)
	if status != "" {
		title += " " + styles.Muted.Render("["+status+"]")
	}
	lines = append(lines, title)

	if row.Summary != "" {
		lines = append(lines, "  "+styles.RowSummary.Render(row.Summary))
	}

	for _, detail := range row.Details {
		if detail == "" {
			continue
		}
		lines = append(lines, "  "+styles.RowDetail.Render(detail))
	}

	block := strings.Join(lines, "\n")
	if selected {
		style := styles.RowSelected.PaddingLeft(1).PaddingRight(1)
		if width > 6 {
			style = style.Width(width - 6)
		}
		return style.Render(block)
	}

	return " " + block
}

func renderRowTitle(row Row) string {
	styles := theme.CurrentStyles()
	title := styles.RowTitle.Render(row.Title)
	fields := strings.Fields(row.Title)
	if len(fields) > 0 {
		method := fields[0]
		if rendered := theme.MethodStyle(method).Render(method); rendered != method {
			title = rendered + strings.TrimPrefix(row.Title, method)
		}
	}

	if row.Current {
		title = styles.RowCurrent.Render(title)
	}

	return title
}
