package workspace

import (
	"fmt"
	"strings"
	"time"

	"straumr-tui/internal/ui/theme"
	"straumr-tui/internal/views/common"
)

// Assumption: the Bubble Tea root model will translate its own key messages
// into these simple string tokens before handing them to the view layer.
// This keeps the view package independent from app/state ownership.
type Key string

const (
	KeyUp      Key = "up"
	KeyDown    Key = "down"
	KeyHome    Key = "home"
	KeyEnd     Key = "end"
	KeyEnter   Key = "enter"
	KeyInspect Key = "inspect"
	KeySearch  Key = "search"
	KeyCommand Key = "command"
	KeySet     Key = "set"
	KeyCreate  Key = "create"
	KeyDelete  Key = "delete"
	KeyEdit    Key = "edit"
	KeyCopy    Key = "copy"
	KeyImport  Key = "import"
	KeyExport  Key = "export"
	KeyOpen    Key = "open"
	KeyCancel  Key = "cancel"
)

type ActionKind string

const (
	ActionNone       ActionKind = ""
	ActionMoveCursor ActionKind = "move-cursor"
	ActionOpenMenu   ActionKind = "open-menu"
	ActionCloseMenu  ActionKind = "close-menu"
	ActionOpenTarget ActionKind = "open-target"
	ActionInspect    ActionKind = "inspect"
	ActionSetActive  ActionKind = "set-active"
	ActionCreate     ActionKind = "create"
	ActionDelete     ActionKind = "delete"
	ActionEdit       ActionKind = "edit"
	ActionCopy       ActionKind = "copy"
	ActionImport     ActionKind = "import"
	ActionExport     ActionKind = "export"
	ActionSearch     ActionKind = "search"
	ActionCommand    ActionKind = "command"
)

type OpenTarget string

const (
	TargetRequests OpenTarget = "requests"
	TargetAuths    OpenTarget = "auths"
	TargetSecrets  OpenTarget = "secrets"
	TargetActivate OpenTarget = "activate"
)

type Action struct {
	Kind   ActionKind
	Target OpenTarget
	Item   Item
}

type Item struct {
	ID           string
	Name         string
	Path         string
	Requests     *int
	Secrets      *int
	Auths        *int
	LastAccessed *time.Time
	Current      bool
	Damaged      bool
	Missing      bool
}

type View struct {
	List        common.ListView
	Items       []Item
	MenuOpen    bool
	MenuCursor  int
	Message     string
	WorkspaceID string
}

func NewView() *View {
	return &View{
		List: common.ListView{
			State: common.ListState{
				Title:     "Workspaces",
				EmptyText: "No workspaces found",
				Hints:     "j/k or up/down move  g/G top/bottom  enter/o open  i inspect  s set active  c create  d delete  e edit  y copy  I import  x export  / search  : command",
			},
		},
	}
}

func (v *View) SetItems(items []Item) {
	v.Items = append(v.Items[:0], items...)
	v.List.SetRows(toRows(v.Items))
	if v.List.State.Cursor >= len(v.Items) {
		v.List.State.Cursor = len(v.Items) - 1
	}
}

func (v *View) Render() string {
	state := v.List.State
	state.Message = v.Message
	state.Footer = fmt.Sprintf("%d/%d workspaces", len(v.Items), len(v.Items))
	rows := toRows(v.Items)
	out := common.Render(state, rows)

	if v.MenuOpen {
		if out != "" {
			out += "\n\n"
		}
		out += renderMenu(v.MenuCursor)
	}

	return out
}

