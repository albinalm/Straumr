package request

// MutationDraft is the structured payload the root layer can turn into CLI
// mutation flags. It stays presentation-only and does not know about CLI
// execution.
type MutationDraft struct {
	Name     string
	URL      string
	Method   string
	Params   []Pair
	Headers  []Pair
	Body     string
	BodyType string
	Auth     string
}

// Submission captures the editor mode and the current draft snapshot together.
type Submission struct {
	Mode  EditorMode
	Draft MutationDraft
}

func (d Draft) MutationDraft() MutationDraft {
	return MutationDraft{
		Name:     d.Name,
		URL:      d.URL,
		Method:   d.Method,
		Params:   append([]Pair(nil), d.Params...),
		Headers:  append([]Pair(nil), d.Headers...),
		Body:     d.Body,
		BodyType: d.BodyType,
		Auth:     d.Auth,
	}
}
