package common

import (
	"fmt"
	"strings"
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
	var b strings.Builder

	if state.Title != "" {
		b.WriteString(state.Title)
		b.WriteString("\n")
	}

	if state.Hints != "" {
		b.WriteString(state.Hints)
		b.WriteString("\n")
	}

	if state.Message != "" {
		b.WriteString(state.Message)
		b.WriteString("\n")
	}

	if len(rows) == 0 {
		if state.EmptyText != "" {
			b.WriteString(state.EmptyText)
			b.WriteString("\n")
		}
		return strings.TrimRight(b.String(), "\n")
	}

	for index, row := range rows {
		prefix := " "
		if index == state.Cursor {
			prefix = ">"
		}

		status := rowStatus(row)
		if status != "" {
			status = fmt.Sprintf(" [%s]", status)
		}

		b.WriteString(prefix)
		b.WriteString(" ")
		b.WriteString(row.Title)
		b.WriteString(status)
		b.WriteString("\n")

		if row.Summary != "" {
			b.WriteString("   ")
			b.WriteString(row.Summary)
			b.WriteString("\n")
		}

		for _, detail := range row.Details {
			if detail == "" {
				continue
			}
			b.WriteString("   ")
			b.WriteString(detail)
			b.WriteString("\n")
		}
	}

	return strings.TrimRight(b.String(), "\n")
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
