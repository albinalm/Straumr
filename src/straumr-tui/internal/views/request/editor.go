package request

import (
	"fmt"
	"strings"
)

type EditorMode string

const (
	EditorModeCreate EditorMode = "create"
	EditorModeEdit   EditorMode = "edit"
)

type EditorField string

const (
	EditorFieldName    EditorField = "name"
	EditorFieldURL     EditorField = "url"
	EditorFieldMethod  EditorField = "method"
	EditorFieldParams  EditorField = "params"
	EditorFieldHeaders EditorField = "headers"
	EditorFieldBody    EditorField = "body"
	EditorFieldAuth    EditorField = "auth"
)

type Pair struct {
	Key   string
	Value string
}

type Draft struct {
	Name     string
	URL      string
	Method   string
	Params   []Pair
	Headers  []Pair
	Body     string
	BodyType string
	Auth     string
}

type EditorActionKind string

const (
	EditorActionNone   EditorActionKind = ""
	EditorActionMove   EditorActionKind = "move"
	EditorActionSubmit EditorActionKind = "submit"
	EditorActionCancel EditorActionKind = "cancel"
)

type EditorAction struct {
	Kind  EditorActionKind
	Field EditorField
}

type EditorView struct {
	Active  bool
	Mode    EditorMode
	Focus   EditorField
	Draft   Draft
	Message string
}

func (v *EditorView) Open(mode EditorMode, draft Draft) {
	v.Active = true
	v.Mode = mode
	v.Focus = EditorFieldName
	v.Draft = draft
	v.Message = ""
}

func (v *EditorView) SetDraft(draft Draft) {
	v.Draft = draft
}

func (v *EditorView) Close() {
	v.Active = false
	v.Message = ""
}

func (v *EditorView) Snapshot() MutationDraft {
	return v.Draft.MutationDraft()
}

func (v *EditorView) CurrentDraft() Draft {
	return v.Draft.Clone()
}

func (v *EditorView) Submit() Submission {
	return NewSubmission(v.Mode, Item{}, v.Draft)
}

func (v *EditorView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder

	title := "Request editor"
	if v.Mode != "" {
		title = fmt.Sprintf("%s (%s)", title, v.Mode)
	}

	b.WriteString(title)
	b.WriteString("\n")
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	b.WriteString(editorSection("Basics"))
	b.WriteString(editorLine(v.Focus == EditorFieldName, "Name", v.Draft.Name))
	b.WriteString(editorLine(v.Focus == EditorFieldURL, "URL", v.Draft.URL))
	b.WriteString(editorLine(v.Focus == EditorFieldMethod, "Method", v.Draft.Method))
	b.WriteString("\n")
	b.WriteString(editorSection("Request Data"))
	b.WriteString(editorLine(v.Focus == EditorFieldParams, "Params", fmt.Sprintf("%d entries", len(v.Draft.Params))))
	b.WriteString(editorLine(v.Focus == EditorFieldHeaders, "Headers", fmt.Sprintf("%d entries", len(v.Draft.Headers))))
	b.WriteString(editorLine(v.Focus == EditorFieldBody, "Body", bodySummary(v.Draft)))
	b.WriteString(editorLine(v.Focus == EditorFieldAuth, "Auth", v.Draft.Auth))
	b.WriteString("\n")
	b.WriteString("Enter submit  Esc cancel  up/down move")

	return strings.TrimRight(b.String(), "\n")
}

func (v *EditorView) HandleKey(key Key) EditorAction {
	switch key {
	case KeyCancel:
		return EditorAction{Kind: EditorActionCancel, Field: v.Focus}
	case KeyEnter:
		return EditorAction{Kind: EditorActionSubmit, Field: v.Focus}
	case KeyUp:
		v.move(-1)
		return EditorAction{Kind: EditorActionMove, Field: v.Focus}
	case KeyDown:
		v.move(1)
		return EditorAction{Kind: EditorActionMove, Field: v.Focus}
	default:
		return EditorAction{Kind: EditorActionNone, Field: v.Focus}
	}
}

func (v *EditorView) move(delta int) {
	order := []EditorField{
		EditorFieldName,
		EditorFieldURL,
		EditorFieldMethod,
		EditorFieldParams,
		EditorFieldHeaders,
		EditorFieldBody,
		EditorFieldAuth,
	}

	index := 0
	for i, field := range order {
		if field == v.Focus {
			index = i
			break
		}
	}

	index += delta
	if index < 0 {
		index = 0
	}
	if index >= len(order) {
		index = len(order) - 1
	}

	v.Focus = order[index]
}

func editorLine(selected bool, label, value string) string {
	marker := "  "
	if selected {
		marker = "> "
	}

	if value == "" {
		value = "(empty)"
	}

	return fmt.Sprintf("%s%s: %s\n", marker, label, value)
}

func editorSection(title string) string {
	return fmt.Sprintf("%s\n", title)
}

func bodySummary(draft Draft) string {
	if draft.BodyType == "" && draft.Body == "" {
		return "(empty)"
	}

	if draft.BodyType == "" {
		return fmt.Sprintf("%d chars", len(draft.Body))
	}

	if draft.Body == "" {
		return draft.BodyType
	}

	return fmt.Sprintf("%s, %d chars", draft.BodyType, len(draft.Body))
}
