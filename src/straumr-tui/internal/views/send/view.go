package send

import (
	"encoding/json"
	"fmt"
	"strings"
	"time"

	"github.com/charmbracelet/lipgloss"

	"straumr-tui/internal/ui/theme"
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
	bodySource    string
	bodyRendered  string
	bodyNotice    string
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
		v.setBeautified(true)
		return Action{Kind: ActionBeautify}
	case KeyRevert:
		v.setBeautified(false)
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
	v.refreshBodyPresentation()
}

func (v *View) SetError(message string) {
	v.Response.Error = message
	v.refreshBodyPresentation()
}

func (v *View) Render() string {
	styles := theme.CurrentStyles()
	sections := []string{
		styles.HelpText.Render(renderFooter()),
		renderStatus(v.StatusText),
		renderRequest(v.Request),
		renderMeta(v.Response),
	}
	if notes := renderNotes(v.Response); notes != "" {
		sections = append(sections, notes)
	}

	summary := renderPane(renderSummaryPane(v.Response, v.FocusedPane == PaneSummary), v.FocusedPane == PaneSummary)
	body := renderPane(renderBodyPane(v), v.FocusedPane == PaneBody)
	sections = append(sections, summary, body)

	return strings.TrimRight(strings.Join(sections, "\n\n"), "\n")
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
	styles := theme.CurrentStyles()
	if text == "" {
		return styles.Muted.Render("• Waiting for request")
	}

	return styles.Success.Render("✓ " + text)
}

func renderRequest(request Request) string {
	styles := theme.CurrentStyles()
	method := theme.MethodStyle(request.Method).Render(strings.ToUpper(strings.TrimSpace(request.Method)))
	uri := strings.TrimSpace(request.URI)
	if method == "" {
		method = styles.Muted.Render("REQUEST")
	}
	if uri == "" {
		uri = styles.Muted.Render("(no request loaded)")
	}

	lines := []string{}
	if strings.TrimSpace(request.Name) != "" {
		lines = append(lines, styles.PanelTitle.Render(request.Name))
	}
	lines = append(lines, method+"  "+styles.Info.Underline(true).Render(uri))
	return strings.Join(lines, "\n")
}

func renderMeta(response Response) string {
	styles := theme.CurrentStyles()
	if response.Error != "" {
		return styles.Danger.Render(response.Error)
	}

	meta := []string{}
	if response.StatusText != "" {
		meta = append(meta, styles.Success.Render(response.StatusText))
	}
	if response.Duration > 0 {
		meta = append(meta, styles.Info.Render(fmt.Sprintf("%d ms", response.Duration.Milliseconds())))
	}
	if response.HTTPVersion != "" {
		meta = append(meta, styles.RowTitle.Render(response.HTTPVersion))
	}

	if len(meta) == 0 {
		return styles.Muted.Render("Response: pending")
	}

	return strings.Join(meta, styles.Muted.Render(" • "))
}

func renderNotes(response Response) string {
	styles := theme.CurrentStyles()
	if len(response.Notes) == 0 {
		return ""
	}

	lines := []string{styles.PanelTitle.Render("Notes")}
	for _, note := range response.Notes {
		if strings.TrimSpace(note) == "" {
			continue
		}
		lines = append(lines, "  "+styles.RowDetail.Render(note))
	}

	if len(lines) == 1 {
		return ""
	}

	return strings.Join(lines, "\n")
}

func renderSummaryPane(response Response, active bool) string {
	lines := []string{paneHeading("Headers", active)}
	if strings.TrimSpace(response.Summary) == "" {
		lines = append(lines, emptyPaneLine())
		return strings.Join(lines, "\n")
	}

	for _, line := range strings.Split(response.Summary, "\n") {
		if strings.TrimSpace(line) == "" {
			continue
		}
		lines = append(lines, renderSummaryLine(line))
	}

	if len(lines) == 1 {
		lines = append(lines, emptyPaneLine())
	}
	return strings.Join(lines, "\n")
}

