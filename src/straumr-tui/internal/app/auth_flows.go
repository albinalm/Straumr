package app

import (
	"encoding/json"
	"strings"

	"straumr-tui/internal/cli"
	"straumr-tui/internal/views/auth"
	"straumr-tui/internal/views/dialogs"

	tea "github.com/charmbracelet/bubbletea"
)

func (m *Model) advanceAuthTextFlow(value string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	pending := *m.pending

	switch pending.Flow {
	case flowAuthName:
		if value == "" {
			m.textInput.Message = "Auth name is required"
			return m, nil
		}
		pending.AuthDraft.Name = value
		m.openAuthTypeSelect(pending)
		return m, nil
	case flowAuthPrefix:
		pending.AuthDraft.Prefix = value
		return m, m.openAuthAutoRenewConfirm(pending)
	case flowAuthUsername:
		pending.AuthDraft.Username = value
		if isBasicAuthType(pending.AuthDraft.Type) || isOAuth2PasswordType(pending.AuthDraft.Type, pending.AuthDraft.Grant) {
			m.openSecretFlow(flowAuthPassword, authTitle(pending), "Password", pending.AuthDraft.Password, "password", "Enter the password", pending)
			return m, nil
		}
		return m, nil
	case flowAuthTokenURL:
		pending.AuthDraft.TokenURL = value
		m.openTextFlow(flowAuthClientID, authTitle(pending), "Client ID", pending.AuthDraft.ClientID, "client-id", "Enter the OAuth2 client ID", pending)
		return m, nil
	case flowAuthClientID:
		pending.AuthDraft.ClientID = value
		m.openSecretFlow(flowAuthClientSecret, authTitle(pending), "Client Secret", pending.AuthDraft.ClientSecret, "client secret", "Enter the OAuth2 client secret", pending)
		return m, nil
	case flowAuthScope:
		pending.AuthDraft.Scope = value
		return m, m.openAuthAutoRenewConfirm(pending)
	case flowAuthAuthorizationURL:
		pending.AuthDraft.AuthorizationURL = value
		m.openTextFlow(flowAuthRedirectURI, authTitle(pending), "Redirect URI", pending.AuthDraft.RedirectURI, "http://localhost:8765/callback", "Enter the redirect URI", pending)
		return m, nil
	case flowAuthRedirectURI:
		pending.AuthDraft.RedirectURI = value
		m.openAuthPKCESelect(pending)
		return m, nil
	case flowAuthCustomURL:
		pending.AuthDraft.CustomURL = value
		m.openTextFlow(flowAuthCustomMethod, authTitle(pending), "Method", pending.AuthDraft.CustomMethod, "POST", "Enter the custom auth request method", pending)
		return m, nil
	case flowAuthCustomMethod:
		pending.AuthDraft.CustomMethod = strings.ToUpper(value)
		m.openAuthCustomHeadersEditor(pending)
		return m, nil
	case flowAuthCustomHeaderAddKey:
		if value == "" {
			m.textInput.Message = "Header name is required"
			return m, nil
		}
		pending.PairKey = value
		m.openTextFlow(flowAuthCustomHeaderAddValue, authTitle(pending), "Header value", "", "(empty)", "Enter the custom auth header value", pending)
		return m, nil
	case flowAuthCustomHeaderAddValue:
		pending.AuthDraft.CustomHeaders = upsertAuthPair(pending.AuthDraft.CustomHeaders, pending.PairKey, value)
		pending.PairKey = ""
		m.openAuthCustomHeadersEditor(pending)
		return m, nil
	case flowAuthCustomHeaderEditValue:
		pending.AuthDraft.CustomHeaders = upsertAuthPair(pending.AuthDraft.CustomHeaders, pending.PairKey, value)
		pending.PairKey = ""
		m.openAuthCustomHeadersEditor(pending)
		return m, nil
	case flowAuthCustomParamAddKey:
		if value == "" {
			m.textInput.Message = "Parameter name is required"
			return m, nil
		}
		pending.PairKey = value
		m.openTextFlow(flowAuthCustomParamAddValue, authTitle(pending), "Param value", "", "(empty)", "Enter the custom auth query parameter value", pending)
		return m, nil
	case flowAuthCustomParamAddValue:
		pending.AuthDraft.CustomParams = upsertAuthPair(pending.AuthDraft.CustomParams, pending.PairKey, value)
		pending.PairKey = ""
		m.openAuthCustomParamsEditor(pending)
		return m, nil
	case flowAuthCustomParamEditValue:
		pending.AuthDraft.CustomParams = upsertAuthPair(pending.AuthDraft.CustomParams, pending.PairKey, value)
		pending.PairKey = ""
		m.openAuthCustomParamsEditor(pending)
		return m, nil
	case flowAuthCustomBody:
		pending.AuthDraft.CustomBody = value
		m.openAuthCustomBodyTypeSelect(pending)
		return m, nil
	case flowAuthExtractionExpr:
		pending.AuthDraft.ExtractionExpression = value
		m.openTextFlow(flowAuthApplyHeaderName, authTitle(pending), "Apply header name", pending.AuthDraft.ApplyHeaderName, "Authorization", "Enter the header name to apply", pending)
		return m, nil
	case flowAuthApplyHeaderName:
		pending.AuthDraft.ApplyHeaderName = value
		m.openTextFlow(flowAuthApplyHeaderTpl, authTitle(pending), "Apply header template", pending.AuthDraft.ApplyHeaderTemplate, "Bearer {{value}}", "Enter the header template", pending)
		return m, nil
	case flowAuthApplyHeaderTpl:
		pending.AuthDraft.ApplyHeaderTemplate = value
		return m, m.openAuthAutoRenewConfirm(pending)
	default:
		m.clearOverlays()
		return m, nil
	}
}

