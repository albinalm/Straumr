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
	flowNone                      pendingFlow = ""
	flowWorkspaceCreateName       pendingFlow = "workspace-create-name"
	flowWorkspaceEditName         pendingFlow = "workspace-edit-name"
	flowWorkspaceCopyName         pendingFlow = "workspace-copy-name"
	flowWorkspaceImportPath       pendingFlow = "workspace-import-path"
	flowWorkspaceExportPath       pendingFlow = "workspace-export-path"
	flowWorkspaceInspect          pendingFlow = "workspace-inspect"
	flowWorkspaceDeleteConfirm    pendingFlow = "workspace-delete-confirm"
	flowRequestCreateName         pendingFlow = "request-create-name"
	flowRequestCreateURL          pendingFlow = "request-create-url"
	flowRequestCreateMethod       pendingFlow = "request-create-method"
	flowRequestCreateMethodPick   pendingFlow = "request-create-method-pick"
	flowRequestCreateAuthPick     pendingFlow = "request-create-auth-pick"
	flowRequestCreateAuth         pendingFlow = "request-create-auth"
	flowRequestCreateBodyPick     pendingFlow = "request-create-body-pick"
	flowRequestCreateBodyType     pendingFlow = "request-create-body-type"
	flowRequestCreateBody         pendingFlow = "request-create-body"
	flowRequestHeadersEditor      pendingFlow = "request-headers-editor"
	flowRequestHeaderAddKey       pendingFlow = "request-header-add-key"
	flowRequestHeaderAddValue     pendingFlow = "request-header-add-value"
	flowRequestHeaderEditValue    pendingFlow = "request-header-edit-value"
	flowRequestParamsEditor       pendingFlow = "request-params-editor"
	flowRequestParamAddKey        pendingFlow = "request-param-add-key"
	flowRequestParamAddValue      pendingFlow = "request-param-add-value"
	flowRequestParamEditValue     pendingFlow = "request-param-edit-value"
	flowRequestEditLoad           pendingFlow = "request-edit-load"
	flowRequestInspect            pendingFlow = "request-inspect"
	flowRequestEditName           pendingFlow = "request-edit-name"
	flowRequestEditURL            pendingFlow = "request-edit-url"
	flowRequestEditMethod         pendingFlow = "request-edit-method"
	flowRequestEditMethodPick     pendingFlow = "request-edit-method-pick"
	flowRequestEditAuthPick       pendingFlow = "request-edit-auth-pick"
	flowRequestEditAuth           pendingFlow = "request-edit-auth"
	flowRequestEditBodyPick       pendingFlow = "request-edit-body-pick"
	flowRequestEditBodyType       pendingFlow = "request-edit-body-type"
	flowRequestEditBody           pendingFlow = "request-edit-body"
	flowRequestCopyName           pendingFlow = "request-copy-name"
	flowRequestDeleteConfirm      pendingFlow = "request-delete-confirm"
	flowAuthName                  pendingFlow = "auth-name"
	flowAuthType                  pendingFlow = "auth-type"
	flowAuthTypePick              pendingFlow = "auth-type-pick"
	flowAuthSecret                pendingFlow = "auth-secret"
	flowAuthPrefix                pendingFlow = "auth-prefix"
	flowAuthUsername              pendingFlow = "auth-username"
	flowAuthPassword              pendingFlow = "auth-password"
	flowAuthGrant                 pendingFlow = "auth-grant"
	flowAuthGrantPick             pendingFlow = "auth-grant-pick"
	flowAuthTokenURL              pendingFlow = "auth-token-url"
	flowAuthClientID              pendingFlow = "auth-client-id"
	flowAuthClientSecret          pendingFlow = "auth-client-secret"
	flowAuthScope                 pendingFlow = "auth-scope"
	flowAuthAuthorizationURL      pendingFlow = "auth-authorization-url"
	flowAuthRedirectURI           pendingFlow = "auth-redirect-uri"
	flowAuthPKCE                  pendingFlow = "auth-pkce"
	flowAuthPKCEPick              pendingFlow = "auth-pkce-pick"
	flowAuthCustomURL             pendingFlow = "auth-custom-url"
	flowAuthCustomMethod          pendingFlow = "auth-custom-method"
	flowAuthCustomHeadersEditor   pendingFlow = "auth-custom-headers-editor"
	flowAuthCustomHeaderAddKey    pendingFlow = "auth-custom-header-add-key"
	flowAuthCustomHeaderAddValue  pendingFlow = "auth-custom-header-add-value"
	flowAuthCustomHeaderEditValue pendingFlow = "auth-custom-header-edit-value"
	flowAuthCustomParamsEditor    pendingFlow = "auth-custom-params-editor"
	flowAuthCustomParamAddKey     pendingFlow = "auth-custom-param-add-key"
	flowAuthCustomParamAddValue   pendingFlow = "auth-custom-param-add-value"
	flowAuthCustomParamEditValue  pendingFlow = "auth-custom-param-edit-value"
	flowAuthCustomBody            pendingFlow = "auth-custom-body"
	flowAuthCustomBodyType        pendingFlow = "auth-custom-body-type"
	flowAuthCustomBodyTypePick    pendingFlow = "auth-custom-body-type-pick"
	flowAuthExtractionSource      pendingFlow = "auth-extraction-source"
	flowAuthExtractionSourcePick  pendingFlow = "auth-extraction-source-pick"
	flowAuthExtractionExpr        pendingFlow = "auth-extraction-expression"
	flowAuthApplyHeaderName       pendingFlow = "auth-apply-header-name"
	flowAuthApplyHeaderTpl        pendingFlow = "auth-apply-header-template"
	flowAuthAutoRenew             pendingFlow = "auth-auto-renew"
	flowAuthEditLoad              pendingFlow = "auth-edit-load"
	flowAuthCopyName              pendingFlow = "auth-copy-name"
	flowAuthInspect               pendingFlow = "auth-inspect"
	flowAuthDeleteConfirm         pendingFlow = "auth-delete-confirm"
	flowSecretCreateName          pendingFlow = "secret-create-name"
	flowSecretCreateValue         pendingFlow = "secret-create-value"
	flowSecretEditLoad            pendingFlow = "secret-edit-load"
	flowSecretEditName            pendingFlow = "secret-edit-name"
	flowSecretEditValue           pendingFlow = "secret-edit-value"
	flowSecretCopyName            pendingFlow = "secret-copy-name"
	flowSecretInspect             pendingFlow = "secret-inspect"
	flowSecretDeleteConfirm       pendingFlow = "secret-delete-confirm"
	flowSendSavePath              pendingFlow = "send-save-path"
	flowSendExportPath            pendingFlow = "send-export-path"
)

