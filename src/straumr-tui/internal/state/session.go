package state

type ScreenID string

const (
	ScreenWorkspaces ScreenID = "workspaces"
	ScreenRequests   ScreenID = "requests"
	ScreenAuths      ScreenID = "auths"
	ScreenSecrets    ScreenID = "secrets"
	ScreenSend       ScreenID = "send"
)

type WorkspaceRef struct {
	ID   string
	Name string
}

type RequestRef struct {
	ID            string
	Name          string
	Method        string
	URI           string
	WorkspaceID   string
	WorkspaceName string
}

type Session struct {
	ActiveWorkspace *WorkspaceRef
	ActiveRequest   *RequestRef
	Screen          ScreenID
	Width           int
	Height          int
	Busy            bool
	Message         string
	Error           string
}

func (s Session) HasWorkspace() bool {
	return s.ActiveWorkspace != nil && s.ActiveWorkspace.ID != ""
}

func (s *Session) SetWorkspace(id, name string) {
	if id == "" {
		s.ActiveWorkspace = nil
		return
	}

	s.ActiveWorkspace = &WorkspaceRef{ID: id, Name: name}
}

func (s *Session) SetRequest(id, name, method, uri, workspaceID, workspaceName string) {
	if id == "" {
		s.ActiveRequest = nil
		return
	}

	s.ActiveRequest = &RequestRef{
		ID:            id,
		Name:          name,
		Method:        method,
		URI:           uri,
		WorkspaceID:   workspaceID,
		WorkspaceName: workspaceName,
	}
}
