package app

import (
	"context"
	"encoding/json"
	"fmt"
	"sort"
	"strings"
	"time"

	"straumr-tui/internal/cache"
	"straumr-tui/internal/cli"
	"straumr-tui/internal/state"
	"straumr-tui/internal/ui"
	"straumr-tui/internal/views/auth"
	"straumr-tui/internal/views/dialogs"
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
	textInput     dialogs.TextInputView
	secretInput   dialogs.SecretInputView
	confirm       dialogs.ConfirmView
	selectView    dialogs.SelectView
	keyValue      dialogs.KeyValueEditorView
	pathPicker    dialogs.PathPickerView
	lastDirs      map[pendingFlow]string
	pending       *pendingAction
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
		lastDirs:      make(map[pendingFlow]string),
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
	case dryRunLoadedMsg:
		return m.applyDryRun(msg), nil
	case requestEditorSeedMsg:
		return m.applyRequestEditorSeed(msg)
	case requestInspectLoadedMsg:
		return m.applyRequestInspect(msg), nil
	case requestPickerChoicesLoadedMsg:
		return m.applyRequestPickerChoices(msg), nil
	case workspaceInspectLoadedMsg:
		return m.applyWorkspaceInspect(msg), nil
	case authEditorSeedMsg:
		return m.applyAuthEditorSeed(msg)
	case authInspectLoadedMsg:
		return m.applyAuthInspect(msg), nil
	case mutationCompletedMsg:
		return m.applyMutation(msg)
	case shellActionCompletedMsg:
		return m.applyShellAction(msg), nil
	case secretEditorSeedMsg:
		return m.applySecretEditorSeed(msg)
	case secretInspectLoadedMsg:
		return m.applySecretInspect(msg), nil
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
	if overlay := m.overlayView(); overlay != "" {
		body = strings.TrimRight(body, "\n")
		if body != "" {
			body += "\n\n"
		}
		body += overlay
	}
	return ui.RenderShell(m.session, body)
}

