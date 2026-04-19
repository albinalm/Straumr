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
	Title       string
	Label       string
	Value       string
	Placeholder string
	Message     string
	Cursor      int
}

func (v *TextInputView) Render() string {
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

func (v *SecretInputView) Render() string {
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
	Title   string
	Message string
	Items   []Pair
	Cursor  int
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
