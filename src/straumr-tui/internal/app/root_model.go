package app

import (
	"context"
	"encoding/json"
	"fmt"
	"strings"
	"time"

	"straumr-tui/internal/cache"
	"straumr-tui/internal/cli"
	"straumr-tui/internal/state"
	"straumr-tui/internal/ui"
	"straumr-tui/internal/views/auth"
	"straumr-tui/internal/views/request"
	"straumr-tui/internal/views/secret"
	"straumr-tui/internal/views/send"
	"straumr-tui/internal/views/workspace"

	tea "github.com/charmbracelet/bubbletea"
)

type Model struct {
	ctx           context.Context
	client        *cli.Client
	cache         *cache.Store
	session       state.Session
	workspaceView *workspace.View
	requestView   *request.View
	authView      *auth.View
	secretView    *secret.View
	sendView      *send.View
}

func NewModel(ctx context.Context, client *cli.Client, store *cache.Store) *Model {
	m := &Model{
		ctx:           ctx,
		client:        client,
		cache:         store,
		workspaceView: workspace.NewView(),
		requestView:   request.NewView(),
		authView:      auth.NewView(),
		secretView:    secret.NewView(),
		sendView:      send.NewView(),
		session: state.Session{
			Screen: state.ScreenWorkspaces,
		},
	}
	return m
}

func (m *Model) Init() tea.Cmd {
	return bootstrapCmd(m.ctx, m.client)
}

func (m *Model) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	switch msg := msg.(type) {
	case tea.WindowSizeMsg:
		m.session.Width = msg.Width
		m.session.Height = msg.Height
		m.workspaceView.List.State.Width = msg.Width
		m.workspaceView.List.State.Height = msg.Height
		m.requestView.List.State.Width = msg.Width
		m.requestView.List.State.Height = msg.Height
		m.authView.List.State.Width = msg.Width
		m.authView.List.State.Height = msg.Height
		m.secretView.List.State.Width = msg.Width
		m.secretView.List.State.Height = msg.Height
		return m, nil
	case tea.KeyMsg:
		return m.updateKey(msg)
	case bootstrapMsg:
		return m.applyBootstrap(msg)
	case workspacesLoadedMsg:
		return m.applyWorkspaces(msg), nil
	case requestsLoadedMsg:
		return m.applyRequests(msg), nil
	case authsLoadedMsg:
		return m.applyAuths(msg), nil
	case secretsLoadedMsg:
		return m.applySecrets(msg), nil
	case sendLoadedMsg:
		return m.applySend(msg), nil
	case cliErrorMsg:
		m.session.Busy = false
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Scope + ": " + msg.Err.Error()
		return m, nil
	case statusMsg:
		m.session.Message = string(msg)
		m.session.Error = ""
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) View() string {
	body := m.renderActiveScreen()
	return ui.RenderShell(m.session, body)
}

func (m *Model) updateKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if global, ok := ui.Global(msg); ok {
		switch global {
		case ui.GlobalQuit:
			return m, tea.Quit
		case ui.GlobalNext:
			m.session.Screen = nextScreen(m.session.Screen, 1)
			return m, m.refreshForActiveScreen()
		case ui.GlobalPrev:
			m.session.Screen = nextScreen(m.session.Screen, -1)
			return m, m.refreshForActiveScreen()
		case ui.GlobalRefresh:
			return m, m.refreshForActiveScreen()
		}
	}

	switch m.session.Screen {
	case state.ScreenWorkspaces:
		if key, ok := ui.WorkspaceKey(msg); ok {
			return m.handleWorkspaceKey(key)
		}
	case state.ScreenRequests:
		if key, ok := ui.RequestKey(msg); ok {
			return m.handleRequestKey(key)
		}
	case state.ScreenAuths:
		if key, ok := ui.AuthKey(msg); ok {
			return m.handleAuthKey(key)
		}
	case state.ScreenSecrets:
		if key, ok := ui.SecretKey(msg); ok {
			return m.handleSecretKey(key)
		}
	case state.ScreenSend:
		if key, ok := ui.SendKey(msg); ok {
			return m.handleSendKey(key)
		}
	default:
		return m, nil
	}

	return m, nil
}