func (m *Model) updateKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if m.hasOverlay() {
		return m.handleOverlayKey(msg)
	}

	if m.session.Screen == state.ScreenSend {
		if key, ok := ui.SendKey(msg); ok && (key == send.KeyTab || key == send.KeyShiftTab) {
			return m.handleSendKey(key)
		}
	}

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
	case workspace.ActionCreate:
		m.openTextFlow(flowWorkspaceCreateName, "Create workspace", "Name", "", "workspace name", "Enter the new workspace name", pendingAction{})
		return m, nil
	case workspace.ActionEdit:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openTextFlow(flowWorkspaceEditName, "Rename workspace", "Name", action.Item.Name, "workspace name", "Rename the selected workspace", pendingAction{
			Identifier: action.Item.ID,
			Name:       action.Item.Name,
		})
		return m, nil
	case workspace.ActionCopy:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openTextFlow(flowWorkspaceCopyName, "Copy workspace", "New name", action.Item.Name+"-copy", "workspace name", "Enter the name for the copied workspace", pendingAction{
			Identifier: action.Item.ID,
			Name:       action.Item.Name,
		})
		return m, nil
	case workspace.ActionDelete:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openConfirmFlow(flowWorkspaceDeleteConfirm, "Delete workspace", fmt.Sprintf("Delete %s?", action.Item.Name), []string{"Cancel", "Delete"}, pendingAction{
			Identifier: action.Item.ID,
			Name:       action.Item.Name,
		})
		return m, nil
	case workspace.ActionInspect:
		if action.Item.ID == "" {
			return m, nil
		}
		m.session.Busy = true
		return m, inspectWorkspaceCmd(m.ctx, m.client, action.Item.ID)
	case workspace.ActionImport:
		m.openPathFlow(flowWorkspaceImportPath, "Import workspace", "Type or browse to an existing workspace file. Enter imports the file, l opens directories.", "", pendingAction{
			PathMode:      dialogs.PathModeOpen,
			PathMustExist: true,
		})
		return m, nil
	case workspace.ActionExport:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openPathFlow(flowWorkspaceExportPath, "Export workspace", "Choose an existing output directory. Tab to the path field to accept the current directory.", "", pendingAction{
			Identifier:    action.Item.ID,
			Name:          action.Item.Name,
			PathMode:      dialogs.PathModeOpen,
			PathMustExist: true,
			PathDirectory: true,
		})
		return m, nil
	case workspace.ActionSearch, workspace.ActionCommand:
		m.session.Message = workspaceActionMessage(action.Kind, action.Item)
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleRequestKey(key request.Key) (tea.Model, tea.Cmd) {
	action := m.requestView.HandleKey(key)
	switch action.Kind {
	case request.ActionMoveCursor, request.ActionSearch, request.ActionCommand:
		return m, nil
	case request.ActionInspect:
		if action.Item.ID == "" || m.requestView.WorkspaceID == "" {
			return m, nil
		}
		m.session.Busy = true
		return m, inspectRequestCmd(m.ctx, m.client, m.requestView.WorkspaceID, action.Item.ID)
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
		if action.Item.ID == "" || m.requestView.WorkspaceID == "" {
			return m, nil
		}
		m.session.Busy = true
		return m, seedRequestEditCmd(m.ctx, m.client, m.requestView.WorkspaceID, action.Item.ID, flowRequestEditLoad)
	case request.ActionCopy:
		if action.Item.ID == "" || m.requestView.WorkspaceID == "" {
			return m, nil
		}
		m.openTextFlow(flowRequestCopyName, "Copy request", "New name", action.Item.Name+"-copy", "request name", "Enter the name for the copied request", pendingAction{
			Identifier:    action.Item.ID,
			Name:          action.Item.Name,
			WorkspaceID:   m.requestView.WorkspaceID,
			WorkspaceName: m.requestView.WorkspaceName,
		})
		return m, nil
	case request.ActionDelete:
		if action.Item.ID == "" || m.requestView.WorkspaceID == "" {
			return m, nil
		}
		m.openConfirmFlow(flowRequestDeleteConfirm, "Delete request", fmt.Sprintf("Delete %s?", action.Item.Name), []string{"Cancel", "Delete"}, pendingAction{
			Identifier:    action.Item.ID,
			Name:          action.Item.Name,
			WorkspaceID:   m.requestView.WorkspaceID,
			WorkspaceName: m.requestView.WorkspaceName,
		})
		return m, nil
	case request.ActionCreate:
		if m.requestView.WorkspaceID == "" {
			m.session.Message = "No workspace selected"
			return m, nil
		}
		m.openTextFlow(flowRequestCreateName, "Create request", "Name", "", "request name", "Enter the new request name", pendingAction{
			WorkspaceID:   m.requestView.WorkspaceID,
			WorkspaceName: m.requestView.WorkspaceName,
			RequestDraft:  request.NewDraft().WithMethod("GET"),
		})
		return m, nil
	case request.ActionSubmit:
		if m.requestView.WorkspaceID == "" {
			m.requestView.CloseEditor()
			m.session.Message = "No workspace selected"
			return m, nil
		}
		submission := m.requestView.EditorSubmission()
		m.requestView.CloseEditor()
		m.session.Busy = true
		if submission.Mode == request.EditorModeEdit && submission.Item.ID != "" {
			return m, editRequestCmd(m.ctx, m.client, m.requestView.WorkspaceID, submission.Item.ID, submission.Draft)
		}
		return m, createRequestCmd(m.ctx, m.client, m.requestView.WorkspaceID, submission.Draft)
	case request.ActionCancel:
		m.requestView.CloseEditor()
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleAuthKey(key auth.Key) (tea.Model, tea.Cmd) {
	action := m.authView.HandleKey(key)
	switch action.Kind {
	case auth.ActionMoveCursor, auth.ActionSearch, auth.ActionCommand:
		return m, nil
	case auth.ActionInspect:
		if action.Item.ID == "" || m.session.ActiveWorkspace == nil {
			return m, nil
		}
		m.session.Busy = true
		return m, inspectAuthCmd(m.ctx, m.client, m.session.ActiveWorkspace.ID, action.Item.ID)
	case auth.ActionEdit, auth.ActionOpenEditor:
		if action.Item.ID == "" || m.session.ActiveWorkspace == nil {
			return m, nil
		}
		m.session.Busy = true
		return m, seedAuthEditCmd(m.ctx, m.client, m.session.ActiveWorkspace.ID, action.Item.ID, flowAuthEditLoad)
	case auth.ActionCopy:
		if action.Item.ID == "" || m.session.ActiveWorkspace == nil {
			return m, nil
		}
		m.openTextFlow(flowAuthCopyName, "Copy auth", "New name", action.Item.Name+"-copy", "auth name", "Enter the name for the copied auth", pendingAction{
			Identifier:    action.Item.ID,
			Name:          action.Item.Name,
			WorkspaceID:   m.session.ActiveWorkspace.ID,
			WorkspaceName: m.session.ActiveWorkspace.Name,
		})
		return m, nil
	case auth.ActionDelete:
		if action.Item.ID == "" || m.session.ActiveWorkspace == nil {
			return m, nil
		}
		m.openConfirmFlow(flowAuthDeleteConfirm, "Delete auth", fmt.Sprintf("Delete %s?", action.Item.Name), []string{"Cancel", "Delete"}, pendingAction{
			Identifier:    action.Item.ID,
			Name:          action.Item.Name,
			WorkspaceID:   m.session.ActiveWorkspace.ID,
			WorkspaceName: m.session.ActiveWorkspace.Name,
		})
		return m, nil
	case auth.ActionCreate:
		if m.session.ActiveWorkspace == nil {
			m.session.Message = "No workspace selected"
			return m, nil
		}
		m.openTextFlow(flowAuthName, "Create auth", "Name", "", "auth name", "Enter the new auth name", pendingAction{
			WorkspaceID:   m.session.ActiveWorkspace.ID,
			WorkspaceName: m.session.ActiveWorkspace.Name,
			AuthMode:      auth.EditorModeCreate,
			AuthDraft: auth.Draft{
				AutoRenew: true,
			},
		})
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) handleSecretKey(key secret.Key) (tea.Model, tea.Cmd) {
	action := m.secretView.HandleKey(key)
	switch action.Kind {
	case secret.ActionMoveCursor, secret.ActionSearch, secret.ActionCommand:
		return m, nil
	case secret.ActionInspect:
		if action.Item.ID == "" {
			return m, nil
		}
		m.session.Busy = true
		return m, inspectSecretCmd(m.ctx, m.client, action.Item.ID)
	case secret.ActionEdit, secret.ActionEditor:
		if action.Item.ID == "" {
			return m, nil
		}
		m.session.Busy = true
		return m, seedSecretEditCmd(m.ctx, m.client, action.Item.ID, flowSecretEditLoad)
	case secret.ActionCreate:
		m.openTextFlow(flowSecretCreateName, "Create secret", "Name", "", "secret name", "Enter the new secret name", pendingAction{})
		return m, nil
	case secret.ActionCopy:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openTextFlow(flowSecretCopyName, "Copy secret", "New name", action.Item.Name+"-copy", "secret name", "Enter the name for the copied secret", pendingAction{
			Identifier: action.Item.ID,
			Name:       action.Item.Name,
		})
		return m, nil
	case secret.ActionDelete:
		if action.Item.ID == "" {
			return m, nil
		}
		m.openConfirmFlow(flowSecretDeleteConfirm, "Delete secret", fmt.Sprintf("Delete %s?", action.Item.Name), []string{"Cancel", "Delete"}, pendingAction{
			Identifier: action.Item.ID,
			Name:       action.Item.Name,
		})
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
	case send.ActionSend:
		if m.session.ActiveRequest == nil {
			return m, nil
		}
		m.session.Busy = true
		m.sendView.SetStatus("Sending...")
		return m, sendCmd(m.ctx, m.client, *m.session.ActiveRequest)
	case send.ActionDryRun:
		if m.session.ActiveRequest == nil {
			return m, nil
		}
		m.session.Busy = true
		m.sendView.SetStatus("Dry run...")
		return m, dryRunCmd(m.ctx, m.client, *m.session.ActiveRequest)
	case send.ActionSaveBody:
		path := defaultSendBodyPath(m.session.ActiveRequest, m.sendView.Response)
		m.openPathFlow(flowSendSavePath, "Save response body", "Type a path, use j/k to browse, h/l to move directories, Tab to complete, Enter to save.", path, pendingAction{
			OutputText: sendBodyText(m.sendView.Response),
		})
		return m, nil
	case send.ActionExport:
		path := defaultSendExportPath(m.session.ActiveRequest)
		m.openPathFlow(flowSendExportPath, "Export response", "Type a path, use j/k to browse, h/l to move directories, Tab to complete, Enter to export.", path, pendingAction{
			OutputText: formatSendExport(m.sendView.Request, m.sendView.Response),
		})
		return m, nil
	case send.ActionCopyPane:
		m.session.Busy = true
		return m, copyToClipboardCmd(sendPaneText(m.sendView), "Copied active pane to the clipboard")
	case send.ActionCopyTemplate:
		m.session.Busy = true
		return m, copyToClipboardCmd(formatSendExport(m.sendView.Request, m.sendView.Response), "Copied response export to the clipboard")
	case send.ActionBeautify, send.ActionRevert:
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

func (m *Model) applyDryRun(msg dryRunLoadedMsg) *Model {
	m.session.Busy = false
	m.sendView.SetRequest(send.Request{
		Name:   msg.Request.Name,
		Method: msg.Request.Method,
		URI:    msg.Request.URI,
	})

	if msg.Err != nil {
		m.sendView.SetStatus("Dry run failed")
		m.sendView.SetError(msg.Err.Error())
		m.session.Error = msg.Err.Error()
		return m
	}

	notes := []string{}
	if msg.Result.Auth != nil && strings.TrimSpace(*msg.Result.Auth) != "" {
		notes = append(notes, "Auth: "+strings.TrimSpace(*msg.Result.Auth))
	}
	if msg.Result.BodyType != "" {
		notes = append(notes, "Body type: "+msg.Result.BodyType)
	}

	body := ""
	if msg.Result.Body != nil {
		body = *msg.Result.Body
	}

	m.sendView.SetStatus("Dry run ready")
	m.sendView.SetResponse(send.Response{
		StatusText: "Preview only",
		Summary:    summarizeFlatHeaders(msg.Result.Headers),
		Body:       body,
		RawBody:    body,
		Notes:      notes,
	})
	m.session.Error = ""
	return m
}

func (m *Model) applyRequestEditorSeed(msg requestEditorSeedMsg) (tea.Model, tea.Cmd) {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m, nil
	}

	draft := request.NewDraft().
		WithName(msg.Item.Name).
		WithURL(msg.Item.Uri).
		WithMethod(msg.Item.Method)

	if msg.Item.Body != nil || msg.Item.BodyType != "" {
		body := ""
		if msg.Item.Body != nil {
			body = *msg.Item.Body
		}
		draft = draft.WithBody(msg.Item.BodyType, body)
	}
	if msg.Item.AuthID != nil {
		draft = draft.WithAuth(*msg.Item.AuthID)
	}
	for key, value := range msg.Item.Params {
		draft = draft.WithParam(key, value)
	}
	for key, value := range msg.Item.Headers {
		draft = draft.WithHeader(key, value)
	}

	m.openTextFlow(flowRequestEditName, "Edit request", "Name", draft.Name, "request name", "Update the request name", pendingAction{
		Identifier:    msg.Item.ID,
		Name:          msg.Item.Name,
		WorkspaceID:   m.requestView.WorkspaceID,
		WorkspaceName: m.requestView.WorkspaceName,
		RequestDraft:  draft,
	})
	return m, nil
}

func (m *Model) applyRequestInspect(msg requestInspectLoadedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}

	item := m.requestView.CurrentItem()
	title := "Request details"
	if strings.TrimSpace(msg.Item.Name) != "" {
		title = "Request details: " + msg.Item.Name
	} else if item.ID != "" {
		title = "Request details: " + item.ID
	} else if msg.RequestID != "" {
		title = "Request details: " + msg.RequestID
	}

	m.openConfirmFlow(flowRequestInspect, title, formatRequestInspectMessage(msg.Item), []string{"Close"}, pendingAction{
		Identifier:    msg.RequestID,
		WorkspaceID:   m.requestView.WorkspaceID,
		WorkspaceName: m.requestView.WorkspaceName,
	})
	return m
}

func (m *Model) applyWorkspaceInspect(msg workspaceInspectLoadedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}

	title := "Workspace details"
	if strings.TrimSpace(msg.Item.Name) != "" {
		title = "Workspace details: " + msg.Item.Name
	}
	m.openConfirmFlow(flowWorkspaceInspect, title, formatWorkspaceInspectMessage(msg.Item), []string{"Close"}, pendingAction{})
	return m
}

func (m *Model) applyRequestPickerChoices(msg requestPickerChoicesLoadedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}
	if m.pending == nil {
		return m
	}

	pending := *m.pending
	items := make([]dialogs.Choice, 0, len(msg.Choices))
	for _, choice := range msg.Choices {
		items = append(items, dialogs.Choice{
			Key:         choice.Key,
			Title:       choice.Title,
			Description: choice.Description,
		})
	}

	switch msg.Flow {
	case flowRequestCreateAuthPick:
		m.openSelectFlow(flowRequestCreateAuthPick, "Create request", "Choose an auth binding", "Enter choose  Esc cancel", items, pending)
	case flowRequestEditAuthPick:
		m.openSelectFlow(flowRequestEditAuthPick, "Edit request", "Choose an auth binding", "Enter choose  Esc cancel", items, pending)
	}
	return m
}

func (m *Model) applyAuthEditorSeed(msg authEditorSeedMsg) (tea.Model, tea.Cmd) {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m, nil
	}

	draft, err := authDraftFromResult(msg.Item)
	if err != nil {
		m.session.Error = err.Error()
		m.session.Message = err.Error()
		return m, nil
	}

	identifier := msg.Item.ID
	if identifier == "" && len(m.authView.Items) > 0 {
		identifier = m.authView.Items[m.authView.List.State.Cursor].ID
	}

	m.openTextFlow(flowAuthName, "Edit auth", "Name", draft.Name, "auth name", "Update the auth name", pendingAction{
		Identifier:    identifier,
		WorkspaceID:   m.session.ActiveWorkspace.ID,
		WorkspaceName: m.session.ActiveWorkspace.Name,
		AuthMode:      auth.EditorModeEdit,
		AuthDraft:     draft,
	})
	return m, nil
}

