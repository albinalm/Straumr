package ui

import (
	"fmt"
	"strings"

	"straumr-tui/internal/state"
	"straumr-tui/internal/ui/theme"

	"github.com/charmbracelet/lipgloss"
)

func RenderShell(session state.Session, body string) string {
	styles := theme.CurrentStyles()
	sections := make([]string, 0, 4)
	header := renderHeader(session, styles)
	if header != "" {
		sections = append(sections, header)
	}

	if session.Message != "" {
		sections = append(sections, styles.Message.Render(session.Message))
	}

	if session.Error != "" {
		sections = append(sections, styles.Error.Render(session.Error))
	}

	body = strings.TrimRight(body, "\n")
	combined := body
	if len(sections) > 0 {
		combined = strings.Join(append(sections, body), "\n")
	}

	return styles.Shell.Render(overlayTopRight(combined, renderBanner(styles), shellWidth(session, combined)))
}

func renderTabs(active state.ScreenID, styles theme.Styles) string {
	screens := []state.ScreenID{
		state.ScreenWorkspaces,
		state.ScreenRequests,
		state.ScreenAuths,
		state.ScreenSecrets,
		state.ScreenSend,
	}

	labels := make([]string, 0, len(screens))
	for _, screen := range screens {
		label := strings.ToUpper(string(screen))
		if screen == active {
			labels = append(labels, styles.TabActive.Render(label))
			continue
		}
		labels = append(labels, styles.TabInactive.Render(label))
	}

	return lipgloss.JoinHorizontal(lipgloss.Top, labels...)
}

func renderHeader(session state.Session, styles theme.Styles) string {
	left := renderTabs(session.Screen, styles)

	contextParts := make([]string, 0, 3)
	if session.ActiveWorkspace != nil && strings.TrimSpace(session.ActiveWorkspace.Name) != "" {
		contextParts = append(contextParts, fmt.Sprintf("workspace: %s", session.ActiveWorkspace.Name))
	}
	if session.ActiveRequest != nil && strings.TrimSpace(session.ActiveRequest.Name) != "" {
		contextParts = append(contextParts, fmt.Sprintf("request: %s", session.ActiveRequest.Name))
	}
	if session.Busy {
		contextParts = append(contextParts, "busy")
	}

	if len(contextParts) == 0 {
		return styles.Header.Render(left)
	}

	right := styles.Context.Render(strings.Join(contextParts, "  •  "))
	return styles.Header.Render(lipgloss.JoinHorizontal(lipgloss.Top, left, "   ", right))
}

func renderBanner(styles theme.Styles) string {
	const banner = `
                              _
                          ___| |_ _ __ __ _ _   _ _ __ ___  _ __
                         / __| __| '__/ _` + "`" + ` | | | | '_ ` + "`" + ` _ \| '__|
                         \__ \ |_| | | (_| | |_| | | | | | | |
                         |___/\__|_|  \__,_|\__,_|_| |_| |_|_|
`
	return styles.Banner.Render(strings.TrimRight(banner, "\n"))
}

func shellWidth(session state.Session, body string) int {
	width := session.Width
	if width <= 0 {
		width = 120
	}
	if bodyWidth := lipgloss.Width(body) + 4; bodyWidth > width {
		width = bodyWidth
	}
	return width
}

func overlayTopRight(base, overlay string, width int) string {
	baseLines := strings.Split(strings.TrimRight(base, "\n"), "\n")
	overlayLines := strings.Split(strings.TrimRight(overlay, "\n"), "\n")
	if len(baseLines) == 1 && baseLines[0] == "" {
		baseLines = nil
	}
	if len(overlayLines) == 1 && overlayLines[0] == "" {
		return base
	}

	if len(baseLines) < len(overlayLines) {
		padding := make([]string, len(overlayLines)-len(baseLines))
		baseLines = append(baseLines, padding...)
	}

	overlayWidth := lipgloss.Width(overlay)
	start := width - overlayWidth
	if start < 2 {
		start = 2
	}

	for i := 0; i < len(overlayLines); i++ {
		line := baseLines[i]
		lineWidth := lipgloss.Width(line)
		if lineWidth >= start {
			baseLines[i] = line + "  " + overlayLines[i]
			continue
		}
		baseLines[i] = line + strings.Repeat(" ", start-lineWidth) + overlayLines[i]
	}

	return strings.TrimRight(strings.Join(baseLines, "\n"), "\n")
}
