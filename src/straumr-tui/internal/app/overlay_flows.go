package app

import (
	"strings"

	"straumr-tui/internal/views/auth"
	"straumr-tui/internal/views/dialogs"
	"straumr-tui/internal/views/request"

	tea "github.com/charmbracelet/bubbletea"
)

type pendingFlow string

const (
	flowNone                   pendingFlow = ""
	flowWorkspaceCreateName    pendingFlow = "workspace-create-name"
	flowWorkspaceEditName      pendingFlow = "workspace-edit-name"
	flowWorkspaceCopyName      pendingFlow = "workspace-copy-name"
	flowWorkspaceDeleteConfirm pendingFlow = "workspace-delete-confirm"
	flowRequestCreateName      pendingFlow = "request-create-name"
	flowRequestCreateURL       pendingFlow = "request-create-url"
	flowRequestCreateMethod    pendingFlow = "request-create-method"
	flowRequestCreateAuth      pendingFlow = "request-create-auth"
	flowRequestCreateBodyType  pendingFlow = "request-create-body-type"
	flowRequestCreateBody      pendingFlow = "request-create-body"
	flowRequestEditLoad        pendingFlow = "request-edit-load"
	flowRequestEditName        pendingFlow = "request-edit-name"
	flowRequestEditURL         pendingFlow = "request-edit-url"
	flowRequestEditMethod      pendingFlow = "request-edit-method"
	flowRequestEditAuth        pendingFlow = "request-edit-auth"
	flowRequestEditBodyType    pendingFlow = "request-edit-body-type"
	flowRequestEditBody        pendingFlow = "request-edit-body"
	flowRequestCopyName        pendingFlow = "request-copy-name"
	flowRequestDeleteConfirm   pendingFlow = "request-delete-confirm"
	flowAuthName               pendingFlow = "auth-name"
	flowAuthType               pendingFlow = "auth-type"
	flowAuthSecret             pendingFlow = "auth-secret"
	flowAuthPrefix             pendingFlow = "auth-prefix"
	flowAuthUsername           pendingFlow = "auth-username"
	flowAuthPassword           pendingFlow = "auth-password"
	flowAuthGrant              pendingFlow = "auth-grant"
	flowAuthTokenURL           pendingFlow = "auth-token-url"
	flowAuthClientID           pendingFlow = "auth-client-id"
	flowAuthClientSecret       pendingFlow = "auth-client-secret"
	flowAuthScope              pendingFlow = "auth-scope"
	flowAuthAuthorizationURL   pendingFlow = "auth-authorization-url"
	flowAuthRedirectURI        pendingFlow = "auth-redirect-uri"
	flowAuthPKCE               pendingFlow = "auth-pkce"
	flowAuthCustomURL          pendingFlow = "auth-custom-url"
	flowAuthCustomMethod       pendingFlow = "auth-custom-method"
	flowAuthCustomBody         pendingFlow = "auth-custom-body"
	flowAuthCustomBodyType     pendingFlow = "auth-custom-body-type"
	flowAuthExtractionSource   pendingFlow = "auth-extraction-source"
	flowAuthExtractionExpr     pendingFlow = "auth-extraction-expression"
	flowAuthApplyHeaderName    pendingFlow = "auth-apply-header-name"
	flowAuthApplyHeaderTpl     pendingFlow = "auth-apply-header-template"
	flowAuthAutoRenew          pendingFlow = "auth-auto-renew"
	flowAuthEditLoad           pendingFlow = "auth-edit-load"
	flowAuthCopyName           pendingFlow = "auth-copy-name"
	flowAuthDeleteConfirm      pendingFlow = "auth-delete-confirm"
	flowSecretCreateName       pendingFlow = "secret-create-name"
	flowSecretCreateValue      pendingFlow = "secret-create-value"
	flowSecretEditLoad         pendingFlow = "secret-edit-load"
	flowSecretEditName         pendingFlow = "secret-edit-name"
	flowSecretEditValue        pendingFlow = "secret-edit-value"
	flowSecretCopyName         pendingFlow = "secret-copy-name"
	flowSecretDeleteConfirm    pendingFlow = "secret-delete-confirm"
	flowSendSavePath           pendingFlow = "send-save-path"
	flowSendExportPath         pendingFlow = "send-export-path"
)

type pendingAction struct {
	Flow          pendingFlow
	Identifier    string
	Name          string
	Value         string
	WorkspaceID   string
	WorkspaceName string
	RequestDraft  request.Draft
	AuthDraft     auth.Draft
	AuthMode      auth.EditorMode
	OutputText    string
}

