package cli

import (
	"encoding/json"
	"time"
)

type WorkspaceSummary struct {
	ID           string     `json:"Id"`
	Name         string     `json:"Name"`
	Path         string     `json:"Path"`
	IsCurrent    bool       `json:"IsCurrent"`
	Requests     *int       `json:"Requests,omitempty"`
	Secrets      *int       `json:"Secrets,omitempty"`
	Auths        *int       `json:"Auths,omitempty"`
	Status       string     `json:"Status,omitempty"`
	LastAccessed *time.Time `json:"LastAccessed,omitempty"`
	Damaged      bool       `json:"Damaged,omitempty"`
	Missing      bool       `json:"Missing,omitempty"`
}

type RequestSummary struct {
	ID           string     `json:"Id"`
	Name         string     `json:"Name"`
	Method       string     `json:"Method"`
	Host         string     `json:"Host,omitempty"`
	URI          string     `json:"Uri,omitempty"`
	BodyType     string     `json:"BodyType,omitempty"`
	Auth         string     `json:"Auth,omitempty"`
	Status       string     `json:"Status,omitempty"`
	LastAccessed *time.Time `json:"LastAccessed,omitempty"`
	Current      bool       `json:"Current,omitempty"`
	Damaged      bool       `json:"Damaged,omitempty"`
	Missing      bool       `json:"Missing,omitempty"`
}

type AuthSummary struct {
	ID           string     `json:"Id"`
	Name         string     `json:"Name"`
	Type         string     `json:"Type"`
	AutoRenew    bool       `json:"AutoRenew,omitempty"`
	LastAccessed *time.Time `json:"LastAccessed,omitempty"`
	Current      bool       `json:"Current,omitempty"`
	Damaged      bool       `json:"Damaged,omitempty"`
	Missing      bool       `json:"Missing,omitempty"`
}

type SecretSummary struct {
	ID           string     `json:"Id"`
	Name         string     `json:"Name"`
	Status       string     `json:"Status"`
	LastAccessed *time.Time `json:"LastAccessed,omitempty"`
	Current      bool       `json:"Current,omitempty"`
	Damaged      bool       `json:"Damaged,omitempty"`
	Missing      bool       `json:"Missing,omitempty"`
}

type WorkspaceCreateResult struct {
	ID   string `json:"Id"`
	Name string `json:"Name"`
	Path string `json:"Path"`
}

type WorkspaceExportResult struct {
	Path string `json:"Path"`
}

type WorkspaceGetResult struct {
	ID           string    `json:"Id"`
	Name         string    `json:"Name"`
	Requests     []string  `json:"Requests,omitempty"`
	Secrets      []string  `json:"Secrets,omitempty"`
	Auths        []string  `json:"Auths,omitempty"`
	Modified     time.Time `json:"Modified"`
	LastAccessed time.Time `json:"LastAccessed"`
}

type RequestCreateResult struct {
	ID     string `json:"Id"`
	Name   string `json:"Name"`
	Method string `json:"Method"`
	Uri    string `json:"Uri"`
}

type RequestGetResult struct {
	ID           string            `json:"Id"`
	Name         string            `json:"Name"`
	Method       string            `json:"Method"`
	Uri          string            `json:"Uri"`
	BodyType     string            `json:"BodyType"`
	Headers      map[string]string `json:"Headers"`
	Params       map[string]string `json:"Params"`
	Body         *string           `json:"Body"`
	AuthID       *string           `json:"AuthId"`
	LastAccessed string            `json:"LastAccessed"`
	Modified     string            `json:"Modified"`
}

type AuthListItem struct {
	ID   string `json:"Id"`
	Name string `json:"Name"`
	Type string `json:"Type"`
}

type AuthGetResult struct {
	ID            string          `json:"Id"`
	Name          string          `json:"Name"`
	Config        json.RawMessage `json:"Config"`
	AutoRenewAuth bool            `json:"AutoRenewAuth"`
	Modified      time.Time       `json:"Modified"`
	LastAccessed  time.Time       `json:"LastAccessed"`
}

type SecretListItem struct {
	ID     string `json:"Id"`
	Name   string `json:"Name"`
	Status string `json:"Status"`
}

type SecretDeleteResult struct {
	ID   string `json:"Id"`
	Name string `json:"Name"`
}

type SecretGetResult struct {
	ID           string    `json:"Id"`
	Name         string    `json:"Name"`
	Value        string    `json:"Value"`
	Modified     time.Time `json:"Modified"`
	LastAccessed time.Time `json:"LastAccessed"`
}

type DryRunResult struct {
	Method   string            `json:"Method"`
	Uri      string            `json:"Uri"`
	Auth     *string           `json:"Auth"`
	Headers  map[string]string `json:"Headers"`
	Params   map[string]string `json:"Params"`
	BodyType string            `json:"BodyType"`
	Body     *string           `json:"Body"`
}

type SendResult struct {
	Status     *int                `json:"Status"`
	Reason     string              `json:"Reason"`
	Version    string              `json:"Version"`
	DurationMs float64             `json:"DurationMs"`
	Headers    map[string][]string `json:"Headers,omitempty"`
	Body       any                 `json:"Body"`
}

type SendSummary = SendResult

type RequestCreateOptions struct {
	Method   string
	Headers  []string
	Params   []string
	Data     string
	BodyType string
	Auth     string
}

type RequestEditOptions struct {
	Name     *string
	Uri      *string
	Method   *string
	Headers  []string
	Params   []string
	Data     *string
	BodyType *string
	Auth     *string
}

type AuthCreateOptions struct {
	Type                 string
	Secret               *string
	Prefix               *string
	Username             *string
	Password             *string
	GrantType            *string
	TokenURL             *string
	ClientID             *string
	ClientSecret         *string
	Scope                *string
	AuthorizationURL     *string
	RedirectURI          *string
	PKCE                 *string
	CustomURL            *string
	CustomMethod         *string
	CustomHeaders        []string
	CustomParams         []string
	CustomBody           *string
	CustomBodyType       *string
	ExtractionSource     *string
	ExtractionExpression *string
	ApplyHeaderName      *string
	ApplyHeaderTemplate  *string
	AutoRenew            *bool
}

type AuthEditOptions struct {
	Name                 *string
	Type                 *string
	Secret               *string
	Prefix               *string
	Username             *string
	Password             *string
	GrantType            *string
	TokenURL             *string
	ClientID             *string
	ClientSecret         *string
	Scope                *string
	AuthorizationURL     *string
	RedirectURI          *string
	PKCE                 *string
	CustomURL            *string
	CustomMethod         *string
	CustomHeaders        []string
	CustomParams         []string
	CustomBody           *string
	CustomBodyType       *string
	ExtractionSource     *string
	ExtractionExpression *string
	ApplyHeaderName      *string
	ApplyHeaderTemplate  *string
	AutoRenew            *bool
}

type SecretCreateOptions struct {
	Name  string
	Value string
}

type SecretEditOptions struct {
	Name  *string
	Value *string
}

type WorkspaceEditOptions struct {
	Name string
}

type ResponseSummary struct {
	StatusCode int               `json:"StatusCode"`
	Status     string            `json:"Status"`
	Headers    map[string]string `json:"Headers,omitempty"`
}

type ErrorEnvelope struct {
	Contents struct {
		Message string `json:"Message"`
	} `json:"Contents"`
}
