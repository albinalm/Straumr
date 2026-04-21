package theme

import (
	"strings"

	"github.com/charmbracelet/lipgloss"
)

type Styles struct {
	Shell       lipgloss.Style
	Header      lipgloss.Style
	HelpText    lipgloss.Style
	Banner      lipgloss.Style
	Panel       lipgloss.Style
	PanelTitle  lipgloss.Style
	Tab         lipgloss.Style
	TabActive   lipgloss.Style
	TabInactive lipgloss.Style
	Context     lipgloss.Style
	Overlay     lipgloss.Style
	Message     lipgloss.Style
	Error       lipgloss.Style
	Muted       lipgloss.Style
	Info        lipgloss.Style
	Success     lipgloss.Style
	Warning     lipgloss.Style
	Danger      lipgloss.Style
	RowTitle    lipgloss.Style
	RowSummary  lipgloss.Style
	RowDetail   lipgloss.Style
	RowSelected lipgloss.Style
	RowCurrent  lipgloss.Style
	Footer      lipgloss.Style
}

func BuildStyles(t Theme) Styles {
	shell := lipgloss.NewStyle()
	if style, ok := withBackground(shell, t.Surface); ok {
		shell = style
	}
	if style, ok := withForeground(shell, t.OnSurface); ok {
		shell = style
	}

	panel := lipgloss.NewStyle().
		Border(lipgloss.RoundedBorder()).
		Padding(1, 1)
	if border := resolveColorToken(t.OnSurface); border != "" {
		panel = panel.BorderForeground(lipgloss.Color(border))
	}
	if style, ok := withBackground(panel, t.Surface); ok {
		panel = style
	}

	selected := lipgloss.NewStyle()
	if style, ok := withBackground(selected, t.SurfaceVariant); ok {
		selected = style
	}
	if style, ok := withForeground(selected, t.Primary); ok {
		selected = style
	}
	selected = selected.Bold(true)

	header := lipgloss.NewStyle().Padding(0, 1)
	if style, ok := withForeground(header, t.Secondary); ok {
		header = style
	}

	tab := lipgloss.NewStyle().Padding(0, 1)
	if style, ok := withForeground(tab, t.OnSurface); ok {
		tab = style
	}

	tabActive := tab.Bold(true)
	if style, ok := withForeground(tabActive, t.OnPrimary); ok {
		tabActive = style
	}
	if style, ok := withBackground(tabActive, t.Primary); ok {
		tabActive = style
	}

	tabInactive := tab
	if style, ok := withForeground(tabInactive, t.Secondary); ok {
		tabInactive = style
	}

	overlay := panel.Copy()
	if border := resolveColorToken(t.Primary); border != "" {
		overlay = overlay.BorderForeground(lipgloss.Color(border))
	}

	return Styles{
		Shell:       shell,
		Header:      header,
		HelpText:    foreground(t.OnSurface),
		Banner:      foreground(t.Primary),
		Panel:       panel,
		PanelTitle:  foreground(t.OnSurface).Bold(true),
		Tab:         tab,
		TabActive:   tabActive,
		TabInactive: tabInactive,
		Context:     foreground(t.Secondary),
		Overlay:     overlay,
		Message:     foreground(t.Info),
		Error:       foreground(t.Danger).Bold(true),
		Muted:       foreground(t.Secondary),
		Info:        foreground(t.Info),
		Success:     foreground(t.Success),
		Warning:     foreground(t.Warning),
		Danger:      foreground(t.Danger),
		RowTitle:    foreground(t.OnSurface),
		RowSummary:  foreground(t.Secondary),
		RowDetail:   foreground(t.Secondary),
		RowSelected: selected,
		RowCurrent:  foreground(t.Primary),
		Footer:      foreground(t.OnSurface),
	}
}

func MethodStyle(method string) lipgloss.Style {
	t := Active()
	switch strings.ToUpper(strings.TrimSpace(method)) {
	case "GET":
		return foreground(t.MethodGet)
	case "POST":
		return foreground(t.MethodPost)
	case "PUT":
		return foreground(t.MethodPut)
	case "PATCH":
		return foreground(t.MethodPatch)
	case "DELETE":
		return foreground(t.MethodDelete)
	case "HEAD":
		return foreground(t.MethodHead)
	case "OPTIONS":
		return foreground(t.MethodOptions)
	case "TRACE":
		return foreground(t.MethodTrace)
	case "CONNECT":
		return foreground(t.MethodConnect)
	default:
		return foreground(t.OnSurface)
	}
}

func foreground(token string) lipgloss.Style {
	style := lipgloss.NewStyle()
	if next, ok := withForeground(style, token); ok {
		return next
	}
	return style
}

func withForeground(style lipgloss.Style, token string) (lipgloss.Style, bool) {
	value := resolveColorToken(token)
	if value == "" {
		return style, false
	}
	return style.Foreground(lipgloss.Color(value)), true
}

func withBackground(style lipgloss.Style, token string) (lipgloss.Style, bool) {
	value := resolveColorToken(token)
	if value == "" {
		return style, false
	}
	return style.Background(lipgloss.Color(value)), true
}

func resolveColorToken(token string) string {
	trimmed := strings.TrimSpace(token)
	if trimmed == "" {
		return ""
	}
	if strings.HasPrefix(trimmed, "#") {
		return trimmed
	}
	if code, ok := ansi16Codes[strings.ToLower(trimmed)]; ok {
		return code
	}
	return trimmed
}

var ansi16Codes = map[string]string{
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