func (m *Model) handleWorkspaceKey(key workspace.Key) (tea.Model, tea.Cmd) {
	action := m.workspaceView.HandleKey(key)
	switch action.Kind {
	case workspace.ActionMoveCursor, workspace.ActionOpenMenu, workspace.ActionCloseMenu:
		return m, nil
	case workspace.ActionOpenTarget:
		if action.Item.ID != "" {
			m.session.SetWorkspace(action.Item.ID, action.Item.Name)
		}
		m.session.Screen = screenFromWorkspaceTarget(action.Target)
		if action.Target == workspace.TargetActivate && action.Item.ID != "" {
			m.session.SetWorkspace(action.Item.ID, action.Item.Name)
			return m, refreshRequestsCmd(m.ctx, m.client, action.Item.ID, action.Item.Name)
		}
		return m, m.refreshForActiveScreen()
	case workspace.ActionSetActive:
		if action.Item.ID != "" {
			m.session.SetWorkspace(action.Item.ID, action.Item.Name)
			m.session.Screen = state.ScreenRequests
			return m, refreshRequestsCmd(m.ctx, m.client, action.Item.ID, action.Item.Name)
		}
		return m, nil
	case workspace.ActionCreate, workspace.ActionDelete, workspace.ActionEdit, workspace.ActionCopy, workspace.ActionImport, workspace.ActionExport, workspace.ActionSearch, workspace.ActionCommand, workspace.ActionInspect:
		m.session.Message = workspaceActionMessage(action.Kind, action.Item)
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleRequestKey(key request.Key) (tea.Model, tea.Cmd) {
	action := m.requestView.HandleKey(key)
	switch action.Kind {
	case request.ActionMoveCursor, request.ActionInspect, request.ActionSearch, request.ActionCommand:
		return m, nil
	case request.ActionSend:
		m.session.SetRequest(
			action.Item.ID,
			action.Item.Name,
			action.Item.Method,
			action.Item.Host,
			m.requestView.WorkspaceID,
			m.requestView.WorkspaceName,
		)
		m.sendView.SetRequest(send.Request{
			Name:   action.Item.Name,
			Method: action.Item.Method,
			URI:    action.Item.Host,
			Auth:   action.Item.Auth,
		})
		m.sendView.SetStatus("Sending...")
		m.sendView.SetResponse(send.Response{})
		m.session.Screen = state.ScreenSend
		if m.session.ActiveRequest != nil {
			m.session.Busy = true
			return m, sendCmd(m.ctx, m.client, *m.session.ActiveRequest)
		}
		return m, nil
	case request.ActionEdit, request.ActionEditAlt:
		m.requestView.Editor.Active = true
		m.requestView.Editor.Message = requestActionMessage(action.Kind, action.Item)
		return m, nil
	case request.ActionCopy, request.ActionCreate, request.ActionDelete:
		m.session.Message = requestActionMessage(action.Kind, action.Item)
		return m, nil
	case request.ActionOpenEditor:
		m.requestView.Editor.Active = false
		m.session.Message = "Request editor submit is not wired yet"
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleAuthKey(key auth.Key) (tea.Model, tea.Cmd) {
	action := m.authView.HandleKey(key)
	switch action.Kind {
	case auth.ActionMoveCursor, auth.ActionInspect, auth.ActionSearch, auth.ActionCommand:
		return m, nil
	case auth.ActionEdit, auth.ActionOpenEditor:
		m.authView.Editor.Active = true
		m.authView.Message = authActionMessage(action.Kind, action.Item)
		return m, nil
	case auth.ActionCreate, auth.ActionDelete, auth.ActionCopy:
		m.session.Message = authActionMessage(action.Kind, action.Item)
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleSecretKey(key secret.Key) (tea.Model, tea.Cmd) {
	action := m.secretView.HandleKey(key)
	switch action.Kind {
	case secret.ActionMoveCursor, secret.ActionInspect, secret.ActionSearch, secret.ActionCommand:
		return m, nil
	case secret.ActionEdit, secret.ActionEditor:
		m.secretView.Editor.Active = true
		m.secretView.Message = secretActionMessage(action.Kind, action.Item)
		return m, nil
	case secret.ActionCreate, secret.ActionDelete, secret.ActionCopy:
		m.session.Message = secretActionMessage(action.Kind, action.Item)
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleSendKey(key send.Key) (tea.Model, tea.Cmd) {
	action := m.sendView.HandleKey(key)
	switch action.Kind {
	case send.ActionCancel:
		m.session.Screen = state.ScreenRequests
		return m, nil
	case send.ActionSwitchPane, send.ActionScroll:
		return m, nil
	case send.ActionBeautify, send.ActionRevert, send.ActionCopyPane, send.ActionCopyTemplate, send.ActionSaveBody, send.ActionExport, send.ActionSend:
		m.session.Message = sendActionMessage(action.Kind)
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) applyBootstrap(msg bootstrapMsg) (tea.Model, tea.Cmd) {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		return m, nil
	}

	m.workspaceView.SetItems(mapWorkspaces(msg.Workspaces))
	if msg.Active != nil {
		m.session.SetWorkspace(msg.Active.ID, msg.Active.Name)
		m.requestView.WorkspaceID = msg.Active.ID
		m.requestView.WorkspaceName = msg.Active.Name
		m.session.Screen = state.ScreenRequests
		m.requestView.SetItems(mapRequests(msg.Requests))
		m.session.Busy = false
		return m, nil
	}

	m.requestView.WorkspaceID = ""
	m.requestView.WorkspaceName = ""
	m.requestView.SetItems(nil)
	m.session.Screen = state.ScreenWorkspaces
	m.session.Busy = false
	return m, nil
}

func (m *Model) applyWorkspaces(msg workspacesLoadedMsg) *Model {
	m.workspaceView.SetItems(mapWorkspaces(msg.Workspaces))
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
	}
	return m
}

func (m *Model) applyRequests(msg requestsLoadedMsg) *Model {
	m.requestView.WorkspaceID = msg.Workspace.ID
	m.requestView.WorkspaceName = msg.Workspace.Name
	m.requestView.SetItems(mapRequests(msg.Requests))
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
	}
	return m
}

func (m *Model) applyAuths(msg authsLoadedMsg) *Model {
	m.authView.WorkspaceName = msg.Workspace.Name
	m.authView.SetItems(mapAuths(msg.Auths))
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
	}
	return m
}

func (m *Model) applySecrets(msg secretsLoadedMsg) *Model {
	m.secretView.SetItems(mapSecrets(msg.Secrets))
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
	}
	return m
}

func (m *Model) applySend(msg sendLoadedMsg) *Model {
	m.session.Busy = false
	m.sendView.SetRequest(send.Request{
		Name:   msg.Request.Name,
		Method: msg.Request.Method,
		URI:    msg.Request.URI,
	})

	if msg.Err != nil {
		m.sendView.SetStatus("Send failed")
		m.sendView.SetError(msg.Err.Error())
		m.session.Error = msg.Err.Error()
		return m
	}

	statusText := "Completed"
	if msg.Result.Status != nil {
		statusText = fmt.Sprintf("%d %s", *msg.Result.Status, strings.TrimSpace(msg.Result.Reason))
	}

	m.sendView.SetStatus(statusText)
	m.sendView.SetResponse(send.Response{
		StatusText:  statusText,
		StatusCode:  msg.Result.Status,
		Duration:    durationFromMs(msg.Result.DurationMs),
		HTTPVersion: msg.Result.Version,
		Summary:     summarizeHeaders(msg.Result.Headers),
		Body:        formatBody(msg.Result.Body),
		RawBody:     formatBody(msg.Result.Body),
	})
	m.session.Error = ""
	return m
}

func (m *Model) renderActiveScreen() string {
	switch m.session.Screen {
	case state.ScreenWorkspaces:
		return m.workspaceView.Render()
	case state.ScreenRequests:
		return m.requestView.Render()
	case state.ScreenAuths:
		return m.authView.Render()
	case state.ScreenSecrets:
		return m.secretView.Render()
	case state.ScreenSend:
		return m.sendView.Render()
	default:
		return placeholderScreen("Straumr", m.session.ActiveWorkspace, "")
	}
}

func placeholderScreen(title string, active *state.WorkspaceRef, note string) string {
	var b strings.Builder
	b.WriteString(title)
	b.WriteString("\n")
	if active != nil && active.Name != "" {
		b.WriteString(fmt.Sprintf("Workspace: %s\n", active.Name))
	}
	if note != "" {
		b.WriteString(note)
	}
	if note == "" {
		b.WriteString("Not implemented yet")
	}
	return strings.TrimRight(b.String(), "\n")
}

func mapWorkspaces(items []cli.WorkspaceSummary) []workspace.Item {
	out := make([]workspace.Item, 0, len(items))
	for _, item := range items {
		damaged, missing := statusFlags(item.Status)
		out = append(out, workspace.Item{
			ID:           item.ID,
			Name:         item.Name,
			Path:         item.Path,
			Requests:     item.Requests,
			Secrets:      nil,
			Auths:        nil,
			LastAccessed: item.LastAccessed,
			Current:      item.IsCurrent,
			Damaged:      damaged,
			Missing:      missing,
		})
	}
	return out
}

func mapRequests(items []cli.RequestSummary) []request.Item {
	out := make([]request.Item, 0, len(items))
	for _, item := range items {
		damaged, missing := statusFlags(item.Status)
		out = append(out, request.Item{
			ID:           item.ID,
			Name:         item.Name,
			Method:       item.Method,
			Host:         item.URI,
			BodyType:     "",
			Auth:         "",
			LastAccessed: item.LastAccessed,
			Current:      false,
			Damaged:      damaged,
			Missing:      missing,
		})
	}
	return out
}

func mapAuths(items []cli.AuthSummary) []auth.Item {
	out := make([]auth.Item, 0, len(items))
	for _, item := range items {
		out = append(out, auth.Item{
			ID:   item.ID,
			Name: item.Name,
			Type: item.Type,
		})
	}
	return out
}

func mapSecrets(items []cli.SecretSummary) []secret.Item {
	out := make([]secret.Item, 0, len(items))
	for _, item := range items {
		damaged, missing := statusFlags(item.Status)
		out = append(out, secret.Item{
			ID:          item.ID,
			Name:        item.Name,
			Status:      item.Status,
			ValueMasked: maskedSecretValue(item.Status),
			Damaged:     damaged,
			Missing:     missing,
		})
	}
	return out
}

func workspaceActionMessage(kind workspace.ActionKind, item workspace.Item) string {
	if item.ID == "" {
		return fmt.Sprintf("%s requested", kind)
	}
	return fmt.Sprintf("%s requested for %s", kind, item.Name)
}

func requestActionMessage(kind request.ActionKind, item request.Item) string {
	if item.ID == "" {
		return fmt.Sprintf("%s requested", kind)
	}
	return fmt.Sprintf("%s requested for %s", kind, item.Name)
}

func authActionMessage(kind auth.ActionKind, item auth.Item) string {
	if item.ID == "" {
		return fmt.Sprintf("%s requested", kind)
	}
	return fmt.Sprintf("%s requested for %s", kind, item.Name)
}

func secretActionMessage(kind secret.ActionKind, item secret.Item) string {
	if item.ID == "" {
		return fmt.Sprintf("%s requested", kind)
	}
	return fmt.Sprintf("%s requested for %s", kind, item.Name)
}

func sendActionMessage(kind send.ActionKind) string {
	return fmt.Sprintf("%s requested", kind)
}

func (m *Model) refreshForActiveScreen() tea.Cmd {
	if cmd := refreshCmd(m.ctx, m.client, m.session); cmd != nil {
		m.session.Busy = true
		return cmd
	}

	m.session.Busy = false
	if m.session.Screen == state.ScreenRequests || m.session.Screen == state.ScreenAuths {
		m.session.Message = "No active workspace selected"
	} else {
		m.session.Message = "Nothing to refresh"
	}
	return nil
}

func intPtr(value int) *int {
	v := value
	return &v
}

func statusFlags(status string) (damaged bool, missing bool) {
	status = strings.ToLower(status)
	return strings.Contains(status, "corrupt") || strings.Contains(status, "damaged"),
		strings.Contains(status, "missing")
}

func maskedSecretValue(status string) string {
	if status == "" {
		return ""
	}
	return "••••••"
}

func durationFromMs(ms float64) time.Duration {
	return time.Duration(ms * float64(time.Millisecond))
}

func summarizeHeaders(headers map[string][]string) string {
	if len(headers) == 0 {
		return "(no headers)"
	}

	lines := make([]string, 0, len(headers))
	for key, values := range headers {
		lines = append(lines, fmt.Sprintf("%s: %s", key, strings.Join(values, ", ")))
	}
	return strings.Join(lines, "\n")
}

func formatBody(body any) string {
	if body == nil {
		return ""
	}

	switch v := body.(type) {
	case string:
		return v
	default:
		data, err := json.MarshalIndent(v, "", "  ")
		if err != nil {
			return fmt.Sprint(v)
		}
		return string(data)
	}
}