func renderBodyPane(v *View) string {
	lines := []string{bodyPaneTitle(v)}
	if v.bodyNotice != "" {
		lines = append(lines, "  "+theme.CurrentStyles().Warning.Render(v.bodyNotice))
	}
	body := v.bodyRendered
	if body == "" {
		body = v.bodySource
	}
	if body == "" {
		lines = append(lines, emptyPaneLine())
		return strings.Join(lines, "\n")
	}

	for _, line := range strings.Split(body, "\n") {
		lines = append(lines, "  "+theme.CurrentStyles().RowDetail.Render(line))
	}
	return strings.Join(lines, "\n")
}

func renderFooter() string {
	return "j/k or up/down scroll  tab/shift-tab switch pane  c copy pane  p copy template  b beautify  R revert  w save body  x export  n dry-run  s refresh  esc close"
}

func (v *View) setBeautified(enabled bool) {
	v.Beautified = enabled
	v.refreshBodyPresentation()
}

func (v *View) refreshBodyPresentation() {
	v.bodyNotice = ""
	v.bodySource = resolveBodySource(v.Response)
	v.bodyRendered = v.bodySource

	if v.bodySource == "" {
		return
	}

	if !v.Beautified {
		return
	}

	pretty, ok := beautifyJSON(v.bodySource)
	if !ok {
		v.bodyNotice = "Beautify unavailable: body is not valid JSON"
		return
	}

	v.bodyRendered = pretty
}

func resolveBodySource(response Response) string {
	if strings.TrimSpace(response.RawBody) != "" {
		return response.RawBody
	}

	return response.Body
}

func bodyPaneTitle(v *View) string {
	label := "Body"
	switch {
	case v.bodySource == "":
		label = "Body"
	case v.Beautified:
		label = "Body (beautified)"
	default:
		label = "Body (raw)"
	}

	return paneHeading(label, v.FocusedPane == PaneBody)
}

func paneHeading(label string, active bool) string {
	styles := theme.CurrentStyles()
	if active {
		return styles.PanelTitle.Render(label + " [active]")
	}

	return styles.PanelTitle.Render(label)
}

func beautifyJSON(value string) (string, bool) {
	trimmed := strings.TrimSpace(value)
	if trimmed == "" {
		return "", false
	}

	var decoded any
	if err := json.Unmarshal([]byte(trimmed), &decoded); err != nil {
		return "", false
	}

	formatted, err := json.MarshalIndent(decoded, "", "  ")
	if err != nil {
		return "", false
	}

	return string(formatted), true
}

func renderPane(body string, active bool) string {
	styles := theme.CurrentStyles()
	panel := styles.Panel
	if active {
		if border := theme.Active().Primary; strings.TrimSpace(border) != "" {
			if resolved := resolvePanelColor(border); resolved != "" {
				panel = panel.BorderForeground(lipgloss.Color(resolved))
			}
		}
	}
	return panel.Render(body)
}

func renderSummaryLine(line string) string {
	styles := theme.CurrentStyles()
	if key, value, ok := strings.Cut(line, ":"); ok {
		return "  " + styles.RowSummary.Render(strings.TrimSpace(key)+":") + " " + styles.RowDetail.Render(strings.TrimSpace(value))
	}
	return "  " + styles.RowDetail.Render(line)
}

func emptyPaneLine() string {
	return "  " + theme.CurrentStyles().Muted.Render("(empty)")
}

func resolvePanelColor(token string) string {
	trimmed := strings.TrimSpace(token)
	if trimmed == "" {
		return ""
	}
	if strings.HasPrefix(trimmed, "#") {
		return trimmed
	}
	switch strings.ToLower(trimmed) {
	case "black":
		return "0"
	case "red":
		return "1"
	case "green":
		return "2"
	case "yellow":
		return "3"
	case "blue":
		return "4"
	case "magenta":
		return "5"
	case "cyan":
		return "6"
	case "gray", "grey":
		return "7"
	case "darkgray", "darkgrey":
		return "8"
	case "brightred":
		return "9"
	case "brightgreen":
		return "10"
	case "brightyellow":
		return "11"
	case "brightblue":
		return "12"
	case "brightmagenta":
		return "13"
	case "brightcyan":
		return "14"
	case "white":
		return "15"
	default:
		return trimmed
	}
}
