package dialogs

import (
	"strings"

	"github.com/charmbracelet/lipgloss"

	"straumr-tui/internal/ui/theme"
)

type dialogStyles struct {
	panel         lipgloss.Style
	title         lipgloss.Style
	message       lipgloss.Style
	sectionTitle  lipgloss.Style
	field         lipgloss.Style
	fieldFocused  lipgloss.Style
	textBlock     lipgloss.Style
	textBlockLive lipgloss.Style
	row           lipgloss.Style
	rowSelected   lipgloss.Style
	rowMuted      lipgloss.Style
	help          lipgloss.Style
}

func currentDialogStyles() dialogStyles {
	base := theme.CurrentStyles()
	tokens := theme.Active()

	panel := base.Panel.Copy().Padding(1, 2)
	title := base.PanelTitle.Copy().Bold(true)
	message := base.Info.Copy()
	sectionTitle := base.Info.Copy().Bold(true)
	help := base.HelpText.Copy()
	row := lipgloss.NewStyle().Padding(0, 1)
	rowSelected := base.RowSelected.Copy().Padding(0, 1)
	rowMuted := base.Muted.Copy()

	field := lipgloss.NewStyle().
		Border(lipgloss.RoundedBorder()).
		Padding(0, 1)
	if fg := resolveDialogColor(tokens.OnSurface); fg != "" {
		field = field.Foreground(lipgloss.Color(fg)).
			BorderForeground(lipgloss.Color(fg))
	}
	if bg := resolveDialogColor(tokens.Surface); bg != "" {
		field = field.Background(lipgloss.Color(bg))
	}

	fieldFocused := field.Copy()
	if fg := resolveDialogColor(tokens.Primary); fg != "" {
		fieldFocused = fieldFocused.BorderForeground(lipgloss.Color(fg))
	}
	if bg := resolveDialogColor(tokens.SurfaceVariant); bg != "" {
		fieldFocused = fieldFocused.Background(lipgloss.Color(bg))
	}

	textBlock := field.Copy().Padding(0, 1)
	textBlockLive := fieldFocused.Copy().Padding(0, 1)

	return dialogStyles{
		panel:         panel,
		title:         title,
		message:       message,
		sectionTitle:  sectionTitle,
		field:         field,
		fieldFocused:  fieldFocused,
		textBlock:     textBlock,
		textBlockLive: textBlockLive,
		row:           row,
		rowSelected:   rowSelected,
		rowMuted:      rowMuted,
		help:          help,
	}
}

func renderDialogChrome(title, message string, content []string, footer string) string {
	styles := currentDialogStyles()
	parts := make([]string, 0, len(content)+3)
	if strings.TrimSpace(title) != "" {
		parts = append(parts, styles.title.Render(title))
	}
	if strings.TrimSpace(message) != "" {
		parts = append(parts, styles.message.Render(message))
	}
	for _, block := range content {
		if strings.TrimSpace(block) != "" {
			parts = append(parts, block)
		}
	}
	if strings.TrimSpace(footer) != "" {
		parts = append(parts, styles.help.Render(footer))
	}
	return styles.panel.Render(strings.Join(parts, "\n\n"))
}

func renderDialogSection(title string, rows []string) string {
	styles := currentDialogStyles()
	parts := make([]string, 0, len(rows)+1)
	if strings.TrimSpace(title) != "" {
		parts = append(parts, styles.sectionTitle.Render(title))
	}
	for _, row := range rows {
		if row != "" {
			parts = append(parts, row)
		}
	}
	return strings.Join(parts, "\n")
}

func renderDialogField(text string, focused bool) string {
	styles := currentDialogStyles()
	if focused {
		return styles.fieldFocused.Render(text)
	}
	return styles.field.Render(text)
}

func renderDialogTextBlock(text string, focused bool) string {
	styles := currentDialogStyles()
	if focused {
		return styles.textBlockLive.Render(text)
	}
	return styles.textBlock.Render(text)
}

func renderDialogRow(text string, selected bool) string {
	styles := currentDialogStyles()
	if selected {
		return styles.rowSelected.Render(text)
	}
	return styles.row.Render(text)
}

func renderDialogMuted(text string) string {
	return currentDialogStyles().rowMuted.Render(text)
}

func resolveDialogColor(token string) string {
	trimmed := strings.TrimSpace(token)
	if trimmed == "" {
		return ""
	}
	if strings.HasPrefix(trimmed, "#") {
		return trimmed
	}
	if code, ok := dialogAnsi16Codes[strings.ToLower(trimmed)]; ok {
		return code
	}
	return trimmed
}

var dialogAnsi16Codes = map[string]string{
	"black":         "0",
	"red":           "1",
	"green":         "2",
	"yellow":        "3",
	"blue":          "4",
	"magenta":       "5",
	"cyan":          "6",
	"gray":          "7",
	"grey":          "7",
	"darkgray":      "8",
	"darkgrey":      "8",
	"brightred":     "9",
	"brightgreen":   "10",
	"brightyellow":  "11",
	"brightblue":    "12",
	"brightmagenta": "13",
	"brightcyan":    "14",
	"white":         "15",
}
