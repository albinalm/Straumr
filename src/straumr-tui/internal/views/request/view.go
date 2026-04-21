package request

import (
	"fmt"
	"strings"
	"time"

	"straumr-tui/internal/views/common"
)

// Assumption: the app shell resolves request-scoped key events into these
// simple tokens before they reach the view layer. This lets the request views
// stay independent from tea.Msg and the CLI client packages.
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
	KeySend    Key = "send"
	KeyCreate  Key = "create"
	KeyDelete  Key = "delete"
	KeyEdit    Key = "edit"
	KeyEditAlt Key = "edit-alt"
	KeyCopy    Key = "copy"
	KeyCancel  Key = "cancel"
)

type ActionKind string

const (
	ActionNone       ActionKind = ""
	ActionMoveCursor ActionKind = "move-cursor"
	ActionSubmit     ActionKind = "submit"
	ActionCancel     ActionKind = "cancel"
	ActionSend       ActionKind = "send"
	ActionCreate     ActionKind = "create"
	ActionDelete     ActionKind = "delete"
	ActionEdit       ActionKind = "edit"
	ActionEditAlt    ActionKind = "edit-alt"
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
	Method       string
	Host         string
	BodyType     string
	Auth         string
	LastAccessed *time.Time
	Current      bool
	Damaged      bool
	Missing      bool
}

type View struct {
	List          common.ListView
	Items         []Item
	WorkspaceName string
	WorkspaceID   string
	target        Item
	Message       string
	Editor        EditorView
}

func NewView() *View {
	return &View{
		List: common.ListView{
			State: common.ListState{
				Title:     "Requests",
				EmptyText: "No requests found",
				Hints:     "j/k or up/down move  g/G top/bottom  enter or s send  i inspect  c create  d delete  e edit  E editor  y copy  / search  : command",
			},
		},
	}
}

func (v *View) OpenEditor(mode EditorMode, draft Draft) {
	target := Item{}
	if mode == EditorModeEdit {
		target = v.currentItem()
	}
	v.OpenEditorForItem(mode, target, draft)
}

func (v *View) OpenCreateEditor(draft Draft) {
	v.OpenEditorForItem(EditorModeCreate, Item{}, draft)
}

func (v *View) OpenEditEditor(target Item, draft Draft) {
	v.OpenEditorForItem(EditorModeEdit, target, draft)
}

func (v *View) OpenEditorForItem(mode EditorMode, target Item, draft Draft) {
	v.target = target
	v.Editor.Open(mode, draft)
}

func (v *View) CloseEditor() {
	v.target = Item{}
	v.Editor.Close()
}

func (v *View) SetEditorDraft(draft Draft) {
	v.Editor.SetDraft(draft)
}

func (v *View) EditorDraft() MutationDraft {
	return v.Editor.Snapshot()
}

func (v *View) EditorSubmission() Submission {
	return NewSubmission(v.Editor.Mode, v.target, v.Editor.Draft)
}

func (v *View) CurrentItem() Item {
	return v.currentItem()
}

func (v *View) HandleEditorKey(key Key) Action {
	return v.handleEditorKey(key)
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
	state.Title = requestTitle(v.WorkspaceName)
	state.Message = v.messageLine()
	state.Footer = fmt.Sprintf("%d/%d requests", len(v.Items), len(v.Items))
	out := common.Render(state, toRows(v.Items))

	if v.Editor.Active {
		if out != "" {
			out += "\n\n"
		}
		out += v.Editor.Render()
	}

	return out
}

func (v *View) HandleKey(key Key) Action {
	if v.Editor.Active {
		return v.handleEditorKey(key)
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
	case KeyEnter, KeySend:
		return Action{Kind: ActionSend, Item: v.currentItem()}
	case KeyInspect:
		return Action{Kind: ActionInspect, Item: v.currentItem()}
	case KeyCreate:
		return Action{Kind: ActionCreate}
	case KeyDelete:
		return Action{Kind: ActionDelete, Item: v.currentItem()}
	case KeyEdit:
		return Action{Kind: ActionEdit, Item: v.currentItem()}
	case KeyEditAlt:
		return Action{Kind: ActionEditAlt, Item: v.currentItem()}
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

func (v *View) handleEditorKey(key Key) Action {
	action := v.Editor.HandleKey(key)
	switch action.Kind {
	case EditorActionMove:
		return Action{Kind: ActionMoveCursor, Item: v.currentItem()}
	case EditorActionSubmit:
		v.Editor.Active = false
		return Action{Kind: ActionSubmit, Item: v.target}
	case EditorActionCancel:
		v.Editor.Active = false
		return Action{Kind: ActionCancel, Item: v.target}
	default:
		return Action{Kind: ActionNone, Item: v.currentItem()}
	}
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

	return ""
}

func toRows(items []Item) []common.Row {
	rows := make([]common.Row, 0, len(items))
	for _, item := range items {
		rows = append(rows, common.Row{
			Key:     item.ID,
			Title:   requestName(item),
			Summary: requestSummary(item),
			Details: requestDetails(item),
			Current: item.Current,
			Damaged: item.Damaged,
			Missing: item.Missing,
		})
	}
	return rows
}

func requestSummary(item Item) string {
	if item.ID == "" {
		return item.ID
	}

	return item.ID
}

func requestDetails(item Item) []string {
	details := []string{requestMetaLine(item)}
	if item.LastAccessed != nil {
		details[0] += " • " + item.LastAccessed.Format("2006-01-02")
	}
	return details
}

func displayName(name, fallback string) string {
	if strings.TrimSpace(name) != "" {
		return name
	}
	return fallback
}

func requestName(item Item) string {
	name := displayName(item.Name, item.ID)
	if method := strings.TrimSpace(item.Method); method != "" {
		return strings.ToUpper(method) + " " + name
	}
	return name
}

func requestTitle(workspaceName string) string {
	if strings.TrimSpace(workspaceName) == "" {
		return "Requests"
	}
	return fmt.Sprintf("Requests - %s", workspaceName)
}

func requestMetaLine(item Item) string {
	parts := []string{}
	if strings.TrimSpace(item.Host) != "" {
		parts = append(parts, item.Host)
	}
	if strings.TrimSpace(item.BodyType) != "" {
		parts = append(parts, item.BodyType)
	} else {
		parts = append(parts, "No body")
	}
	if strings.TrimSpace(item.Auth) != "" {
		parts = append(parts, item.Auth)
	} else {
		parts = append(parts, "No auth")
	}

	if len(parts) == 0 {
		return item.ID
	}

	return strings.Join(parts, " • ")
}
