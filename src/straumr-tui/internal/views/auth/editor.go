package auth

import (
	"fmt"
	"strings"
)

type EditorMode string

const (
	EditorModeCreate EditorMode = "create"
	EditorModeEdit   EditorMode = "edit"
)

type EditorField string

const (
	EditorFieldName                 EditorField = "name"
	EditorFieldType                 EditorField = "type"
	EditorFieldSecret               EditorField = "secret"
	EditorFieldPrefix               EditorField = "prefix"
	EditorFieldUsername             EditorField = "username"
	EditorFieldPassword             EditorField = "password"
	EditorFieldGrant                EditorField = "grant"
	EditorFieldTokenURL             EditorField = "token-url"
	EditorFieldClientID             EditorField = "client-id"
	EditorFieldClientSecret         EditorField = "client-secret"
	EditorFieldScope                EditorField = "scope"
	EditorFieldAuthorizationURL     EditorField = "authorization-url"
	EditorFieldRedirectURI          EditorField = "redirect-uri"
	EditorFieldPKCE                 EditorField = "pkce"
	EditorFieldCustomURL            EditorField = "custom-url"
	EditorFieldCustomMethod         EditorField = "custom-method"
	EditorFieldCustomHeaders        EditorField = "custom-headers"
	EditorFieldCustomParams         EditorField = "custom-params"
	EditorFieldCustomBody           EditorField = "custom-body"
	EditorFieldCustomBodyType       EditorField = "custom-body-type"
	EditorFieldExtractionSource     EditorField = "extraction-source"
	EditorFieldExtractionExpression EditorField = "extraction-expression"
	EditorFieldApplyHeaderName      EditorField = "apply-header-name"
	EditorFieldApplyHeaderTemplate  EditorField = "apply-header-template"
	EditorFieldAutoRenew            EditorField = "auto-renew"
)

type Pair struct {
	Key   string
	Value string
}

type Draft struct {
	Name                 string
	Type                 string
	Secret               string
	Prefix               string
	Username             string
	Password             string
	Grant                string
	TokenURL             string
	ClientID             string
	ClientSecret         string
	Scope                string
	AuthorizationURL     string
	RedirectURI          string
	PKCE                 string
	CustomURL            string
	CustomMethod         string
	CustomHeaders        []Pair
	CustomParams         []Pair
	CustomBody           string
	CustomBodyType       string
	ExtractionSource     string
	ExtractionExpression string
	ApplyHeaderName      string
	ApplyHeaderTemplate  string
	AutoRenew            bool
}

type EditorActionKind string

const (
	EditorActionNone   EditorActionKind = ""
	EditorActionMove   EditorActionKind = "move"
	EditorActionSubmit EditorActionKind = "submit"
	EditorActionCancel EditorActionKind = "cancel"
)

type EditorAction struct {
	Kind  EditorActionKind
	Field EditorField
}

type EditorView struct {
	Active  bool
	Mode    EditorMode
	Focus   EditorField
	Draft   Draft
	Message string
}

func (v *EditorView) Render() string {
	var b strings.Builder

	title := "Auth editor"
	if v.Mode != "" {
		title = fmt.Sprintf("%s (%s)", title, v.Mode)
	}

	b.WriteString(title)
	b.WriteString("\n")
	if v.Message != "" {
		b.WriteString(v.Message)
		b.WriteString("\n")
	}

	for _, line := range v.renderLines() {
		b.WriteString(line)
		b.WriteString("\n")
	}

	b.WriteString("Enter submit  Esc cancel  j/k or Tab/Shift+Tab move")
	return strings.TrimRight(b.String(), "\n")
}

func (v *EditorView) HandleKey(key Key) EditorAction {
	switch key {
	case KeyCancel:
		return EditorAction{Kind: EditorActionCancel, Field: v.Focus}
	case KeyEnter:
		return EditorAction{Kind: EditorActionSubmit, Field: v.Focus}
	case KeyUp:
		v.move(-1)
		return EditorAction{Kind: EditorActionMove, Field: v.Focus}
	case KeyDown:
		v.move(1)
		return EditorAction{Kind: EditorActionMove, Field: v.Focus}
	default:
		return EditorAction{Kind: EditorActionNone, Field: v.Focus}
	}
}