func (m *Model) advanceAuthSecretFlow(value string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	pending := *m.pending

	switch pending.Flow {
	case flowAuthSecret:
		pending.AuthDraft.Secret = value
		if strings.TrimSpace(pending.AuthDraft.Prefix) == "" {
			pending.AuthDraft.Prefix = "Bearer"
		}
		m.openTextFlow(flowAuthPrefix, authTitle(pending), "Prefix", pending.AuthDraft.Prefix, "Bearer", "Enter the bearer prefix", pending)
		return m, nil
	case flowAuthPassword:
		pending.AuthDraft.Password = value
		if isBasicAuthType(pending.AuthDraft.Type) {
			return m, m.openAuthAutoRenewConfirm(pending)
		}
		m.openTextFlow(flowAuthScope, authTitle(pending), "Scope", pending.AuthDraft.Scope, "read write", "Enter the OAuth2 scope", pending)
		return m, nil
	case flowAuthClientSecret:
		pending.AuthDraft.ClientSecret = value
		switch {
		case isOAuth2AuthorizationCodeType(pending.AuthDraft.Type, pending.AuthDraft.Grant):
			m.openTextFlow(flowAuthAuthorizationURL, authTitle(pending), "Authorization URL", pending.AuthDraft.AuthorizationURL, "https://auth.example.com/authorize", "Enter the authorization URL", pending)
			return m, nil
		case isOAuth2PasswordType(pending.AuthDraft.Type, pending.AuthDraft.Grant):
			m.openTextFlow(flowAuthUsername, authTitle(pending), "Username", pending.AuthDraft.Username, "username", "Enter the resource owner username", pending)
			return m, nil
		default:
			m.openTextFlow(flowAuthScope, authTitle(pending), "Scope", pending.AuthDraft.Scope, "read write", "Enter the OAuth2 scope", pending)
			return m, nil
		}
	default:
		m.clearOverlays()
		return m, nil
	}
}

func (m *Model) advanceAuthConfirmFlow(choice string) (tea.Model, tea.Cmd) {
	if m.pending == nil {
		m.clearOverlays()
		return m, nil
	}

	pending := *m.pending

	switch pending.Flow {
	case flowAuthAutoRenew:
		pending.AuthDraft.AutoRenew = choice == "on"
		m.clearOverlays()
		m.session.Busy = true
		if pending.AuthMode == auth.EditorModeEdit {
			return m, editAuthCmd(m.ctx, m.client, pending.WorkspaceID, pending.Identifier, pending.AuthDraft.MutationDraft())
		}
		return m, createAuthCmd(m.ctx, m.client, pending.WorkspaceID, pending.AuthDraft.MutationDraft())
	default:
		return m, nil
	}
}