type pendingAction struct {
	Flow          pendingFlow
	Identifier    string
	Name          string
	Value         string
	PairKey       string
	WorkspaceID   string
	WorkspaceName string
	RequestDraft  request.Draft
	AuthDraft     auth.Draft
	AuthMode      auth.EditorMode
	OutputText    string
	PathMode      dialogs.PathMode
	PathMustExist bool
	PathDirectory bool
}

func (m *Model) hasOverlay() bool {
	return m.textInput.Active || m.secretInput.Active || m.confirm.Active || m.selectView.Active || m.keyValue.Active || m.pathPicker.Active
}

func (m *Model) overlayView() string {
	switch {
	case m.textInput.Active:
		return m.textInput.Render()
	case m.secretInput.Active:
		return m.secretInput.Render()
	case m.confirm.Active:
		return m.confirm.Render()
	case m.selectView.Active:
		return m.selectView.Render()
	case m.keyValue.Active:
		return m.keyValue.Render()
	case m.pathPicker.Active:
		return m.pathPicker.Render()
	default:
		return ""
	}
}

func (m *Model) clearOverlays() {
	m.textInput.Close()
	m.secretInput.Close()
	m.confirm.Close()
	m.selectView.Close()
	m.keyValue.Close()
	m.pathPicker.Close()
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

func (m *Model) openSelectFlow(flow pendingFlow, title, message, help string, items []dialogs.Choice, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending
	m.selectView.Open(title, message, help, items)
}

func (m *Model) openKeyValueFlow(flow pendingFlow, title, message string, items []dialogs.Pair, pending pendingAction) {
	m.clearOverlays()
	pending.Flow = flow
	m.pending = &pending
	m.keyValue.Open(title, message, items)
}

func (m *Model) handleOverlayKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	switch {
	case m.textInput.Active:
		return m.handleTextInputKey(msg)
	case m.secretInput.Active:
		return m.handleSecretInputKey(msg)
	case m.confirm.Active:
		return m.handleConfirmKey(msg)
	case m.selectView.Active:
		return m.handleSelectKey(msg)
	case m.keyValue.Active:
		return m.handleKeyValueKey(msg)
	case m.pathPicker.Active:
		return m.handlePathPickerKey(msg)
	default:
		return m, nil
	}
}