func (v *View) HandleKey(key Key) Action {
	if v.MenuOpen {
		return v.handleMenuKey(key)
	}

	switch key {
	case KeyUp:
		v.List.MoveCursor(-1)
		return Action{Kind: ActionMoveCursor, Item: v.currentItem()}
	case KeyDown:
		v.List.MoveCursor(1)
		return Action{Kind: ActionMoveCursor, Item: v.currentItem()}
	case KeyHome:
		v.List.Home()
		return Action{Kind: ActionMoveCursor, Item: v.currentItem()}
	case KeyEnd:
		v.List.End()
		return Action{Kind: ActionMoveCursor, Item: v.currentItem()}
	case KeyEnter, KeyOpen:
		v.MenuOpen = true
		v.MenuCursor = 0
		return Action{Kind: ActionOpenMenu, Item: v.currentItem()}
	case KeyInspect:
		return Action{Kind: ActionInspect, Item: v.currentItem()}
	case KeySet:
		return Action{Kind: ActionSetActive, Item: v.currentItem()}
	case KeyCreate:
		return Action{Kind: ActionCreate}
	case KeyDelete:
		return Action{Kind: ActionDelete, Item: v.currentItem()}
	case KeyEdit:
		return Action{Kind: ActionEdit, Item: v.currentItem()}
	case KeyCopy:
		return Action{Kind: ActionCopy, Item: v.currentItem()}
	case KeyImport:
		return Action{Kind: ActionImport}
	case KeyExport:
		return Action{Kind: ActionExport, Item: v.currentItem()}
	case KeySearch:
		return Action{Kind: ActionSearch}
	case KeyCommand:
		return Action{Kind: ActionCommand}
	default:
		return Action{Kind: ActionNone}
	}
}

func (v *View) handleMenuKey(key Key) Action {
	switch key {
	case KeyUp:
		if v.MenuCursor > 0 {
			v.MenuCursor--
		}
	case KeyDown:
		if v.MenuCursor < 3 {
			v.MenuCursor++
		}
	case KeyCancel:
		v.MenuOpen = false
		return Action{Kind: ActionCloseMenu, Item: v.currentItem()}
	case KeyEnter, KeyOpen:
		v.MenuOpen = false
		switch v.MenuCursor {
		case 0:
			return Action{Kind: ActionOpenTarget, Target: TargetRequests, Item: v.currentItem()}
		case 1:
			return Action{Kind: ActionOpenTarget, Target: TargetAuths, Item: v.currentItem()}
		case 2:
			return Action{Kind: ActionOpenTarget, Target: TargetSecrets, Item: v.currentItem()}
		case 3:
			return Action{Kind: ActionOpenTarget, Target: TargetActivate, Item: v.currentItem()}
		}
	}

	return Action{Kind: ActionNone, Item: v.currentItem()}
}

func (v *View) currentItem() Item {
	if len(v.Items) == 0 || v.List.State.Cursor < 0 || v.List.State.Cursor >= len(v.Items) {
		return Item{}
	}

	return v.Items[v.List.State.Cursor]
}

func toRows(items []Item) []common.Row {
	rows := make([]common.Row, 0, len(items))
	for _, item := range items {
		rows = append(rows, common.Row{
			Key:     item.ID,
			Title:   displayName(item.Name, item.ID),
			Summary: workspaceSummary(item),
			Details: workspaceDetails(item),
			Current: item.Current,
			Damaged: item.Damaged,
			Missing: item.Missing,
		})
	}
	return rows
}

func workspaceSummary(item Item) string {
	if item.ID == "" {
		return item.ID
	}

	return item.ID
}

func workspaceDetails(item Item) []string {
	stats := make([]string, 0, 3)
	stats = append(stats, countText("req", item.Requests))
	stats = append(stats, countText("sec", item.Secrets))
	stats = append(stats, countText("auth", item.Auths))

	details := []string{strings.Join(stats, " • ")}
	if item.LastAccessed != nil {
		details[0] += " • " + item.LastAccessed.Format("2006-01-02")
	}
	if item.Path != "" {
		details = append(details, "path: "+item.Path)
	}
	return details
}

func displayName(name, fallback string) string {
	if strings.TrimSpace(name) != "" {
		return name
	}
	return fallback
}

func countText(label string, value *int) string {
	if value == nil {
		return fmt.Sprintf("%s: n/a", label)
	}

	return fmt.Sprintf("%d %s", *value, label)
}

func menuChoiceLine(selected bool, label string) string {
	prefix := "  "
	if selected {
		prefix = "▸ "
	}

	return prefix + label
}

func renderMenu(cursor int) string {
	styles := theme.CurrentStyles()
	lines := []string{
		styles.PanelTitle.Render("Workspace"),
		menuChoiceLine(cursor == 0, "Requests"),
		menuChoiceLine(cursor == 1, "Auths"),
		menuChoiceLine(cursor == 2, "Secrets"),
		menuChoiceLine(cursor == 3, "Set active"),
		"",
		styles.Muted.Render("Esc cancel  Enter confirm"),
	}
	return styles.Panel.Render(strings.Join(lines, "\n"))
}
