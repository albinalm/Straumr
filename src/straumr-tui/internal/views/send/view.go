package send

import (
	"fmt"
	"strings"
	"time"
)

type Key string

const (
	KeyUp           Key = "up"
	KeyDown         Key = "down"
	KeyHome         Key = "home"
	KeyEnd          Key = "end"
	KeyTab          Key = "tab"
	KeyShiftTab     Key = "shift-tab"
	KeyCancel       Key = "cancel"
	KeyCopyPane     Key = "copy-pane"
	KeyCopyTemplate Key = "copy-template"
	KeyBeautify     Key = "beautify"
	KeyRevert       Key = "revert"
	KeySaveBody     Key = "save-body"
	KeyExport       Key = "export"
	KeyDryRun       Key = "dry-run"
	KeyRefresh      Key = "refresh"
)

type ActionKind string

const (
	ActionNone         ActionKind = ""
	ActionScroll       ActionKind = "scroll"
	ActionSwitchPane   ActionKind = "switch-pane"
	ActionCopyPane     ActionKind = "copy-pane"
	ActionCopyTemplate ActionKind = "copy-template"
	ActionBeautify     ActionKind = "beautify"
	ActionRevert       ActionKind = "revert"
	ActionSaveBody     ActionKind = "save-body"
	ActionExport       ActionKind = "export"
	ActionCancel       ActionKind = "cancel"
	ActionSend         ActionKind = "send"
	ActionDryRun       ActionKind = "dry-run"
)

type Pane string

const (
	PaneSummary Pane = "summary"
	PaneBody    Pane = "body"
)

type Request struct {
	Name   string
	Method string
	URI    string
	Auth   string
}

type Response struct {
	StatusText  string
	StatusCode  *int
	Duration    time.Duration
	HTTPVersion string
	Summary     string
	Body        string
	RawBody     string
	Notes       []string
	Error       string
}

type View struct {
	StatusText    string
	Request       Request
	Response      Response
	FocusedPane   Pane
	SummaryScroll int
	BodyScroll    int
	Beautified    bool
	Available     bool
}

func NewView() *View {
	return &View{
		FocusedPane: PaneBody,
		StatusText:  "Waiting for request",
	}
}

func (v *View) HandleKey(key Key) Action {
	switch key {
	case KeyCancel:
		return Action{Kind: ActionCancel}
	case KeyUp, KeyDown, KeyHome, KeyEnd:
		return Action{Kind: ActionScroll}
	case KeyTab, KeyShiftTab:
		v.switchPane(key == KeyShiftTab)
		return Action{Kind: ActionSwitchPane}
	case KeyCopyPane:
		return Action{Kind: ActionCopyPane}
	case KeyCopyTemplate:
		return Action{Kind: ActionCopyTemplate}
	case KeyBeautify:
		v.Beautified = true
		return Action{Kind: ActionBeautify}
	case KeyRevert:
		v.Beautified = false
		return Action{Kind: ActionRevert}
	case KeySaveBody:
		return Action{Kind: ActionSaveBody}
	case KeyExport:
		return Action{Kind: ActionExport}
	case KeyDryRun:
		return Action{Kind: ActionDryRun}
	case KeyRefresh:
		return Action{Kind: ActionSend}
	default:
		return Action{Kind: ActionNone}
	}
}

type Action struct {
	Kind ActionKind
}

func (v *View) SetStatus(text string) {
	v.StatusText = text
}

func (v *View) SetRequest(request Request) {
	v.Request = request
}

func (v *View) SetResponse(response Response) {
	v.Response = response
}

func (v *View) SetError(message string) {
	v.Response.Error = message
}

func (v *View) Render() string {
	var b strings.Builder

	b.WriteString(renderStatus(v.StatusText))
	b.WriteString("\n")
	b.WriteString(renderRequest(v.Request))
	b.WriteString("\n")
	b.WriteString(renderMeta(v.Response))
	b.WriteString("\n")
	if notes := renderNotes(v.Response); notes != "" {
		b.WriteString(notes)
		b.WriteString("\n")
	}
	b.WriteString(renderSummaryPane(v.Response))
	b.WriteString("\n\n")
	b.WriteString(renderBodyPane(v))

	return strings.TrimRight(b.String(), "\n")
}

func (v *View) switchPane(reverse bool) {
	if reverse {
		if v.FocusedPane == PaneBody {
			v.FocusedPane = PaneSummary
		} else {
			v.FocusedPane = PaneBody
		}
		return
	}

	if v.FocusedPane == PaneSummary {
		v.FocusedPane = PaneBody
	} else {
		v.FocusedPane = PaneSummary
	}
}

func renderStatus(text string) string {
	if text == "" {
		return "Status: idle"
	}

	return fmt.Sprintf("Status: %s", text)
}

func renderRequest(request Request) string {
	parts := []string{}
	if request.Method != "" {
		parts = append(parts, strings.ToUpper(request.Method))
	}
	if request.URI != "" {
		parts = append(parts, request.URI)
	}
	line := strings.Join(parts, " ")
	if line == "" {
		line = "(no request loaded)"
	}

	if request.Name != "" {
		return fmt.Sprintf("Request: %s\n  %s", request.Name, line)
	}

	return fmt.Sprintf("Request: %s", line)
}

func renderMeta(response Response) string {
	if response.Error != "" {
		return fmt.Sprintf("Response: error\n  %s", response.Error)
	}

	meta := []string{}
	if response.StatusText != "" {
		meta = append(meta, response.StatusText)
	}
	if response.Duration > 0 {
		meta = append(meta, fmt.Sprintf("%d ms", response.Duration.Milliseconds()))
	}
	if response.HTTPVersion != "" {
		meta = append(meta, response.HTTPVersion)
	}

	if len(meta) == 0 {
		return "Response: pending"
	}

	return fmt.Sprintf("Response: %s", strings.Join(meta, "  "))
}

func renderNotes(response Response) string {
	if len(response.Notes) == 0 {
		return ""
	}

	lines := []string{"Notes"}
	for _, note := range response.Notes {
		if strings.TrimSpace(note) == "" {
			continue
		}
		lines = append(lines, "  "+note)
	}

	if len(lines) == 1 {
		return ""
	}

	return strings.Join(lines, "\n")
}

func renderSummaryPane(response Response) string {
	lines := []string{"Summary"}
	if response.Summary == "" {
		lines = append(lines, "  (empty)")
	} else {
		for _, line := range strings.Split(response.Summary, "\n") {
			lines = append(lines, "  "+line)
		}
	}
	return strings.Join(lines, "\n")
}

func renderBodyPane(v *View) string {
	lines := []string{"Body"}
	body := v.Response.Body
	if body == "" {
		if v.Response.RawBody == "" {
			lines = append(lines, "  (empty)")
			return strings.Join(lines, "\n")
		}
		body = v.Response.RawBody
	}

	for _, line := range strings.Split(body, "\n") {
		lines = append(lines, "  "+line)
	}
	return strings.Join(lines, "\n")
}