func (m *Model) handleSelectKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	key, ok := dialogKey(msg)
	if !ok {
		return m, nil
	}

	switch m.selectView.HandleKey(key) {
	case dialogs.ActionAccept:
		return m.acceptSelect(strings.TrimSpace(m.selectView.Result(true).Choice.Key))
	case dialogs.ActionCancel:
		m.clearOverlays()
		return m, nil
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

func (m *Model) handleKeyValueKey(msg tea.KeyMsg) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	if msg.String() == "c" {
		switch m.pending.Flow {
		case flowRequestHeadersEditor:
			pending := *m.pending
			m.openTextFlow(flowRequestHeaderAddKey, "Request headers", "Header name", "", "Header-Name", "Enter the header name", pending)
			return m, nil
		case flowRequestParamsEditor:
			pending := *m.pending
			m.openTextFlow(flowRequestParamAddKey, "Request params", "Param name", "", "param", "Enter the query parameter name", pending)
			return m, nil
		case flowAuthCustomHeadersEditor:
			pending := *m.pending
			m.openTextFlow(flowAuthCustomHeaderAddKey, authTitle(pending), "Header name", "", "Header-Name", "Enter the custom auth header name", pending)
			return m, nil
		case flowAuthCustomParamsEditor:
			pending := *m.pending
			m.openTextFlow(flowAuthCustomParamAddKey, authTitle(pending), "Param name", "", "param", "Enter the custom auth query parameter name", pending)
			return m, nil
		}
	}

	key, ok := dialogKey(msg)
	if !ok {
		return m, nil
	}

	switch m.keyValue.HandleKey(key) {
	case dialogs.ActionMove:
		return m, nil
	case dialogs.ActionEditValue:
		result := m.keyValue.Result(true)
		if result.Item.Key == "" {
			return m, nil
		}
		pending := *m.pending
		pending.PairKey = result.Item.Key
		switch m.pending.Flow {
		case flowRequestHeadersEditor:
			m.openTextFlow(flowRequestHeaderEditValue, "Request headers", "Header value", result.Item.Value, "(empty)", "Update the header value", pending)
		case flowRequestParamsEditor:
			m.openTextFlow(flowRequestParamEditValue, "Request params", "Param value", result.Item.Value, "(empty)", "Update the parameter value", pending)
		case flowAuthCustomHeadersEditor:
			m.openTextFlow(flowAuthCustomHeaderEditValue, authTitle(pending), "Header value", result.Item.Value, "(empty)", "Update the custom auth header value", pending)
		case flowAuthCustomParamsEditor:
			m.openTextFlow(flowAuthCustomParamEditValue, authTitle(pending), "Param value", result.Item.Value, "(empty)", "Update the custom auth query parameter value", pending)
		}
		return m, nil
	case dialogs.ActionDelete:
		result := m.keyValue.Result(true)
		if result.Item.Key == "" {
			return m, nil
		}
		switch m.pending.Flow {
		case flowRequestHeadersEditor:
			pending := *m.pending
			pending.RequestDraft = pending.RequestDraft.WithoutHeader(result.Item.Key)
			m.openRequestHeadersEditor(pending)
		case flowRequestParamsEditor:
			pending := *m.pending
			pending.RequestDraft = pending.RequestDraft.WithoutParam(result.Item.Key)
			m.openRequestParamsEditor(pending)
		case flowAuthCustomHeadersEditor:
			pending := *m.pending
			pending.AuthDraft.CustomHeaders = withoutAuthPair(pending.AuthDraft.CustomHeaders, result.Item.Key)
			m.openAuthCustomHeadersEditor(pending)
		case flowAuthCustomParamsEditor:
			pending := *m.pending
			pending.AuthDraft.CustomParams = withoutAuthPair(pending.AuthDraft.CustomParams, result.Item.Key)
			m.openAuthCustomParamsEditor(pending)
		}
		return m, nil
	case dialogs.ActionCancel:
		switch m.pending.Flow {
		case flowRequestHeadersEditor:
			pending := *m.pending
			m.openRequestParamsEditor(pending)
		case flowRequestParamsEditor:
			return m.finalizeRequestDraft(*m.pending)
		case flowAuthCustomHeadersEditor:
			pending := *m.pending
			m.openAuthCustomParamsEditor(pending)
		case flowAuthCustomParamsEditor:
			pending := *m.pending
			m.openTextFlow(flowAuthCustomBody, authTitle(pending), "Body", pending.AuthDraft.CustomBody, "{\"user\":\"admin\"}", "Enter the custom auth body", pending)
		default:
			m.clearOverlays()
			return m, nil
		}
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
		m.openRequestMethodSelect(pending)
		return m, nil
	case flowRequestCreateMethod:
		if value == "" {
			m.textInput.Message = "HTTP method is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(value))
		m.clearOverlays()
		m.session.Busy = true
		return m, loadRequestAuthChoicesCmd(m.ctx, m.client, pending.WorkspaceID, flowRequestCreateAuthPick)
	case flowRequestCreateAuth:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithAuth(value)
		return m.openRequestBodyTypeSelect(pending)
	case flowRequestCreateBodyType:
		pending := *m.pending
		bodyType := normalizeBodyTypeForCLI(value)
		if bodyType == "" {
			bodyType = "none"
		}
		pending.RequestDraft = pending.RequestDraft.WithBody(bodyType, pending.RequestDraft.Body)
		if bodyType == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.openRequestHeadersEditor(pending)
			return m, nil
		}
		m.openTextFlow(flowRequestCreateBody, "Create request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowRequestCreateBody:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(pending.RequestDraft.BodyType), value)
		m.openRequestHeadersEditor(pending)
		return m, nil
	case flowRequestHeaderAddKey:
		if value == "" {
			m.textInput.Message = "Header name is required"
			return m, nil
		}
		pending := *m.pending
		pending.PairKey = value
		m.openTextFlow(flowRequestHeaderAddValue, "Request headers", "Header value", "", "(empty)", "Enter the header value", pending)
		return m, nil
	case flowRequestHeaderAddValue:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithHeader(pending.PairKey, value)
		pending.PairKey = ""
		m.openRequestHeadersEditor(pending)
		return m, nil
	case flowRequestHeaderEditValue:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithHeader(pending.PairKey, value)
		pending.PairKey = ""
		m.openRequestHeadersEditor(pending)
		return m, nil
	case flowRequestParamAddKey:
		if value == "" {
			m.textInput.Message = "Parameter name is required"
			return m, nil
		}
		pending := *m.pending
		pending.PairKey = value
		m.openTextFlow(flowRequestParamAddValue, "Request params", "Param value", "", "(empty)", "Enter the query parameter value", pending)
		return m, nil
	case flowRequestParamAddValue:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithParam(pending.PairKey, value)
		pending.PairKey = ""
		m.openRequestParamsEditor(pending)
		return m, nil
	case flowRequestParamEditValue:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithParam(pending.PairKey, value)
		pending.PairKey = ""
		m.openRequestParamsEditor(pending)
		return m, nil
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
			pending.RequestDraft = pending.RequestDraft.WithMethod(method)
		}
		m.openRequestMethodSelect(pending)
		return m, nil
	case flowRequestEditMethod:
		if value == "" {
			m.textInput.Message = "HTTP method is required"
			return m, nil
		}
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(value))
		m.clearOverlays()
		m.session.Busy = true
		return m, loadRequestAuthChoicesCmd(m.ctx, m.client, pending.WorkspaceID, flowRequestEditAuthPick)
	case flowRequestEditAuth:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithAuth(value)
		return m.openRequestBodyTypeSelect(pending)
	case flowRequestEditBodyType:
		pending := *m.pending
		bodyType := normalizeBodyTypeForCLI(value)
		if bodyType == "" {
			bodyType = "none"
		}
		pending.RequestDraft = pending.RequestDraft.WithBody(bodyType, pending.RequestDraft.Body)
		if bodyType == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.openRequestHeadersEditor(pending)
			return m, nil
		}
		m.openTextFlow(flowRequestEditBody, "Edit request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowRequestEditBody:
		pending := *m.pending
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(pending.RequestDraft.BodyType), value)
		m.openRequestHeadersEditor(pending)
		return m, nil
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
	default:
		m.clearOverlays()
		return m, nil
	}
}

