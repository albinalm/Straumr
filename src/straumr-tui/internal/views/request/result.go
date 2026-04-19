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

func NewDraft() Draft {
	return Draft{}
}

func DraftFromItem(item Item) Draft {
	return Draft{
		Name:     item.Name,
		URL:      item.Target(),
		Method:   item.Method,
		BodyType: item.BodyType,
		Auth:     item.Auth,
	}
}

func (d Draft) Clone() Draft {
	return Draft{
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

func (d Draft) WithName(name string) Draft {
	d.Name = name
	return d
}

func (d Draft) WithURL(url string) Draft {
	d.URL = url
	return d
}

func (d Draft) WithMethod(method string) Draft {
	d.Method = method
	return d
}

func (d Draft) WithBody(bodyType, body string) Draft {
	d.BodyType = bodyType
	d.Body = body
	return d
}

func (d Draft) WithAuth(auth string) Draft {
	d.Auth = auth
	return d
}

func (d Draft) WithParam(key, value string) Draft {
	d.Params = upsertPair(d.Params, key, value)
	return d
}

func (d Draft) WithoutParam(key string) Draft {
	d.Params = removePair(d.Params, key)
	return d
}

func (d Draft) WithHeader(key, value string) Draft {
	d.Headers = upsertPair(d.Headers, key, value)
	return d
}

func (d Draft) WithoutHeader(key string) Draft {
	d.Headers = removePair(d.Headers, key)
	return d
}

// Submission captures the editor mode and the current draft snapshot together.
type Submission struct {
	Mode  EditorMode
	Item  Item
	Draft MutationDraft
}

func NewSubmission(mode EditorMode, item Item, draft Draft) Submission {
	return Submission{
		Mode:  mode,
		Item:  item,
		Draft: draft.MutationDraft(),
	}
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

func (i Item) Target() string {
	if i.Host != "" {
		return i.Host
	}

	return ""
}

func upsertPair(items []Pair, key, value string) []Pair {
	for i := range items {
		if items[i].Key == key {
			items[i].Value = value
			return items
		}
	}

	return append(items, Pair{Key: key, Value: value})
}

func removePair(items []Pair, key string) []Pair {
	out := items[:0]
	for _, item := range items {
		if item.Key == key {
			continue
		}
		out = append(out, item)
	}
	return out
}
