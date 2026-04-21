package dialogs

import (
	"strconv"
	"strings"
)

const textViewerWindowSize = 18

type TextViewerView struct {
	OverlayState
	Title  string
	Help   string
	Body   string
	lines  []string
	scroll int
}

func (v *TextViewerView) Open(title, help, body string) {
	v.OverlayState.Open()
	v.Title = title
	v.Help = help
	v.Body = body
	v.lines = strings.Split(body, "\n")
	if len(v.lines) == 0 {
		v.lines = []string{""}
	}
	v.scroll = 0
}

func (v *TextViewerView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Help = ""
	v.Body = ""
	v.lines = nil
	v.scroll = 0
}

func (v *TextViewerView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyUp:
		if v.scroll > 0 {
			v.scroll--
		}
		return ActionMove
	case KeyDown:
		if v.scroll < v.maxScroll() {
			v.scroll++
		}
		return ActionMove
	case KeyHome:
		v.scroll = 0
		return ActionMove
	case KeyEnd:
		v.scroll = v.maxScroll()
		return ActionMove
	case KeyEnter:
		return ActionAccept
	case KeyCancel:
		return ActionCancel
	default:
		return ActionNone
	}
}

func (v *TextViewerView) Render() string {
	if !v.Active {
		return ""
	}

	start := v.scroll
	end := start + textViewerWindowSize
	if end > len(v.lines) {
		end = len(v.lines)
	}
	if start > len(v.lines) {
		start = len(v.lines)
	}

	bodyLines := make([]string, 0, end-start)
	for _, line := range v.lines[start:end] {
		bodyLines = append(bodyLines, line)
	}

	content := []string{renderDialogTextBlock(strings.Join(bodyLines, "\n"), false)}
	if v.maxScroll() > 0 {
		content = append(content, renderDialogMuted(
			"Lines "+intToString(start+1)+"-"+intToString(end)+" of "+intToString(len(v.lines)),
		))
	}

	help := v.Help
	if strings.TrimSpace(help) == "" {
		help = "j/k or up/down scroll  g/G top/bottom  Enter/Esc close"
	}

	return renderDialogChrome(
		titleOrDefault(v.Title, "Details"),
		"",
		content,
		help,
	)
}

func (v *TextViewerView) maxScroll() int {
	if len(v.lines) <= textViewerWindowSize {
		return 0
	}
	return len(v.lines) - textViewerWindowSize
}

func intToString(value int) string {
	return strconv.Itoa(value)
}
