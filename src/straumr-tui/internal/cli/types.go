package cli

import "time"

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

type SendSummary struct {
	Status     *int                `json:"Status"`
	Reason     string              `json:"Reason"`
	Version    string              `json:"Version"`
	DurationMs float64             `json:"DurationMs"`
	Headers    map[string][]string `json:"Headers,omitempty"`
	Body       any                 `json:"Body"`
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
