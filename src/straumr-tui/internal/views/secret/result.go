package secret

// MutationDraft is the structured payload the shell can turn into CLI
// arguments when it is ready to perform a real mutation.
type MutationDraft struct {
	Name  string
	Value string
}

// Submission captures the editor mode with the current structured snapshot.
type Submission struct {
	Mode  EditorMode
	Draft MutationDraft
}

func (d Draft) MutationDraft() MutationDraft {
	return MutationDraft{
		Name:  d.Name,
		Value: d.Value,
	}
}