func (m *Model) acceptSelect(choice string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	pending := *m.pending
	switch pending.Flow {
	case flowRequestCreateMethodPick:
		if choice == requestMethodCustomChoice {
			m.openTextFlow(flowRequestCreateMethod, "Create request", "Method", pending.RequestDraft.Method, "GET", "Enter a custom HTTP method", pending)
			return m, nil
		}
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(choice))
		m.clearOverlays()
		m.session.Busy = true
		return m, loadRequestAuthChoicesCmd(m.ctx, m.client, pending.WorkspaceID, flowRequestCreateAuthPick)
	case flowRequestEditMethodPick:
		if choice == requestMethodCustomChoice {
			m.openTextFlow(flowRequestEditMethod, "Edit request", "Method", pending.RequestDraft.Method, "GET", "Enter a custom HTTP method", pending)
			return m, nil
		}
		pending.RequestDraft = pending.RequestDraft.WithMethod(strings.ToUpper(choice))
		m.clearOverlays()
		m.session.Busy = true
		return m, loadRequestAuthChoicesCmd(m.ctx, m.client, pending.WorkspaceID, flowRequestEditAuthPick)
	case flowRequestCreateAuthPick:
		pending.RequestDraft = pending.RequestDraft.WithAuth(choice)
		return m.openRequestBodyTypeSelect(pending)
	case flowRequestEditAuthPick:
		pending.RequestDraft = pending.RequestDraft.WithAuth(choice)
		return m.openRequestBodyTypeSelect(pending)
	case flowRequestCreateBodyPick:
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(choice), pending.RequestDraft.Body)
		if normalizeBodyTypeForCLI(choice) == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.openRequestHeadersEditor(pending)
			return m, nil
		}
		m.openTextFlow(flowRequestCreateBody, "Create request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowRequestEditBodyPick:
		pending.RequestDraft = pending.RequestDraft.WithBody(normalizeBodyTypeForCLI(choice), pending.RequestDraft.Body)
		if normalizeBodyTypeForCLI(choice) == "none" {
			pending.RequestDraft = pending.RequestDraft.WithBody("none", "")
			m.openRequestHeadersEditor(pending)
			return m, nil
		}
		m.openTextFlow(flowRequestEditBody, "Edit request", "Body", pending.RequestDraft.Body, "(optional)", "Enter the request body", pending)
		return m, nil
	case flowAuthTypePick:
		normalized, ok := normalizeAuthTypeInput(choice)
		if !ok {
			m.session.Message = "Unsupported auth type"
			m.clearOverlays()
			return m, nil
		}
		pending.AuthDraft.Type = normalized
		applyAuthTypeDefaults(&pending.AuthDraft)
		m.clearOverlays()
		return m, m.openNextAuthField(pending)
	case flowAuthGrantPick:
		grant, ok := normalizeGrantInput(choice)
		if !ok {
			m.session.Message = "Unsupported OAuth2 grant"
			m.clearOverlays()
			return m, nil
		}
		pending.AuthDraft.Grant = grant
		m.openTextFlow(flowAuthTokenURL, authTitle(pending), "Token URL", pending.AuthDraft.TokenURL, "https://auth.example.com/token", "Enter the OAuth2 token URL", pending)
		return m, nil
	case flowAuthPKCEPick:
		pkce, ok := normalizePKCEInput(choice)
		if !ok {
			m.session.Message = "Unsupported PKCE mode"
			m.clearOverlays()
			return m, nil
		}
		pending.AuthDraft.PKCE = pkce
		m.openTextFlow(flowAuthScope, authTitle(pending), "Scope", pending.AuthDraft.Scope, "read write", "Enter the OAuth2 scope", pending)
		return m, nil
	case flowAuthCustomBodyTypePick:
		pending.AuthDraft.CustomBodyType = normalizeBodyTypeForCLI(choice)
		m.openAuthExtractionSourceSelect(pending)
		return m, nil
	case flowAuthExtractionSourcePick:
		pending.AuthDraft.ExtractionSource = strings.ToLower(strings.TrimSpace(choice))
		m.openTextFlow(flowAuthExtractionExpr, authTitle(pending), "Extraction expression", pending.AuthDraft.ExtractionExpression, "access_token", "Enter the extraction expression", pending)
		return m, nil
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
	case "backspace":
		return dialogs.KeyBackspace, true
	case "delete":
		return dialogs.KeyDelete, true
	default:
		return "", false
	}
}

