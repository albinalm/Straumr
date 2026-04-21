package common

import (
	"regexp"
	"strings"

	"github.com/charmbracelet/lipgloss"

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
	Footer    string
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
		sections = append(sections, renderMessage(state.Message))
	}

	contentLines := []string{styles.Muted.Render(state.EmptyText)}
	if len(rows) > 0 {
		contentLines = make([]string, 0, len(rows))
		for index, row := range rows {
			contentLines = append(contentLines, renderRow(row, index == state.Cursor, state.Width))
		}
	}
	contentLines = append(contentLines, padContent(state, len(contentLines))...)

	body := strings.Join(contentLines, "\n")
	if footer := renderFooter(state.Footer, state.Width); footer != "" {
		if body != "" {
			body += "\n\n"
		}
		body += footer
	}

	panelBody := styles.Panel.Render(body)
	sections = append(sections, renderPanelTitle(state.Title)+"\n"+panelBody)

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
	titlePrefix := rowMarker(row, selected)
	status := rowStatus(row)
	if status != "" {
		title += " " + styles.Muted.Render("("+status+")")
	}
	lines = append(lines, titlePrefix+" "+title)

	if row.Summary != "" {
		lines = append(lines, "  "+renderSummary(row.Summary))
	}

	for _, detail := range row.Details {
		if detail == "" {
			continue
		}
		lines = append(lines, "  "+renderDetail(detail))
	}

	block := strings.Join(lines, "\n")
	if selected {
		style := styles.RowSelected.PaddingLeft(1).PaddingRight(2)
		if width > 8 {
			style = style.Width(width - 8)
		}
		return style.Render(block)
	}

	return styles.RowDetail.Render(" ") + block
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

func renderMessage(message string) string {
	styles := theme.CurrentStyles()
	lower := strings.ToLower(strings.TrimSpace(message))
	switch {
	case strings.Contains(lower, "error"), strings.Contains(lower, "failed"), strings.Contains(lower, "cannot"), strings.Contains(lower, "no "):
		return styles.Error.Render(message)
	case strings.Contains(lower, "created"), strings.Contains(lower, "updated"), strings.Contains(lower, "deleted"), strings.Contains(lower, "ready"), strings.Contains(lower, "saved"):
		return styles.Success.Render(message)
	default:
		return styles.Message.Render(message)
	}
}

func renderPanelTitle(title string) string {
	styles := theme.CurrentStyles()
	if strings.TrimSpace(title) == "" {
		return ""
	}
	return styles.PanelTitle.Render("┤" + title + "├")
}

func renderFooter(footer string, width int) string {
	styles := theme.CurrentStyles()
	trimmed := strings.TrimSpace(footer)
	if trimmed == "" {
		return ""
	}
	if width <= 0 {
		return styles.Footer.Render(trimmed)
	}
	return lipgloss.PlaceHorizontal(width-6, lipgloss.Right, styles.Footer.Render(trimmed))
}

func rowMarker(row Row, selected bool) string {
	styles := theme.CurrentStyles()
	switch {
	case selected:
		return styles.Success.Render("▸")
	case row.Current:
		return styles.Info.Render("◇")
	default:
		return styles.Muted.Render(" ")
	}
}

func renderSummary(summary string) string {
	styles := theme.CurrentStyles()
	if strings.TrimSpace(summary) == "" {
		return styles.RowSummary.Render("(none)")
	}
	return stylizeInline(summary, styles.RowSummary, styles.Info)
}

func renderDetail(detail string) string {
	styles := theme.CurrentStyles()
	if strings.TrimSpace(detail) == "" {
		return ""
	}
	return stylizeInline(detail, styles.RowDetail, styles.Info)
}

func stylizeInline(text string, base, accent lipgloss.Style) string {
	segments := strings.Split(text, " • ")
	rendered := make([]string, 0, len(segments))
	for _, segment := range segments {
		trimmed := strings.TrimSpace(segment)
		if trimmed == "" {
			continue
		}

		if match := colonMatcher.FindStringSubmatch(trimmed); len(match) == 3 {
			rendered = append(rendered, base.Render(match[1])+" "+styleDates(match[2], base, accent))
			continue
		}

		rendered = append(rendered, styleDates(trimmed, base, accent))
	}

	return strings.Join(rendered, base.Render(" • "))
}

func styleDates(text string, base, accent lipgloss.Style) string {
	indexes := dateMatcher.FindAllStringIndex(text, -1)
	if len(indexes) == 0 {
		return base.Render(text)
	}

	var b strings.Builder
	start := 0
	for _, idx := range indexes {
		if idx[0] > start {
			b.WriteString(base.Render(text[start:idx[0]]))
		}
		b.WriteString(accent.Render(text[idx[0]:idx[1]]))
		start = idx[1]
	}
	if start < len(text) {
		b.WriteString(base.Render(text[start:]))
	}
	return b.String()
}

func padContent(state ListState, usedLines int) []string {
	if state.Height <= 0 {
		return nil
	}

	available := state.Height - 10
	if available <= usedLines+2 {
		return nil
	}

	padding := available - usedLines - 2
	if padding < 0 {
		return nil
	}

	lines := make([]string, padding)
	for i := range lines {
		lines[i] = ""
	}
	return lines
}

var (
	dateMatcher  = regexp.MustCompile(`\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2}:\d{2})?`)
	colonMatcher = regexp.MustCompile(`^([^:]+:)\s*(.*)$`)
)
