package dialogs

import (
	"fmt"
	"strings"

	tea "github.com/charmbracelet/bubbletea"
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

type MultiLineInputView struct {
	OverlayState
	Title   string
	Message string
	Value   string
	Cursor  int
}

func (v *MultiLineInputView) Open(title, message, value string) {
	v.OverlayState.Open()
	v.Title = title
	v.Message = message
	v.Value = value
	v.Cursor = len([]rune(value))
}

func (v *MultiLineInputView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Message = ""
	v.Value = ""
	v.Cursor = 0
}

func (v *MultiLineInputView) Result(accepted bool) InputResult {
	return InputResult{
		Accepted:  accepted,
		Cancelled: !accepted,
		Value:     v.Value,
	}
}

func (v *MultiLineInputView) Render() string {
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

	lines := renderMultilineCursor(v.Value, v.Cursor)
	if len(lines) == 0 {
		lines = []string{"|"}
	}

	for _, line := range lines {
		b.WriteString("  ")
		b.WriteString(line)
		b.WriteString("\n")
	}

	b.WriteString("Ctrl+S accept  Ctrl+O load file  Ctrl+L clear  Enter newline  Esc back")
	return strings.TrimRight(b.String(), "\n")
}

func (v *MultiLineInputView) ApplyKey(msg tea.KeyMsg) ActionKind {
	switch msg.String() {
	case "ctrl+s":
		return ActionAccept
	case "esc":
		return ActionCancel
	case "left":
		v.moveHorizontal(-1)
	case "right":
		v.moveHorizontal(1)
	case "up":
		v.moveVertical(-1)
	case "down":
		v.moveVertical(1)
	case "home":
		v.moveToLineEdge(false)
	case "end":
		v.moveToLineEdge(true)
	case "backspace":
		v.backspace()
	case "delete":
		v.deleteForward()
	case "enter":
		v.insertText("\n")
	default:
		switch {
		case msg.Type == tea.KeySpace:
			v.insertText(" ")
		case len(msg.Runes) > 0:
			v.insertText(string(msg.Runes))
		}
	}

	return ActionNone
}

func (v *MultiLineInputView) SetValue(value string) {
	v.Value = value
	v.Cursor = len([]rune(value))
}

func (v *MultiLineInputView) moveHorizontal(delta int) {
	runes := []rune(v.Value)
	v.Cursor += delta
	if v.Cursor < 0 {
		v.Cursor = 0
	}
	if v.Cursor > len(runes) {
		v.Cursor = len(runes)
	}
}

func (v *MultiLineInputView) moveVertical(delta int) {
	line, col, lines := multilineCursorState(v.Value, v.Cursor)
	if len(lines) == 0 {
		return
	}
	target := line + delta
	if target < 0 {
		target = 0
	}
	if target >= len(lines) {
		target = len(lines) - 1
	}
	targetCol := col
	if targetCol > len(lines[target]) {
		targetCol = len(lines[target])
	}
	v.Cursor = multilineIndexFromLineCol(lines, target, targetCol)
}

func (v *MultiLineInputView) moveToLineEdge(end bool) {
	line, _, lines := multilineCursorState(v.Value, v.Cursor)
	if len(lines) == 0 {
		v.Cursor = 0
		return
	}
	col := 0
	if end {
		col = len(lines[line])
	}
	v.Cursor = multilineIndexFromLineCol(lines, line, col)
}

func (v *MultiLineInputView) backspace() {
	runes := []rune(v.Value)
	if v.Cursor <= 0 || v.Cursor > len(runes) {
		return
	}
	runes = append(runes[:v.Cursor-1], runes[v.Cursor:]...)
	v.Cursor--
	v.Value = string(runes)
}

func (v *MultiLineInputView) deleteForward() {
	runes := []rune(v.Value)
	if v.Cursor < 0 || v.Cursor >= len(runes) {
		return
	}
	runes = append(runes[:v.Cursor], runes[v.Cursor+1:]...)
	v.Value = string(runes)
}

func (v *MultiLineInputView) insertText(text string) {
	runes := []rune(v.Value)
	insert := []rune(text)
	if v.Cursor < 0 {
		v.Cursor = 0
	}
	if v.Cursor > len(runes) {
		v.Cursor = len(runes)
	}
	runes = append(runes[:v.Cursor], append(insert, runes[v.Cursor:]...)...)
	v.Cursor += len(insert)
	v.Value = string(runes)
}

func renderMultilineCursor(value string, cursor int) []string {
	runes := []rune(value)
	if cursor < 0 {
		cursor = 0
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	withCursor := append([]rune{}, runes[:cursor]...)
	withCursor = append(withCursor, '|')
	withCursor = append(withCursor, runes[cursor:]...)
	return strings.Split(string(withCursor), "\n")
}

func multilineCursorState(value string, cursor int) (line int, col int, lines []string) {
	lines = strings.Split(value, "\n")
	if len(lines) == 0 {
		lines = []string{""}
	}
	if cursor < 0 {
		cursor = 0
	}

	offset := 0
	for i, current := range lines {
		length := len([]rune(current))
		if cursor <= offset+length {
			return i, cursor - offset, lines
		}
		offset += length + 1
	}

	last := len(lines) - 1
	return last, len([]rune(lines[last])), lines
}

func multilineIndexFromLineCol(lines []string, line int, col int) int {
	index := 0
	for i := 0; i < line; i++ {
		index += len([]rune(lines[i])) + 1
	}
	return index + col
}

type PairInputField string

const (
	PairInputFieldKey   PairInputField = "key"
	PairInputFieldValue PairInputField = "value"
)

type PairInputView struct {
	OverlayState
	Title       string
	Message     string
	Focus       PairInputField
	Key         string
	Value       string
	KeyCursor   int
	ValueCursor int
}

func (v *PairInputView) Open(title, message, key, value string) {
	v.OverlayState.Open()
	v.Title = title
	v.Message = message
	v.Focus = PairInputFieldKey
	v.Key = key
	v.Value = value
	v.KeyCursor = len([]rune(key))
	v.ValueCursor = len([]rune(value))
}

func (v *PairInputView) Close() {
	v.OverlayState.Close()
	v.Title = ""
	v.Message = ""
	v.Focus = PairInputFieldKey
	v.Key = ""
	v.Value = ""
	v.KeyCursor = 0
	v.ValueCursor = 0
}

func (v *PairInputView) Result(accepted bool) PairInputResult {
	return PairInputResult{
		Accepted:  accepted,
		Cancelled: !accepted,
		Pair: Pair{
			Key:   v.Key,
			Value: v.Value,
		},
	}
}

func (v *PairInputView) Render() string {
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

	keyPrefix := "  "
	valuePrefix := "  "
	if v.Focus == PairInputFieldKey {
		keyPrefix = "> "
	}
	if v.Focus == PairInputFieldValue {
		valuePrefix = "> "
	}

	b.WriteString(fmt.Sprintf("%sKey: %s\n", keyPrefix, renderSingleLineCursor(v.Key, v.KeyCursor, "Header-Name")))
	b.WriteString(fmt.Sprintf("%sValue: %s\n", valuePrefix, renderSingleLineCursor(v.Value, v.ValueCursor, "(empty)")))
	b.WriteString("Enter next/accept  Tab switch field  Ctrl+S accept  Esc cancel")
	return strings.TrimRight(b.String(), "\n")
}

func (v *PairInputView) ApplyKey(msg tea.KeyMsg) ActionKind {
	switch msg.String() {
	case "ctrl+s":
		return ActionAccept
	case "esc":
		return ActionCancel
	case "tab", "shift+tab", "backtab", "up", "down", "j", "k":
		v.switchFocus()
	case "enter":
		if v.Focus == PairInputFieldKey {
			v.Focus = PairInputFieldValue
			return ActionNone
		}
		return ActionAccept
	default:
		value, cursor := v.activeField()
		applySingleLineEdit(value, cursor, msg)
	}

	return ActionNone
}

func (v *PairInputView) switchFocus() {
	if v.Focus == PairInputFieldKey {
		v.Focus = PairInputFieldValue
		return
	}
	v.Focus = PairInputFieldKey
}

func (v *PairInputView) activeField() (*string, *int) {
	if v.Focus == PairInputFieldValue {
		return &v.Value, &v.ValueCursor
	}
	return &v.Key, &v.KeyCursor
}

func renderSingleLineCursor(value string, cursor int, placeholder string) string {
	runes := []rune(value)
	if cursor < 0 {
		cursor = 0
	}
	if cursor > len(runes) {
		cursor = len(runes)
	}
	withCursor := append([]rune{}, runes[:cursor]...)
	withCursor = append(withCursor, '|')
	withCursor = append(withCursor, runes[cursor:]...)
	rendered := string(withCursor)
	if value == "" {
		return "|" + placeholder
	}
	return rendered
}

func applySingleLineEdit(value *string, cursor *int, msg tea.KeyMsg) {
	runes := []rune(*value)
	if *cursor < 0 {
		*cursor = 0
	}
	if *cursor > len(runes) {
		*cursor = len(runes)
	}

	switch msg.String() {
	case "left":
		if *cursor > 0 {
			*cursor--
		}
		return
	case "right":
		if *cursor < len(runes) {
			*cursor++
		}
		return
	case "home":
		*cursor = 0
		return
	case "end":
		*cursor = len(runes)
		return
	case "backspace":
		if *cursor > 0 {
			runes = append(runes[:*cursor-1], runes[*cursor:]...)
			*cursor--
			*value = string(runes)
		}
		return
	case "delete":
		if *cursor < len(runes) {
			runes = append(runes[:*cursor], runes[*cursor+1:]...)
			*value = string(runes)
		}
		return
	}

	if msg.Type == tea.KeySpace {
		runes = append(runes[:*cursor], append([]rune{' '}, runes[*cursor:]...)...)
		*cursor++
		*value = string(runes)
		return
	}
	if len(msg.Runes) == 0 {
		return
	}

	insert := msg.Runes
	runes = append(runes[:*cursor], append(insert, runes[*cursor:]...)...)
	*cursor += len(insert)
	*value = string(runes)
}