func (m *Model) openNextAuthField(pending pendingAction) tea.Cmd {
	switch normalizeAuthTypeValue(pending.AuthDraft.Type) {
	case "bearer":
		m.openSecretFlow(flowAuthSecret, authTitle(pending), "Secret", pending.AuthDraft.Secret, "token", "Enter the bearer token", pending)
	case "basic":
		m.openTextFlow(flowAuthUsername, authTitle(pending), "Username", pending.AuthDraft.Username, "username", "Enter the username", pending)
	case "oauth2":
		m.openAuthGrantSelect(pending)
	case "oauth2-client-credentials", "oauth2-authorization-code", "oauth2-password":
		m.openTextFlow(flowAuthTokenURL, authTitle(pending), "Token URL", pending.AuthDraft.TokenURL, "https://auth.example.com/token", "Enter the OAuth2 token URL", pending)
	case "custom":
		m.openTextFlow(flowAuthCustomURL, authTitle(pending), "Custom URL", pending.AuthDraft.CustomURL, "https://api.example.com/login", "Enter the custom auth request URL", pending)
	default:
		m.openAuthTypeSelect(pending)
	}
	return nil
}

func (m *Model) openAuthAutoRenewConfirm(pending pendingAction) tea.Cmd {
	options := []string{"On", "Off"}
	if !pending.AuthDraft.AutoRenew {
		options = []string{"Off", "On"}
	}
	m.openConfirmFlow(flowAuthAutoRenew, authTitle(pending), "Auto renew auth?", options, pending)
	return nil
}

func (m *Model) openAuthCustomHeadersEditor(pending pendingAction) {
	m.openKeyValueFlow(
		flowAuthCustomHeadersEditor,
		authTitle(pending),
		"Custom headers: j/k move  Enter edit entry  c add  d delete  Esc continue",
		dialogPairsFromAuthPairs(pending.AuthDraft.CustomHeaders),
		pending,
	)
}

func (m *Model) openAuthCustomParamsEditor(pending pendingAction) {
	m.openKeyValueFlow(
		flowAuthCustomParamsEditor,
		authTitle(pending),
		"Custom params: j/k move  Enter edit entry  c add  d delete  Esc continue",
		dialogPairsFromAuthPairs(pending.AuthDraft.CustomParams),
		pending,
	)
}

func authTitle(pending pendingAction) string {
	if pending.AuthMode == auth.EditorModeEdit {
		return "Edit auth"
	}
	return "Create auth"
}

func (m *Model) openAuthTypeSelect(pending pendingAction) {
	m.openSelectFlow(
		flowAuthTypePick,
		authTitle(pending),
		"Choose the auth type",
		"Enter choose  Esc cancel",
		authChoiceItems(authTypeChoices, pending.AuthDraft.Type, "bearer"),
		pending,
	)
}

func (m *Model) openAuthGrantSelect(pending pendingAction) {
	m.openSelectFlow(
		flowAuthGrantPick,
		authTitle(pending),
		"Choose the OAuth2 grant",
		"Enter choose  Esc cancel",
		authChoiceItems(authGrantChoices, pending.AuthDraft.Grant, "client-credentials"),
		pending,
	)
}

func (m *Model) openAuthPKCESelect(pending pendingAction) {
	m.openSelectFlow(
		flowAuthPKCEPick,
		authTitle(pending),
		"Choose the PKCE mode",
		"Enter choose  Esc cancel",
		authChoiceItems(authPKCEChoices, pending.AuthDraft.PKCE, "S256"),
		pending,
	)
}

func (m *Model) openAuthCustomBodyTypeSelect(pending pendingAction) {
	m.openSelectFlow(
		flowAuthCustomBodyTypePick,
		authTitle(pending),
		"Choose the custom auth body type",
		"Enter choose  Esc cancel",
		authChoiceItems(authBodyTypeChoices, pending.AuthDraft.CustomBodyType, "json"),
		pending,
	)
}

