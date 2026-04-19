package auth

import "strings"

// MutationDraft is the structured auth payload that the root layer can map to
// CLI arguments. The config is split by auth type so the shell can branch
// cleanly without reconstructing type-specific state from flat editor fields.
type MutationDraft struct {
	Name      string
	Type      string
	AutoRenew bool
	Config    AuthConfigDraft
}

type AuthConfigDraft interface {
	isAuthConfigDraft()
}

type UnknownConfigDraft struct {
	Type string
}

func (UnknownConfigDraft) isAuthConfigDraft() {}

type BearerConfigDraft struct {
	Secret string
	Prefix string
}

func (BearerConfigDraft) isAuthConfigDraft() {}

type BasicConfigDraft struct {
	Username string
	Password string
}

func (BasicConfigDraft) isAuthConfigDraft() {}

type OAuth2ConfigDraft struct {
	Grant            string
	TokenURL         string
	ClientID         string
	ClientSecret     string
	Scope            string
	AuthorizationURL string
	RedirectURI      string
	PKCE             string
}

func (OAuth2ConfigDraft) isAuthConfigDraft() {}

type CustomConfigDraft struct {
	URL                  string
	Method               string
	Headers              []Pair
	Params               []Pair
	Body                 string
	BodyType             string
	ExtractionSource     string
	ExtractionExpression string
	ApplyHeaderName      string
	ApplyHeaderTemplate  string
}

func (CustomConfigDraft) isAuthConfigDraft() {}

// Submission captures the structured snapshot produced by the editor.
type Submission struct {
	Mode  EditorMode
	Draft MutationDraft
}

func (d Draft) MutationDraft() MutationDraft {
	mut := MutationDraft{
		Name:      d.Name,
		Type:      d.Type,
		AutoRenew: d.AutoRenew,
	}

	switch normalizeType(d.Type) {
	case "bearer":
		mut.Config = BearerConfigDraft{
			Secret: d.Secret,
			Prefix: d.Prefix,
		}
	case "basic":
		mut.Config = BasicConfigDraft{
			Username: d.Username,
			Password: d.Password,
		}
	case "oauth2", "oauth2-client-credentials", "oauth2-authorization-code", "oauth2-password":
		mut.Config = OAuth2ConfigDraft{
			Grant:            d.Grant,
			TokenURL:         d.TokenURL,
			ClientID:         d.ClientID,
			ClientSecret:     d.ClientSecret,
			Scope:            d.Scope,
			AuthorizationURL: d.AuthorizationURL,
			RedirectURI:      d.RedirectURI,
			PKCE:             d.PKCE,
		}
	case "custom":
		mut.Config = CustomConfigDraft{
			URL:                  d.CustomURL,
			Method:               d.CustomMethod,
			Headers:              append([]Pair(nil), d.CustomHeaders...),
			Params:               append([]Pair(nil), d.CustomParams...),
			Body:                 d.CustomBody,
			BodyType:             d.CustomBodyType,
			ExtractionSource:     d.ExtractionSource,
			ExtractionExpression: d.ExtractionExpression,
			ApplyHeaderName:      d.ApplyHeaderName,
			ApplyHeaderTemplate:  d.ApplyHeaderTemplate,
		}
	default:
		mut.Config = UnknownConfigDraft{Type: d.Type}
	}

	return mut
}

func normalizeType(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