func (m *Model) applyAuthInspect(msg authInspectLoadedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}

	title := "Auth details"
	if strings.TrimSpace(msg.Item.Name) != "" {
		title = "Auth details: " + msg.Item.Name
	}
	m.openConfirmFlow(flowAuthInspect, title, formatAuthInspectMessage(msg.Item), []string{"Close"}, pendingAction{})
	return m
}

func (m *Model) applyMutation(msg mutationCompletedMsg) (tea.Model, tea.Cmd) {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m, nil
	}

	m.session.Error = ""
	m.session.Message = msg.Message
	return m, m.refreshScreen(msg.Screen)
}

func (m *Model) applyShellAction(msg shellActionCompletedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}
	m.session.Error = ""
	m.session.Message = msg.Message
	return m
}

func (m *Model) applySecretEditorSeed(msg secretEditorSeedMsg) (tea.Model, tea.Cmd) {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m, nil
	}

	m.openTextFlow(flowSecretEditName, "Edit secret", "Name", msg.Item.Name, "secret name", "Update the secret name", pendingAction{
		Identifier: msg.Item.ID,
		Name:       msg.Item.Name,
		Value:      msg.Item.Value,
	})
	return m, nil
}

func (m *Model) applySecretInspect(msg secretInspectLoadedMsg) *Model {
	m.session.Busy = false
	if msg.Err != nil {
		m.session.Error = msg.Err.Error()
		m.session.Message = msg.Err.Error()
		return m
	}

	title := "Secret details"
	if strings.TrimSpace(msg.Item.Name) != "" {
		title = "Secret details: " + msg.Item.Name
	}
	m.openConfirmFlow(flowSecretInspect, title, formatSecretInspectMessage(msg.Item), []string{"Close"}, pendingAction{})
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
			Secrets:      item.Secrets,
			Auths:        item.Auths,
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
			BodyType:     item.BodyType,
			Auth:         item.Auth,
			LastAccessed: item.LastAccessed,
			Current:      item.Current,
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
			ID:           item.ID,
			Name:         item.Name,
			Type:         item.Type,
			AutoRenew:    item.AutoRenew,
			LastAccessed: item.LastAccessed,
			Current:      item.Current,
			Damaged:      item.Damaged,
			Missing:      item.Missing,
		})
	}
	return out
}