func (m *Model) openAuthExtractionSourceSelect(pending pendingAction) {
	m.openSelectFlow(
		flowAuthExtractionSourcePick,
		authTitle(pending),
		"Choose the token extraction source",
		"Enter choose  Esc cancel",
		authChoiceItems(authExtractionSourceChoices, pending.AuthDraft.ExtractionSource, "jsonpath"),
		pending,
	)
}

func normalizeAuthTypeInput(value string) (string, bool) {
	switch normalizeAuthTypeValue(value) {
	case "bearer", "basic", "oauth2", "oauth2-client-credentials", "oauth2-authorization-code", "oauth2-password", "custom":
		return normalizeAuthTypeValue(value), true
	default:
		return "", false
	}
}

func normalizeAuthTypeValue(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}

func normalizeGrantInput(value string) (string, bool) {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "clientcredentials", "client-credentials":
		return "client-credentials", true
	case "authorizationcode", "authorization-code":
		return "authorization-code", true
	case "password", "resourceownerpassword", "resource-owner-password":
		return "password", true
	default:
		return "", false
	}
}

func normalizePKCEInput(value string) (string, bool) {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "", "s256":
		return "S256", true
	case "plain":
		return "plain", true
	case "disabled":
		return "disabled", true
	default:
		return "", false
	}
}

func applyAuthTypeDefaults(draft *auth.Draft) {
	switch normalizeAuthTypeValue(draft.Type) {
	case "bearer":
		if strings.TrimSpace(draft.Prefix) == "" {
			draft.Prefix = "Bearer"
		}
	case "oauth2-client-credentials":
		draft.Grant = "client-credentials"
	case "oauth2-authorization-code":
		draft.Grant = "authorization-code"
		if strings.TrimSpace(draft.RedirectURI) == "" {
			draft.RedirectURI = "http://localhost:8765/callback"
		}
		if strings.TrimSpace(draft.PKCE) == "" {
			draft.PKCE = "S256"
		}
	case "oauth2-password":
		draft.Grant = "password"
	case "custom":
		if strings.TrimSpace(draft.CustomMethod) == "" {
			draft.CustomMethod = "POST"
		}
		if strings.TrimSpace(draft.CustomBodyType) == "" {
			draft.CustomBodyType = "json"
		}
		if strings.TrimSpace(draft.ExtractionSource) == "" {
			draft.ExtractionSource = "jsonpath"
		}
		if strings.TrimSpace(draft.ApplyHeaderName) == "" {
			draft.ApplyHeaderName = "Authorization"
		}
		if strings.TrimSpace(draft.ApplyHeaderTemplate) == "" {
			draft.ApplyHeaderTemplate = "Bearer {{value}}"
		}
	}
}

func isBasicAuthType(value string) bool {
	return normalizeAuthTypeValue(value) == "basic"
}

func isOAuth2AuthorizationCodeType(typ, grant string) bool {
	typ = normalizeAuthTypeValue(typ)
	grant = strings.ToLower(strings.TrimSpace(grant))
	return typ == "oauth2-authorization-code" || (typ == "oauth2" && grant == "authorization-code")
}

func isOAuth2PasswordType(typ, grant string) bool {
	typ = normalizeAuthTypeValue(typ)
	grant = strings.ToLower(strings.TrimSpace(grant))
	return typ == "oauth2-password" || (typ == "oauth2" && grant == "password")
}

