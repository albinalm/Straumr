package ui

import (
	"fmt"
	"strings"

	"straumr-tui/internal/state"
)

func RenderShell(session state.Session, body string) string {
	var b strings.Builder

	b.WriteString(renderTabs(session.Screen))
	b.WriteString("\n")

	if session.ActiveWorkspace != nil && session.ActiveWorkspace.Name != "" {
		b.WriteString(fmt.Sprintf("Workspace: %s", session.ActiveWorkspace.Name))
		if session.Busy {
			b.WriteString(" [loading]")
		}
		b.WriteString("\n")
	}

	if session.Message != "" {
		b.WriteString(session.Message)
		b.WriteString("\n")
	}

	if session.Error != "" {
		b.WriteString(session.Error)
		b.WriteString("\n")
	}

	b.WriteString(body)
	return strings.TrimRight(b.String(), "\n")
}

func renderTabs(active state.ScreenID) string {
	screens := []state.ScreenID{
		state.ScreenWorkspaces,
		state.ScreenRequests,
		state.ScreenAuths,
		state.ScreenSecrets,
		state.ScreenSend,
	}

	labels := make([]string, 0, len(screens))
	for _, screen := range screens {
		label := string(screen)
		if screen == active {
			label = "[" + label + "]"
		}
		labels = append(labels, label)
	}

	return strings.Join(labels, "  ")
}
