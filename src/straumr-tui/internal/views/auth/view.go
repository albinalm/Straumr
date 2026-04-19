package auth

import (
	"fmt"
	"strings"
	"time"

	"straumr-tui/internal/views/common"
)

// Key is the local token set the Bubble Tea root should translate into before
// calling the auth view. The package does not depend on tea.Msg.
type Key string

const (
	KeyUp        Key = "up"
	KeyDown      Key = "down"
	KeyHome      Key = "home"
	KeyEnd       Key = "end"
	KeyEnter     Key = "enter"
	KeyInspect   Key = "inspect"
	KeySearch    Key = "search"
	KeyCommand   Key = "command"
	KeyCreate    Key = "create"
	KeyDelete    Key = "delete"
	KeyEdit      Key = "edit"
	KeyEditAlt   Key = "edit-alt"
	KeyCopy      Key = "copy"
	KeyCancel    Key = "cancel"
	KeyOpen      Key = "open"
	KeyActivate  Key = "activate"
	KeyDirectory Key = "directory"
)

type ActionKind string

const (
	ActionNone       ActionKind = ""
	ActionMoveCursor ActionKind = "move-cursor"
	ActionCreate     ActionKind = "create"
	ActionDelete     ActionKind = "delete"
	ActionEdit       ActionKind = "edit"
	ActionEditor     ActionKind = "editor"
	ActionCopy       ActionKind = "copy"
	ActionInspect    ActionKind = "inspect"
	ActionSearch     ActionKind = "search"
	ActionCommand    ActionKind = "command"
	ActionOpenEditor ActionKind = "open-editor"
)

type Action struct {
	Kind ActionKind
	Item Item
}

type Item struct {
	ID           string
	Name         string
	Type         string
	AutoRenew    bool
	LastAccessed *time.Time
	Modified     *time.Time
	Current      bool
	Damaged      bool
	Missing      bool
}

type View struct {
	List          common.ListView
	Items         []Item
	WorkspaceName string
	Message       string
	Editor        EditorView
}

func NewView() *View {
	return &View{
		List: common.ListView{
			State: common.ListState{
				Title:     "Auths",
				EmptyText: "No auths found",
				Hints:     "j/k or up/down move  g/G top/bottom  enter/e edit  E editor  i inspect  c create  d delete  y copy  / search  : command",
			},
		},
	}
}

func (v *View) OpenEditor(mode EditorMode, draft Draft) {
	v.Editor.Open(mode, draft)
}

func (v *View) CloseEditor() {
	v.Editor.Close()
}

func (v *View) EditorDraft() MutationDraft {
	return v.Editor.Snapshot()
}

func (v *View) EditorSubmission() Submission {
	return v.Editor.Submit()
}

func (v *View) SetItems(items []Item) {
	v.Items = append(v.Items[:0], items...)
	v.List.SetRows(toRows(v.Items))
	if v.List.State.Cursor >= len(v.Items) {
		v.List.State.Cursor = len(v.Items) - 1
	}
}

func (v *View) HandleKey(key Key) Action {
	if v.Editor.Active {
		editorAction := v.Editor.HandleKey(key)
		switch editorAction.Kind {
		case EditorActionSubmit:
			v.Editor.Active = false
			return Action{Kind: ActionOpenEditor, Item: v.currentItem()}
		case EditorActionCancel:
			v.Editor.Active = false
			return Action{Kind: ActionNone, Item: v.currentItem()}
		case EditorActionMove:
			return Action{Kind: ActionNone, Item: v.currentItem()}
		default:
			return Action{Kind: ActionNone, Item: v.currentItem()}
		}
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
	case KeyEnter, KeyEdit:
		return Action{Kind: ActionEdit, Item: v.currentItem()}
	case KeyEditAlt:
		v.Editor.Active = true
		v.Editor.Message = ""
		return Action{Kind: ActionOpenEditor, Item: v.currentItem()}
	case KeyInspect:
		return Action{Kind: ActionInspect, Item: v.currentItem()}
	case KeyCreate:
		return Action{Kind: ActionCreate}
	case KeyDelete:
		return Action{Kind: ActionDelete, Item: v.currentItem()}
	case KeyCopy:
		return Action{Kind: ActionCopy, Item: v.currentItem()}
	case KeySearch:
		return Action{Kind: ActionSearch}
	case KeyCommand:
		return Action{Kind: ActionCommand}
	default:
		return Action{Kind: ActionNone}
	}
}

func (v *View) Render() string {
	state := v.List.State
	state.Message = v.messageLine()
	out := common.Render(state, toRows(v.Items))

	if v.Editor.Active {
		if out != "" {
			out += "\n\n"
		}
		out += v.Editor.Render()
	}

	return out
}

func (v *View) currentItem() Item {
	if len(v.Items) == 0 || v.List.State.Cursor < 0 || v.List.State.Cursor >= len(v.Items) {
		return Item{}
	}

	return v.Items[v.List.State.Cursor]
}

func (v *View) messageLine() string {
	if v.Message != "" {
		return v.Message
	}

	if v.WorkspaceName == "" {
		return "No workspace selected"
	}

	return fmt.Sprintf("Workspace: %s", v.WorkspaceName)
}

func toRows(items []Item) []common.Row {
	rows := make([]common.Row, 0, len(items))
	for _, item := range items {
		rows = append(rows, common.Row{
			Key:     item.ID,
			Title:   displayName(item.Name, item.ID),
			Summary: authSummary(item),
			Details: authDetails(item),
			Current: item.Current,
			Damaged: item.Damaged,
			Missing: item.Missing,
		})
	}
	return rows
}

func authSummary(item Item) string {
	if item.Type == "" {
		return item.ID
	}

	return item.Type
}

func authDetails(item Item) []string {
	details := []string{fmt.Sprintf("auto renew: %s", boolText(item.AutoRenew))}
	if item.Modified != nil {
		details = append(details, fmt.Sprintf("modified: %s", item.Modified.Format("2006-01-02 15:04:05")))
	}
	if item.LastAccessed != nil {
		details = append(details, fmt.Sprintf("last accessed: %s", item.LastAccessed.Format("2006-01-02 15:04:05")))
	}
	return details
}

func displayName(name, fallback string) string {
	if strings.TrimSpace(name) != "" {
		return name
	}
	return fallback
}

func boolText(value bool) string {
	if value {
		return "on"
	}
	return "off"
}