func authDraftFromResult(item cli.AuthGetResult) (auth.Draft, error) {
	var base struct {
		AuthType string `json:"AuthType"`
	}
	if err := json.Unmarshal(item.Config, &base); err != nil {
		return auth.Draft{}, err
	}

	draft := auth.Draft{
		Name:      item.Name,
		AutoRenew: item.AutoRenewAuth,
	}

	switch base.AuthType {
	case "Bearer":
		var config struct {
			Token  string `json:"Token"`
			Prefix string `json:"Prefix"`
		}
		if err := json.Unmarshal(item.Config, &config); err != nil {
			return auth.Draft{}, err
		}
		draft.Type = "bearer"
		draft.Secret = config.Token
		draft.Prefix = config.Prefix
	case "Basic":
		var config struct {
			Username string `json:"Username"`
			Password string `json:"Password"`
		}
		if err := json.Unmarshal(item.Config, &config); err != nil {
			return auth.Draft{}, err
		}
		draft.Type = "basic"
		draft.Username = config.Username
		draft.Password = config.Password
	case "OAuth2":
		var config struct {
			GrantType        string `json:"GrantType"`
			TokenURL         string `json:"TokenUrl"`
			ClientID         string `json:"ClientId"`
			ClientSecret     string `json:"ClientSecret"`
			Scope            string `json:"Scope"`
			AuthorizationURL string `json:"AuthorizationUrl"`
			RedirectURI      string `json:"RedirectUri"`
			UsePKCE          bool   `json:"UsePkce"`
			CodeChallenge    string `json:"CodeChallengeMethod"`
			Username         string `json:"Username"`
			Password         string `json:"Password"`
		}
		if err := json.Unmarshal(item.Config, &config); err != nil {
			return auth.Draft{}, err
		}
		draft.Type = oauthTypeFromGrant(config.GrantType)
		draft.Grant = oauthGrantFromRaw(config.GrantType)
		draft.TokenURL = config.TokenURL
		draft.ClientID = config.ClientID
		draft.ClientSecret = config.ClientSecret
		draft.Scope = config.Scope
		draft.AuthorizationURL = config.AuthorizationURL
		draft.RedirectURI = config.RedirectURI
		draft.Username = config.Username
		draft.Password = config.Password
		if !config.UsePKCE {
			draft.PKCE = "disabled"
		} else if strings.TrimSpace(config.CodeChallenge) == "" {
			draft.PKCE = "S256"
		} else {
			draft.PKCE = config.CodeChallenge
		}
	case "Custom":
		var config struct {
			URL                  string            `json:"Url"`
			Method               string            `json:"Method"`
			BodyType             string            `json:"BodyType"`
			Bodies               map[string]string `json:"Bodies"`
			Headers              map[string]string `json:"Headers"`
			Params               map[string]string `json:"Params"`
			Source               string            `json:"Source"`
			ExtractionExpression string            `json:"ExtractionExpression"`
			ApplyHeaderName      string            `json:"ApplyHeaderName"`
			ApplyHeaderTemplate  string            `json:"ApplyHeaderTemplate"`
		}
		if err := json.Unmarshal(item.Config, &config); err != nil {
			return auth.Draft{}, err
		}
		draft.Type = "custom"
		draft.CustomURL = config.URL
		draft.CustomMethod = config.Method
		draft.CustomBodyType = normalizeBodyTypeForCLI(config.BodyType)
		draft.CustomBody = resolveBodyForType(config.BodyType, config.Bodies)
		draft.ExtractionSource = strings.ToLower(strings.TrimSpace(config.Source))
		draft.ExtractionExpression = config.ExtractionExpression
		draft.ApplyHeaderName = config.ApplyHeaderName
		draft.ApplyHeaderTemplate = config.ApplyHeaderTemplate
		draft.CustomHeaders = authPairsFromMap(config.Headers)
		draft.CustomParams = authPairsFromMap(config.Params)
	default:
		draft.Type = strings.ToLower(base.AuthType)
	}

	applyAuthTypeDefaults(&draft)
	return draft, nil
}

func oauthTypeFromGrant(grant string) string {
	switch oauthGrantFromRaw(grant) {
	case "client-credentials":
		return "oauth2-client-credentials"
	case "authorization-code":
		return "oauth2-authorization-code"
	case "password":
		return "oauth2-password"
	default:
		return "oauth2"
	}
}

func oauthGrantFromRaw(grant string) string {
	switch strings.ToLower(strings.TrimSpace(grant)) {
	case "clientcredentials", "client-credentials":
		return "client-credentials"
	case "authorizationcode", "authorization-code":
		return "authorization-code"
	case "resourceownerpassword", "password":
		return "password"
	default:
		return strings.ToLower(strings.TrimSpace(grant))
	}
}