func (m *Model) hasOverlay() bool {
	return m.textInput.Active || m.secretInput.Active || m.confirm.Active
}

func (m *Model) overlayView() string {
	switch {
	case m.textInput.Active:
		return m.textInput.Render()
	case m.secretInput.Active:
		return m.secretInput.Render()
	case m.confirm.Active:
		return m.confirm.Render()
	default:
		return ""
	}
}

func (m *Model) clearOverlays() {
	m.textInput.Close()
	m.secretInput.Close()
	m.confirm.Close()
	m.pending = nil
}

func (m *Model) openTextFlow(flow pendingFlow, title, label, value, placeholder, message string, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending
	m.textInput.Open(title, label, value, placeholder, message)
}

func (m *Model) openSecretFlow(flow pendingFlow, title, label, value, placeholder, message string, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending
	m.secretInput.Open(title, label, value, placeholder, message)
}

func (m *Model) openConfirmFlow(flow pendingFlow, title, message string, options []string, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending
	m.confirm.Open(title, message, options)
}

func (m *Model) handleOverlayKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	switch {
	case m.textInput.Active:
		return m.handleTextInputKey(msg)
	case m.secretInput.Active:
		return m.handleSecretInputKey(msg)
	case m.confirm.Active:
		return m.handleConfirmKey(msg)
	default:
		return m, nil
	}
}

func (m *Model) handleTextInputKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if key, ok := dialogKey(msg); ok {
		switch m.textInput.HandleKey(key) {
		case dialogs.ActionAccept:
			return m.acceptTextInput(strings.TrimSpace(m.textInput.Value))
		case dialogs.ActionCancel:
			m.clearOverlays()
			return m, nil
		}
	}

	applyTextEdit(&m.textInput.Value, &m.textInput.Cursor, msg)
	return m, nil
}

func (m *Model) handleSecretInputKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if key, ok := dialogKey(msg); ok {
		switch m.secretInput.HandleKey(key) {
		case dialogs.ActionAccept:
			return m.acceptSecretInput(m.secretInput.Value)
		case dialogs.ActionCancel:
			m.clearOverlays()
			return m, nil
		}
	}

	if msg.String() == "ctrl+v" {
		m.secretInput.Visible = !m.secretInput.Visible
		return m, nil
	}

	applyTextEdit(&m.secretInput.Value, &m.secretInput.Cursor, msg)
	return m, nil
}

func (m *Model) handleConfirmKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	key, ok := dialogKey(msg)
	if !ok {
		return m, nil
	}

	switch m.confirm.HandleKey(key) {
	case dialogs.ActionAccept:
		choice := strings.ToLower(strings.TrimSpace(m.confirm.Result(true).Choice))
		return m.acceptConfirm(choice)
	case dialogs.ActionCancel:
		m.clearOverlays()
		return m, nil
	default:
		return m, nil
	}
}