func (v *EditorView) move(delta int) {
	order := []EditorField{
		EditorFieldName,
		EditorFieldType,
		EditorFieldSecret,
		EditorFieldPrefix,
		EditorFieldUsername,
		EditorFieldPassword,
		EditorFieldGrant,
		EditorFieldTokenURL,
		EditorFieldClientID,
		EditorFieldClientSecret,
		EditorFieldScope,
		EditorFieldAuthorizationURL,
		EditorFieldRedirectURI,
		EditorFieldPKCE,
		EditorFieldCustomURL,
		EditorFieldCustomMethod,
		EditorFieldCustomHeaders,
		EditorFieldCustomParams,
		EditorFieldCustomBody,
		EditorFieldCustomBodyType,
		EditorFieldExtractionSource,
		EditorFieldExtractionExpression,
		EditorFieldApplyHeaderName,
		EditorFieldApplyHeaderTemplate,
		EditorFieldAutoRenew,
	}

	index := 0
	for i, field := range order {
		if field == v.Focus {
			index = i
			break
		}
	}

	index += delta
	if index < 0 {
		index = 0
	}
	if index >= len(order) {
		index = len(order) - 1
	}

	v.Focus = order[index]
}

func (v *EditorView) renderLines() []string {
	lines := []string{
		editorLine(v.Focus == EditorFieldName, "Name", v.Draft.Name),
		editorLine(v.Focus == EditorFieldType, "Type", v.Draft.Type),
		editorLine(v.Focus == EditorFieldSecret, "Secret", masked(v.Draft.Secret)),
		editorLine(v.Focus == EditorFieldPrefix, "Prefix", v.Draft.Prefix),
		editorLine(v.Focus == EditorFieldUsername, "Username", v.Draft.Username),
		editorLine(v.Focus == EditorFieldPassword, "Password", masked(v.Draft.Password)),
		editorLine(v.Focus == EditorFieldGrant, "Grant", v.Draft.Grant),
		editorLine(v.Focus == EditorFieldTokenURL, "Token URL", v.Draft.TokenURL),
		editorLine(v.Focus == EditorFieldClientID, "Client ID", v.Draft.ClientID),
		editorLine(v.Focus == EditorFieldClientSecret, "Client Secret", masked(v.Draft.ClientSecret)),
		editorLine(v.Focus == EditorFieldScope, "Scope", v.Draft.Scope),
		editorLine(v.Focus == EditorFieldAuthorizationURL, "Authorization URL", v.Draft.AuthorizationURL),
		editorLine(v.Focus == EditorFieldRedirectURI, "Redirect URI", v.Draft.RedirectURI),
		editorLine(v.Focus == EditorFieldPKCE, "PKCE", v.Draft.PKCE),
		editorLine(v.Focus == EditorFieldCustomURL, "Custom URL", v.Draft.CustomURL),
		editorLine(v.Focus == EditorFieldCustomMethod, "Custom Method", v.Draft.CustomMethod),
		editorLine(v.Focus == EditorFieldCustomHeaders, "Custom Headers", fmt.Sprintf("%d entries", len(v.Draft.CustomHeaders))),
		editorLine(v.Focus == EditorFieldCustomParams, "Custom Params", fmt.Sprintf("%d entries", len(v.Draft.CustomParams))),
		editorLine(v.Focus == EditorFieldCustomBody, "Custom Body", summary(v.Draft.CustomBody)),
		editorLine(v.Focus == EditorFieldCustomBodyType, "Custom Body Type", v.Draft.CustomBodyType),
		editorLine(v.Focus == EditorFieldExtractionSource, "Extraction Source", v.Draft.ExtractionSource),
		editorLine(v.Focus == EditorFieldExtractionExpression, "Extraction Expression", v.Draft.ExtractionExpression),
		editorLine(v.Focus == EditorFieldApplyHeaderName, "Apply Header Name", v.Draft.ApplyHeaderName),
		editorLine(v.Focus == EditorFieldApplyHeaderTemplate, "Apply Header Template", v.Draft.ApplyHeaderTemplate),
		editorLine(v.Focus == EditorFieldAutoRenew, "Auto Renew", boolText(v.Draft.AutoRenew)),
	}

	return lines
}

func editorLine(selected bool, label, value string) string {
	marker := "  "
	if selected {
		marker = "> "
	}

	if value == "" {
		value = "(empty)"
	}

	return fmt.Sprintf("%s%s: %s", marker, label, value)
}

func masked(value string) string {
	if value == "" {
		return "(empty)"
	}

	return "******"
}

func summary(value string) string {
	if value == "" {
		return "(empty)"
	}

	return fmt.Sprintf("%d chars", len(value))
}