func mapSecrets(items []cli.SecretSummary) []secret.Item {
	out := make([]secret.Item, 0, len(items))
	for _, item := range items {
		damaged, missing := statusFlags(item.Status)
		out = append(out, secret.Item{
			ID:           item.ID,
			Name:         item.Name,
			Status:       item.Status,
			ValueMasked:  maskedSecretValue(item.Status),
			LastAccessed: item.LastAccessed,
			Current:      item.Current,
			Damaged:      damaged,
			Missing:      missing,
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

func formatRequestInspectMessage(item cli.RequestGetResult) string {
	lines := []string{
		"ID: " + fallbackText(item.ID, "(unknown)"),
		"Name: " + fallbackText(item.Name, "(unnamed)"),
		"Method: " + fallbackText(item.Method, "(empty)"),
		"URL: " + fallbackText(item.Uri, "(empty)"),
		"Body type: " + fallbackText(item.BodyType, "none"),
	}

	if item.AuthID != nil && strings.TrimSpace(*item.AuthID) != "" {
		lines = append(lines, "Auth ID: "+strings.TrimSpace(*item.AuthID))
	} else {
		lines = append(lines, "Auth ID: (none)")
	}

	lines = append(lines,
		"Headers: "+formatFlatMapLines(item.Headers),
		"Params: "+formatFlatMapLines(item.Params),
		"Last accessed: "+fallbackText(item.LastAccessed, "(unknown)"),
		"Modified: "+fallbackText(item.Modified, "(unknown)"),
	)

	body := "(empty)"
	if item.Body != nil && strings.TrimSpace(*item.Body) != "" {
		body = trimPreview(*item.Body, 400)
	}
	lines = append(lines, "Body preview:", body)

	return strings.Join(lines, "\n")
}

func formatWorkspaceInspectMessage(item cli.WorkspaceGetResult) string {
	return strings.Join([]string{
		"ID: " + fallbackText(item.ID, "(unknown)"),
		"Name: " + fallbackText(item.Name, "(unnamed)"),
		fmt.Sprintf("Requests: %d", len(item.Requests)),
		fmt.Sprintf("Auths: %d", len(item.Auths)),
		fmt.Sprintf("Secrets: %d", len(item.Secrets)),
		"Last accessed: " + item.LastAccessed.Format("2006-01-02 15:04:05"),
		"Modified: " + item.Modified.Format("2006-01-02 15:04:05"),
	}, "\n")
}

func formatAuthInspectMessage(item cli.AuthGetResult) string {
	typeLabel := "(unknown)"
	if draft, err := authDraftFromResult(item); err == nil {
		typeLabel = draft.Type
	}

	configPreview := "(unavailable)"
	if len(item.Config) > 0 {
		if pretty, err := json.MarshalIndent(json.RawMessage(item.Config), "", "  "); err == nil {
			configPreview = trimPreview(string(pretty), 500)
		}
	}

	return strings.Join([]string{
		"ID: " + fallbackText(item.ID, "(unknown)"),
		"Name: " + fallbackText(item.Name, "(unnamed)"),
		"Type: " + typeLabel,
		fmt.Sprintf("Auto renew: %t", item.AutoRenewAuth),
		"Last accessed: " + item.LastAccessed.Format("2006-01-02 15:04:05"),
		"Modified: " + item.Modified.Format("2006-01-02 15:04:05"),
		"Config preview:",
		configPreview,
	}, "\n")
}

func formatSecretInspectMessage(item cli.SecretGetResult) string {
	masked := "••••••"
	if strings.TrimSpace(item.Value) == "" {
		masked = "(empty)"
	}
	return strings.Join([]string{
		"ID: " + fallbackText(item.ID, "(unknown)"),
		"Name: " + fallbackText(item.Name, "(unnamed)"),
		"Value: " + masked,
		fmt.Sprintf("Value length: %d", len(item.Value)),
		"Last accessed: " + item.LastAccessed.Format("2006-01-02 15:04:05"),
		"Modified: " + item.Modified.Format("2006-01-02 15:04:05"),
	}, "\n")
}

func formatFlatMapLines(values map[string]string) string {
	if len(values) == 0 {
		return "(none)"
	}

	keys := make([]string, 0, len(values))
	for key := range values {
		keys = append(keys, key)
	}
	sort.Strings(keys)

	lines := make([]string, 0, len(keys))
	for _, key := range keys {
		lines = append(lines, "  - "+key+" = "+values[key])
	}
	return "\n" + strings.Join(lines, "\n")
}

func trimPreview(value string, limit int) string {
	trimmed := strings.TrimSpace(value)
	if trimmed == "" || len(trimmed) <= limit {
		return trimmed
	}
	return trimmed[:limit] + "..."
}

func fallbackText(value, fallback string) string {
	if strings.TrimSpace(value) == "" {
		return fallback
	}
	return value
}

func (m *Model) openRequestBodyTypeSelect(pending pendingAction) (tea.Model, tea.Cmd) {
	items := make([]dialogs.Choice, 0, len(requestBodyTypeChoices))
	for _, choice := range requestBodyTypeChoices {
		items = append(items, dialogs.Choice{
			Key:         choice.Key,
			Title:       choice.Title,
			Description: choice.Description,
		})
	}

	switch {
	case pending.Identifier != "":
		m.openSelectFlow(flowRequestEditBodyPick, "Edit request", "Choose the request body type", "Enter choose  Esc cancel", items, pending)
	default:
		m.openSelectFlow(flowRequestCreateBodyPick, "Create request", "Choose the request body type", "Enter choose  Esc cancel", items, pending)
	}
	return m, nil
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

func (m *Model) refreshScreen(screen state.ScreenID) tea.Cmd {
	switch screen {
	case state.ScreenWorkspaces:
		m.session.Busy = true
		return bootstrapCmd(m.ctx, m.client)
	case state.ScreenRequests:
		if m.session.ActiveWorkspace == nil {
			return nil
		}
		m.session.Busy = true
		return refreshRequestsCmd(m.ctx, m.client, m.session.ActiveWorkspace.ID, m.session.ActiveWorkspace.Name)
	case state.ScreenAuths:
		if m.session.ActiveWorkspace == nil {
			return nil
		}
		m.session.Busy = true
		return refreshAuthsCmd(m.ctx, m.client, m.session.ActiveWorkspace.ID, m.session.ActiveWorkspace.Name)
	case state.ScreenSecrets:
		m.session.Busy = true
		return refreshSecretsCmd(m.ctx, m.client)
	default:
		return nil
	}
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

func summarizeFlatHeaders(headers map[string]string) string {
	if len(headers) == 0 {
		return "(no headers)"
	}

	lines := make([]string, 0, len(headers))
	for key, value := range headers {
		lines = append(lines, fmt.Sprintf("%s: %s", key, value))
	}
	return strings.Join(lines, "\n")
}

func sendBodyText(response send.Response) string {
	if response.Body != "" {
		return response.Body
	}
	return response.RawBody
}

func defaultSendBodyPath(request *state.RequestRef, response send.Response) string {
	name := "response-body"
	if request != nil && strings.TrimSpace(request.Name) != "" {
		name = sanitizeFilename(request.Name) + "-body"
	}
	ext := ".txt"
	if looksLikeJSON(response.Body) || looksLikeJSON(response.RawBody) {
		ext = ".json"
	}
	return name + ext
}

func defaultSendExportPath(request *state.RequestRef) string {
	name := "response-export"
	if request != nil && strings.TrimSpace(request.Name) != "" {
		name = sanitizeFilename(request.Name) + "-response"
	}
	return name + ".txt"
}

func formatSendExport(request send.Request, response send.Response) string {
	var b strings.Builder
	b.WriteString("Request\n")
	if request.Name != "" {
		b.WriteString("Name: " + request.Name + "\n")
	}
	if request.Method != "" || request.URI != "" {
		b.WriteString("Line: " + strings.TrimSpace(request.Method+" "+request.URI) + "\n")
	}
	if request.Auth != "" {
		b.WriteString("Auth: " + request.Auth + "\n")
	}
	b.WriteString("\nResponse\n")
	if response.StatusText != "" {
		b.WriteString("Status: " + response.StatusText + "\n")
	}
	if response.HTTPVersion != "" {
		b.WriteString("HTTP Version: " + response.HTTPVersion + "\n")
	}
	if response.Duration > 0 {
		b.WriteString(fmt.Sprintf("DurationMs: %d\n", response.Duration.Milliseconds()))
	}
	if len(response.Notes) > 0 {
		b.WriteString("Notes:\n")
		for _, note := range response.Notes {
			if strings.TrimSpace(note) == "" {
				continue
			}
			b.WriteString("- " + note + "\n")
		}
	}
	if response.Summary != "" {
		b.WriteString("\nHeaders\n")
		b.WriteString(response.Summary)
		b.WriteString("\n")
	}
	body := sendBodyText(response)
	if body != "" {
		b.WriteString("\nBody\n")
		b.WriteString(body)
		if !strings.HasSuffix(body, "\n") {
			b.WriteString("\n")
		}
	}
	if response.Error != "" {
		b.WriteString("\nError\n")
		b.WriteString(response.Error)
		if !strings.HasSuffix(response.Error, "\n") {
			b.WriteString("\n")
		}
	}
	return strings.TrimRight(b.String(), "\n") + "\n"
}

func sanitizeFilename(value string) string {
	value = strings.TrimSpace(value)
	if value == "" {
		return "response"
	}
	replacer := strings.NewReplacer(
		"\\", "-",
		"/", "-",
		":", "-",
		"*", "-",
		"?", "-",
		"\"", "-",
		"<", "-",
		">", "-",
		"|", "-",
		" ", "-",
	)
	value = replacer.Replace(value)
	value = strings.Trim(value, "-.")
	if value == "" {
		return "response"
	}
	return value
}

func looksLikeJSON(value string) bool {
	trimmed := strings.TrimSpace(value)
	return strings.HasPrefix(trimmed, "{") || strings.HasPrefix(trimmed, "[")
}

func sendPaneText(view *send.View) string {
	if view == nil {
		return ""
	}
	if view.FocusedPane == send.PaneSummary {
		if view.Response.Summary != "" {
			return view.Response.Summary
		}
		return view.Response.StatusText
	}
	return sendBodyText(view.Response)
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