func (m *Model) acceptTextInput(value string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	if isAuthFlow(m.pending.Flow) {
		return m.advanceAuthTextFlow(value)
	}

	switch m.pending.Flow {
	case flowWorkspaceCreateName:
		if value == "" {
			m.textInput.Message = "Workspace name is required"
			return m, nil
		}
		m.clearOverlays()
		m.session.Busy = true
		return m, createWorkspaceCmd(m.ctx, m.client, value)
	case flowWorkspaceEditName:
		if value == "" {
			m.textInput.Message = "Workspace name is required"
			return m, nil
		}
		identifier := m.pending.Identifier
		m.clearOverlays()
		m.session.Busy = true
		return m, editWorkspaceCmd(m.ctx, m.client, identifier, value)
	case flowWorkspaceCopyName:
		if value == "" {
			m.textInput.Message = "Workspace name is required"
			return m, nil
		}
		identifier := m.pending.Identifier
		m.clearOverlays()
		m.session.Busy = true
		return m, copyWorkspaceCmd(m.ctx, m.client, identifier, value)
	case flowRequestCopyName:
		if value == "" {
			m.textInput.Message = "Request name is required"
			return m, nil
		}
		pending := *m.pending
		m.clearOverlays()
		m.session.Busy = true
		return m, copyRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, value)
	case flowRequestCreateName:
		if value == "" {
			m.textInput.Message = "Request name is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithName(value)
		m.openTextFlow(flowRequestCreateURL, "Create request", "URL", pending.RequestDraft.URL, "https://example.com", "Enter the request URL", pending)
		return m, nil
	case flowRequestCreateURL:
		if value == "" {
			m.textInput.Message = "Request URL is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithURL(value)
		method := pending.RequestDraft.Method
		if strings.TrimSpace(method) == "" {
			method = "GET"
			pending.RequestDraft = pending.RequestDraft.WithMethod(method)
		}
		m.openTextFlow(flowRequestCreateMethod, "Create request", "Method", method, "GET", "Enter the HTTP method", pending)
		return m, nil
	case flowRequestCreateMethod:
		if value == "" {
			m.textInput.Message = "HTTP method is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(value))
		m.openTextFlow(flowRequestCreateAuth, "Create request", "Auth", pending.RequestDraft.Auth, "(blank for none)", "Enter an auth name or ID, or leave blank for none", pending)
		return m, nil
	case flowRequestCreateAuth:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithAuth(value)
		bodyType := pending.RequestDraft.BodyType
		if strings.TrimSpace(bodyType) == "" {
			bodyType = "none"
		}
		m.openTextFlow(flowRequestCreateBodyType, "Create request", "Body type", bodyType, "none|json|xml|text|form|multipart|raw", "Enter the body type", pending)
		return m, nil
	case flowRequestCreateBodyType:
		pending := *m.pending
		bodyType := normalizeBodyTypeForCLI(value)
		if bodyType == "" {
			bodyType = "none"
		}
		pending.RequestDraft = pending.RequestDraft.WithBody(bodyType, pending.RequestDraft.Body)
		if bodyType == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.clearOverlays()
			m.session.Busy = true
			return m, createRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.RequestDraft.MutationDraft())
		}
		m.openTextFlow(flowRequestCreateBody, "Create request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowRequestCreateBody:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(pending.RequestDraft.BodyType), value)
		m.clearOverlays()
		m.session.Busy = true
		return m, createRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.RequestDraft.MutationDraft())
	case flowRequestEditName:
		if value == "" {
			m.textInput.Message = "Request name is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithName(value)
		m.openTextFlow(flowRequestEditURL, "Edit request", "URL", pending.RequestDraft.URL, "https://example.com", "Update the request URL", pending)
		return m, nil
	case flowRequestEditURL:
		if value == "" {
			m.textInput.Message = "Request URL is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithURL(value)
		method := pending.RequestDraft.Method
		if strings.TrimSpace(method) == "" {
			method = "GET"
		}
		m.openTextFlow(flowRequestEditMethod, "Edit request", "Method", method, "GET", "Update the HTTP method", pending)
		return m, nil
	case flowRequestEditMethod:
		if value == "" {
			m.textInput.Message = "HTTP method is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(value))
		m.openTextFlow(flowRequestEditAuth, "Edit request", "Auth", pending.RequestDraft.Auth, "(blank for none)", "Enter an auth name or ID, or leave blank for none", pending)
		return m, nil
	case flowRequestEditAuth:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithAuth(value)
		bodyType := pending.RequestDraft.BodyType
		if strings.TrimSpace(bodyType) == "" {
			bodyType = "none"
		}
		m.openTextFlow(flowRequestEditBodyType, "Edit request", "Body type", bodyType, "none|json|xml|text|form|multipart|raw", "Enter the body type", pending)
		return m, nil
	case flowRequestEditBodyType:
		pending := *m.pending
		bodyType := normalizeBodyTypeForCLI(value)
		if bodyType == "" {
			bodyType = "none"
		}
		pending.RequestDraft = pending.RequestDraft.WithBody(bodyType, pending.RequestDraft.Body)
		if bodyType == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.clearOverlays()
			m.session.Busy = true
			return m, editRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.RequestDraft.MutationDraft())
		}
		m.openTextFlow(flowRequestEditBody, "Edit request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowRequestEditBody:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(pending.RequestDraft.BodyType), value)
		m.clearOverlays()
		m.session.Busy = true
		return m, editRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.RequestDraft.MutationDraft())
	case flowAuthCopyName:
		if value == "" {
			m.textInput.Message = "Auth name is required"
			return m, nil
		}
		pending := *m.pending
		m.clearOverlays()
		m.session.Busy = true
		return m, copyAuthCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, value)
	case flowSecretCreateName:
		if value == "" {
			m.textInput.Message = "Secret name is required"
			return m, nil
		}
		pending := *m.pending
		pending.Name = value
		m.openSecretFlow(flowSecretCreateValue, "Create secret", "Value", "", "secret value", "Enter the secret value", pending)
		return m, nil
	case flowSecretEditName:
		if value == "" {
			m.textInput.Message = "Secret name is required"
			return m, nil
		}
		pending := *m.pending
		pending.Name = value
		m.openSecretFlow(flowSecretEditValue, "Edit secret", "Value", pending.Value, "secret value", "Update the secret value", pending)
		return m, nil
	case flowSecretCopyName:
		if value == "" {
			m.textInput.Message = "Secret name is required"
			return m, nil
		}
		identifier := m.pending.Identifier
		m.clearOverlays()
		m.session.Busy = true
		return m, copySecretCmd(m.ctx, m.client, identifier, value)
	case flowSendSavePath:
		if value == "" {
			m.textInput.Message = "A file path is required"
			return m, nil
		}
		content := m.pending.OutputText
		m.clearOverlays()
		m.session.Busy = true
		return m, writeFileCmd(value, content, "Saved response body to "+value)
	case flowSendExportPath:
		if value == "" {
			m.textInput.Message = "A file path is required"
			return m, nil
		}
		content := m.pending.OutputText
		m.clearOverlays()
		m.session.Busy = true
		return m, writeFileCmd(value, content, "Exported response to "+value)
	default:
		m.clearOverlays()
		return m, nil
	}
}