func authPairsFromMap(values map[string]string) []auth.Pair {
	pairs := make([]auth.Pair, 0, len(values))
	for key, value := range values {
		pairs = append(pairs, auth.Pair{Key: key, Value: value})
	}
	return pairs
}

func dialogPairsFromAuthPairs(items []auth.Pair) []dialogs.Pair {
	out := make([]dialogs.Pair, 0, len(items))
	for _, item := range items {
		out = append(out, dialogs.Pair{
			Key:   item.Key,
			Value: item.Value,
		})
	}
	return out
}

func upsertAuthPair(items []auth.Pair, key, value string) []auth.Pair {
	for i := range items {
		if items[i].Key == key {
			items[i].Value = value
			return items
		}
	}
	return append(items, auth.Pair{Key: key, Value: value})
}

func withoutAuthPair(items []auth.Pair, key string) []auth.Pair {
	out := items[:0]
	for _, item := range items {
		if item.Key == key {
			continue
		}
		out = append(out, item)
	}
	return out
}

func resolveBodyForType(bodyType string, bodies map[string]string) string {
	if len(bodies) == 0 {
		return ""
	}

	target := strings.ToLower(strings.TrimSpace(bodyType))
	for key, value := range bodies {
		if strings.ToLower(strings.TrimSpace(key)) == target {
			return value
		}
	}
	for _, value := range bodies {
		return value
	}
	return ""
}

var authTypeChoices = []pickerChoice{
	{Key: "bearer", Title: "Bearer", Description: "Static bearer token with configurable prefix"},
	{Key: "basic", Title: "Basic", Description: "Username and password credentials"},
	{Key: "oauth2", Title: "OAuth2", Description: "Choose the grant in the next step"},
	{Key: "oauth2-client-credentials", Title: "OAuth2 Client Credentials", Description: "Client credentials grant with token URL"},
	{Key: "oauth2-authorization-code", Title: "OAuth2 Authorization Code", Description: "Authorization code grant with redirect and PKCE"},
	{Key: "oauth2-password", Title: "OAuth2 Password", Description: "Resource owner password grant"},
	{Key: "custom", Title: "Custom", Description: "Custom request and token extraction rules"},
}

var authGrantChoices = []pickerChoice{
	{Key: "client-credentials", Title: "Client credentials", Description: "Use client ID and client secret to fetch a token"},
	{Key: "authorization-code", Title: "Authorization code", Description: "Use authorization URL, redirect URI, and PKCE"},
	{Key: "password", Title: "Password", Description: "Use username and password with OAuth2 token endpoint"},
}

var authPKCEChoices = []pickerChoice{
	{Key: "S256", Title: "S256", Description: "Recommended PKCE challenge method"},
	{Key: "plain", Title: "Plain", Description: "Use plain-text PKCE challenge"},
	{Key: "disabled", Title: "Disabled", Description: "Do not use PKCE"},
}

var authBodyTypeChoices = requestBodyTypeChoices

var authExtractionSourceChoices = []pickerChoice{
	{Key: "jsonpath", Title: "JSONPath", Description: "Extract the token from a JSON body"},
	{Key: "header", Title: "Header", Description: "Extract the token from a response header"},
	{Key: "regex", Title: "Regex", Description: "Extract the token using a regular expression"},
}

func authChoiceItems(choices []pickerChoice, currentValue, fallbackValue string) []dialogs.Choice {
	current := strings.TrimSpace(currentValue)
	if current == "" {
		current = fallbackValue
	}

	items := make([]dialogs.Choice, 0, len(choices))
	appendChoice := func(match string) {
		for _, choice := range choices {
			if strings.EqualFold(choice.Key, match) {
				items = append(items, dialogs.Choice{
					Key:         choice.Key,
					Title:       choice.Title,
					Description: choice.Description,
				})
				return
			}
		}
	}

	appendChoice(current)
	for _, choice := range choices {
		if strings.EqualFold(choice.Key, current) {
			continue
		}
		items = append(items, dialogs.Choice{
			Key:         choice.Key,
			Title:       choice.Title,
			Description: choice.Description,
		})
	}
	return items
}
