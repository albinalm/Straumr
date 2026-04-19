package dialogs

import (
	"fmt"
	"strings"
)

type Pair struct {
	Key       string
	Value     string
	Sensitive bool
}

type TextInputView struct {
	OverlayState
	Title       string
	Label       string
	Value       string
	Placeholder string
	Message     string
	Cursor      int
}

func (v *TextInputView) Open(title, label, value, placeholder, message string) {
	v.OverlayState.Open()
	v.Title = title
	v.Label = label
	v.Value = value
	v.Placeholder = placeholder
	v.Message = message
	v.Cursor = len(value)
}

func (v *TextInputView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Label = ""
	v.Value = ""
	v.Placeholder = ""
	v.Message = ""
	v.Cursor = 0
}

func (v *TextInputView) Result(accepted bool) InputResult {
	return InputResult{
		Accepted:  accepted,
		Cancelled: !accepted,
		Value:     v.Value,
	}
}

func (v *TextInputView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder

	if v.Title != "" {
		b.WriteString(v.Title)
		b.WriteString("\n")
	}
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	label := v.Label
	if label == "" {
		label = "Value"
	}
	b.WriteString(fmt.Sprintf("%s: %s", label, displayOrPlaceholder(v.Value, v.Placeholder)))
	return strings.TrimRight(b.String(), "\n")
}

func (v *TextInputView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyEnter:
		return ActionAccept
	case KeyCancel:
		return ActionCancel
	default:
		return ActionNone
	}
}

type SecretInputView struct {
	TextInputView
	Visible bool
}

func (v *SecretInputView) Open(title, label, value, placeholder, message string) {
	v.TextInputView.Open(title, label, value, placeholder, message)
	v.Visible = false
}

func (v *SecretInputView) Close() {
	v.TextInputView.Close()
	v.Visible = false
}

func (v *SecretInputView) Result(accepted bool) InputResult {
	return v.TextInputView.Result(accepted)
}

func (v *SecretInputView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder

	title := titleOrDefault(v.TextInputView.Title, "Secret input")
	b.WriteString(title)
	b.WriteString("\n")
	if v.TextInputView.Message != "" {
		b.WriteString(v.TextInputView.Message)
		b.WriteString("\n")
	}
	label := v.TextInputView.Label
	if label == "" {
		label = "Value"
	}

	value := v.TextInputView.Value
	if !v.Visible {
		value = maskedValue(value)
	}

	b.WriteString(fmt.Sprintf("%s: %s", label, displayOrPlaceholder(value, v.TextInputView.Placeholder)))
	return strings.TrimRight(b.String(), "\n")
}

type KeyValueEditorView struct {
	OverlayState
	Title   string
	Message string
	Items   []Pair
	Cursor  int
}

func (v *KeyValueEditorView) Open(title, message string, items []Pair) {
	v.OverlayState.Open()
	v.Title = title
	v.Message = message
	v.Cursor = 0
	v.SetItems(items)
}

func (v *KeyValueEditorView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Message = ""
	v.Items = nil
	v.Cursor = 0
}

func (v *KeyValueEditorView) SetItems(items []Pair) {
	v.Items = append(v.Items[:0], items...)
	if v.Cursor >= len(v.Items) {
		v.Cursor = len(v.Items) - 1
	}
	if v.Cursor < 0 {
		v.Cursor = 0
	}
}

func (v *KeyValueEditorView) Result(accepted bool) KeyValueResult {
	result := KeyValueResult{
		Accepted:  accepted,
		Cancelled: !accepted,
		Index:     v.Cursor,
		Items:     append([]Pair(nil), v.Items...),
	}
	if v.Cursor >= 0 && v.Cursor < len(v.Items) {
		result.Item = v.Items[v.Cursor]
	}
	return result
}

func (v *KeyValueEditorView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyUp:
		if v.Cursor > 0 {
			v.Cursor--
		}
		return ActionMove
	case KeyDown:
		if v.Cursor < len(v.Items)-1 {
			v.Cursor++
		}
		return ActionMove
	case KeyEnter:
		return ActionEditValue
	case KeyDelete:
		return ActionDelete
	case KeyCancel:
		return ActionCancel
	default:
		return ActionNone
	}
}

func (v *KeyValueEditorView) Render() string {
	if !v.Active {
		return ""
	}

	var b strings.Builder
	if v.Title != "" {
		b.WriteString(v.Title)
		b.WriteString("\n")
	}
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	if len(v.Items) == 0 {
		b.WriteString("(empty)")
		return b.String()
	}
	for index, item := range v.Items {
		prefix := "  "
		if index == v.Cursor {
			prefix = "> "
		}
		value := item.Value
		if item.Sensitive {
			value = maskedValue(value)
		}
		b.WriteString(fmt.Sprintf("%s%s: %s\n", prefix, item.Key, displayOrPlaceholder(value, "(empty)")))
	}
	return strings.TrimRight(b.String(), "\n")
}

func displayOrPlaceholder(value, placeholder string) string {
	if value == "" {
		return placeholder
	}
	return value
}

func titleOrDefault(title, fallback string) string {
	if strings.TrimSpace(title) == "" {
		return fallback
	}
	return title
}

func maskedValue(value string) string {
	if value == "" {
		return ""
	}
	return "******"
}