func (m *Model) acceptSecretInput(value string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	if isAuthFlow(m.pending.Flow) {
		return m.advanceAuthSecretFlow(value)
	}

	if strings.TrimSpace(value) == "" {
		m.secretInput.Message = "Secret value is required"
		return m, nil
	}

	switch m.pending.Flow {
	case flowSecretCreateValue:
		pending := *m.pending
		m.clearOverlays()
		m.session.Busy = true
		return m, createSecretCmd(m.ctx, m.client, pending.Name, value)
	case flowSecretEditValue:
		pending := *m.pending
		m.clearOverlays()
		m.session.Busy = true
		return m, editSecretCmd(m.ctx, m.client, pending.Identifier, pending.Name, value)
	default:
		m.clearOverlays()
		return m, nil
	}
}

func (m *Model) acceptConfirm(choice string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	if isAuthFlow(m.pending.Flow) {
		return m.advanceAuthConfirmFlow(choice)
	}

	pending := *m.pending
	m.clearOverlays()

	switch pending.Flow {
	case flowWorkspaceDeleteConfirm:
		if choice != "delete" {
			return m, nil
		}
		m.session.Busy = true
		return m, deleteWorkspaceCmd(m.ctx, m.client, pending.Identifier, pending.Name)
	case flowRequestDeleteConfirm:
		if choice != "delete" {
			return m, nil
		}
		m.session.Busy = true
		return m, deleteRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.Name)
	case flowAuthDeleteConfirm:
		if choice != "delete" {
			return m, nil
		}
		m.session.Busy = true
		return m, deleteAuthCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.Name)
	case flowSecretDeleteConfirm:
		if choice != "delete" {
			return m, nil
		}
		m.session.Busy = true
		return m, deleteSecretCmd(m.ctx, m.client, pending.Identifier, pending.Name)
	default:
		return m, nil
	}
}

func dialogKey(msg tea.KeyMsg) (dialogs.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return dialogs.KeyUp, true
	case "down", "j":
		return dialogs.KeyDown, true
	case "left", "h":
		return dialogs.KeyLeft, true
	case "right", "l":
		return dialogs.KeyRight, true
	case "g":
		return dialogs.KeyHome, true
	case "G":
		return dialogs.KeyEnd, true
	case "enter":
		return dialogs.KeyEnter, true
	case "esc":
		return dialogs.KeyCancel, true
	case "/":
		return dialogs.KeySearch, true
	case "tab":
		return dialogs.KeyTab, true
	case "shift+tab", "backtab":
		return dialogs.KeyShiftTab, true
	default:
		return "", false
	}
}

func isAuthFlow(flow pendingFlow) bool {
	switch flow {
	case flowAuthName,
		flowAuthType,
		flowAuthSecret,
		flowAuthPrefix,
		flowAuthUsername,
		flowAuthPassword,
		flowAuthGrant,
		flowAuthTokenURL,
		flowAuthClientID,
		flowAuthClientSecret,
		flowAuthScope,
		flowAuthAuthorizationURL,
		flowAuthRedirectURI,
		flowAuthPKCE,
		flowAuthCustomURL,
		flowAuthCustomMethod,
		flowAuthCustomBody,
		flowAuthCustomBodyType,
		flowAuthExtractionSource,
		flowAuthExtractionExpr,
		flowAuthApplyHeaderName,
		flowAuthApplyHeaderTpl,
		flowAuthAutoRenew,
		flowAuthEditLoad:
		return true
	default:
		return false
	}
}

func applyTextEdit(value *string, cursor *int, msg tea.KeyMsg) {
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