func isAuthFlow(flow pendingFlow) bool {
	switch flow {
	case flowAuthName,
		flowAuthType,
		flowAuthTypePick,
		flowAuthSecret,
		flowAuthPrefix,
		flowAuthUsername,
		flowAuthPassword,
		flowAuthGrant,
		flowAuthGrantPick,
		flowAuthTokenURL,
		flowAuthClientID,
		flowAuthClientSecret,
		flowAuthScope,
		flowAuthAuthorizationURL,
		flowAuthRedirectURI,
		flowAuthPKCE,
		flowAuthPKCEPick,
		flowAuthCustomURL,
		flowAuthCustomMethod,
		flowAuthCustomHeadersEditor,
		flowAuthCustomHeaderAddKey,
		flowAuthCustomHeaderAddValue,
		flowAuthCustomHeaderEditValue,
		flowAuthCustomParamsEditor,
		flowAuthCustomParamAddKey,
		flowAuthCustomParamAddValue,
		flowAuthCustomParamEditValue,
		flowAuthCustomBody,
		flowAuthCustomBodyType,
		flowAuthCustomBodyTypePick,
		flowAuthExtractionSource,
		flowAuthExtractionSourcePick,
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

func (m *Model) openRequestHeadersEditor(pending pendingAction) {
	m.openKeyValueFlow(
		flowRequestHeadersEditor,
		requestFlowTitle(pending),
		"Headers: j/k move  Enter edit value  c add  d delete  Esc continue",
		dialogPairsFromRequestPairs(pending.RequestDraft.Headers),
		pending,
	)
}

func (m *Model) openRequestParamsEditor(pending pendingAction) {
	m.openKeyValueFlow(
		flowRequestParamsEditor,
		requestFlowTitle(pending),
		"Params: j/k move  Enter edit value  c add  d delete  Esc finish",
		dialogPairsFromRequestPairs(pending.RequestDraft.Params),
		pending,
	)
}

func (m *Model) finalizeRequestDraft(pending pendingAction) (tea.Model, tea.Cmd) {
	m.clearOverlays()
	m.session.Busy = true

	if pending.Identifier != "" {
		return m, editRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.RequestDraft.MutationDraft())
	}

	return m, createRequestCmd(m.ctx, m.client, pending.WorkspaceID, pending.RequestDraft.MutationDraft())
}

func requestFlowTitle(pending pendingAction) string {
	if pending.Identifier != "" {
		return "Edit request"
	}
	return "Create request"
}

func dialogPairsFromRequestPairs(items []request.Pair) []dialogs.Pair {
	out := make([]dialogs.Pair, 0, len(items))
	for _, item := range items {
		out = append(out, dialogs.Pair{
			Key:   item.Key,
			Value: item.Value,
		})
	}
	return out
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
