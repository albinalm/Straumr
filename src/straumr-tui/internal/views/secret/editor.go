package secret

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
	EditorFieldName   EditorField = "name"
	EditorFieldValue  EditorField = "value"
	EditorFieldReveal EditorField = "reveal"
)

type Draft struct {
	Name   string
	Value  string
	Reveal bool
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

func (v *EditorView) Close() {
	v.Active = false
	v.Message = ""
}

func (v *EditorView) Snapshot() MutationDraft {
	return v.Draft.MutationDraft()
}

func (v *EditorView) Submit() Submission {
	return Submission{
		Mode:  v.Mode,
		Draft: v.Snapshot(),
	}
}

func (v *EditorView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder

	title := "Secret editor"
	if v.Mode != "" {
		title = fmt.Sprintf("%s (%s)", title, v.Mode)
	}

	b.WriteString(title)
	b.WriteString("\n")
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	b.WriteString(editorLine(v.Focus == EditorFieldName, "Name", v.Draft.Name))
	b.WriteString(editorLine(v.Focus == EditorFieldValue, "Value", maskedValue(v.Draft.Value, v.Draft.Reveal)))
	b.WriteString(editorLine(v.Focus == EditorFieldReveal, "Reveal", boolText(v.Draft.Reveal)))
	b.WriteString("Enter submit  Esc cancel  j/k or Tab/Shift+Tab move")

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
	case KeyToggle:
		v.Draft.Reveal = !v.Draft.Reveal
		return EditorAction{Kind: EditorActionMove, Field: v.Focus}
	default:
		return EditorAction{Kind: EditorActionNone, Field: v.Focus}
	}
}

func (v *EditorView) move(delta int) {
	order := []EditorField{EditorFieldName, EditorFieldValue, EditorFieldReveal}
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

func maskedValue(value string, reveal bool) string {
	if value == "" {
		return "(empty)"
	}
	if reveal {
		return value
	}
	return "******"
}
