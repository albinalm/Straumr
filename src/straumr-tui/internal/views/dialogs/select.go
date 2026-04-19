package dialogs

import (
	"fmt"
	"strings"
)

type Key string

const (
	KeyUp       Key = "up"
	KeyDown     Key = "down"
	KeyLeft     Key = "left"
	KeyRight    Key = "right"
	KeyHome     Key = "home"
	KeyEnd      Key = "end"
	KeyEnter    Key = "enter"
	KeyCancel   Key = "cancel"
	KeySearch   Key = "search"
	KeyDelete   Key = "delete"
	KeyTab      Key = "tab"
	KeyShiftTab Key = "shift-tab"
	KeyOpen     Key = "open"
)

type ActionKind string

const (
	ActionNone      ActionKind = ""
	ActionMove      ActionKind = "move"
	ActionAccept    ActionKind = "accept"
	ActionCancel    ActionKind = "cancel"
	ActionFilter    ActionKind = "filter"
	ActionOpen      ActionKind = "open"
	ActionBack      ActionKind = "back"
	ActionDelete    ActionKind = "delete"
	ActionEditValue ActionKind = "edit-value"
	ActionToggle    ActionKind = "toggle"
)

type Choice struct {
	Key         string
	Title       string
	Description string
	Disabled    bool
}

type SelectView struct {
	Title   string
	Message string
	Help    string
	Filter  string
	Cursor  int
	Items   []Choice
}

func (v *SelectView) SetItems(items []Choice) {
	v.Items = append(v.Items[:0], items...)
	if v.Cursor >= len(v.Items) {
		v.Cursor = len(v.Items) - 1
	}
	if v.Cursor < 0 {
		v.Cursor = 0
	}
}

func (v *SelectView) HandleKey(key Key) ActionKind {
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
	case KeyHome:
		v.Cursor = 0
		return ActionMove
	case KeyEnd:
		if len(v.Items) > 0 {
			v.Cursor = len(v.Items) - 1
		}
		return ActionMove
	case KeyEnter, KeyOpen:
		return ActionAccept
	case KeyCancel:
		return ActionCancel
	case KeySearch:
		return ActionFilter
	default:
		return ActionNone
	}
}

func (v *SelectView) Render() string {
	var b strings.Builder

	if v.Title != "" {
		b.WriteString(v.Title)
		b.WriteString("\n")
	}
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	if v.Filter != "" {
		b.WriteString(fmt.Sprintf("Filter: %s\n", v.Filter))
	}
	for index, item := range v.Items {
		prefix := "  "
		if index == v.Cursor {
			prefix = "> "
		}

		line := prefix + item.Title
		if item.Disabled {
			line += " [disabled]"
		}
		b.WriteString(line)
		b.WriteString("\n")
		if item.Description != "" {
			b.WriteString("  ")
			b.WriteString(item.Description)
			b.WriteString("\n")
		}
	}
	if len(v.Items) == 0 {
		b.WriteString("(empty)\n")
	}
	if v.Help != "" {
		b.WriteString(v.Help)
	}

	return strings.TrimRight(b.String(), "\n")
}

type ConfirmView struct {
	Title   string
	Message string
	Options []string
	Cursor  int
}

func (v *ConfirmView) SetOptions(options []string) {
	v.Options = append(v.Options[:0], options...)
	if v.Cursor >= len(v.Options) {
		v.Cursor = len(v.Options) - 1
	}
	if v.Cursor < 0 {
		v.Cursor = 0
	}
}

func (v *ConfirmView) HandleKey(key Key) ActionKind {
	switch key {
	case KeyLeft, KeyUp:
		if v.Cursor > 0 {
			v.Cursor--
		}
		return ActionMove
	case KeyRight, KeyDown:
		if v.Cursor < len(v.Options)-1 {
			v.Cursor++
		}
		return ActionMove
	case KeyEnter, KeyOpen:
		return ActionAccept
	case KeyCancel:
		return ActionCancel
	default:
		return ActionNone
	}
}

func (v *ConfirmView) Render() string {
	var b strings.Builder
	if v.Title != "" {
		b.WriteString(v.Title)
		b.WriteString("\n")
	}
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}
	for index, option := range v.Options {
		prefix := "  "
		if index == v.Cursor {
			prefix = "> "
		}
		b.WriteString(prefix)
		b.WriteString(option)
		b.WriteString("\n")
	}
	return strings.TrimRight(b.String(), "\n")
}
