package app

import (
	"straumr-tui/internal/cli"
	"straumr-tui/internal/state"
)

type bootstrapMsg struct {
	Workspaces []cli.WorkspaceSummary
	Active     *cli.WorkspaceSummary
	Requests   []cli.RequestSummary
	Err        error
}

type workspacesLoadedMsg struct {
	Workspaces []cli.WorkspaceSummary
	Err        error
}

type requestsLoadedMsg struct {
	Workspace state.WorkspaceRef
	Requests  []cli.RequestSummary
	Err       error
}

type authsLoadedMsg struct {
	Workspace state.WorkspaceRef
	Auths     []cli.AuthSummary
	Err       error
}

type secretsLoadedMsg struct {
	Secrets []cli.SecretSummary
	Err     error
}

type sendLoadedMsg struct {
	Request state.RequestRef
	Result  cli.SendSummary
	Err     error
}

type dryRunLoadedMsg struct {
	Request state.RequestRef
	Result  cli.DryRunResult
	Err     error
}

type requestEditorSeedMsg struct {
	Item   cli.RequestGetResult
	Err    error
	Action pendingFlow
}

type requestInspectLoadedMsg struct {
	Item      cli.RequestGetResult
	Err       error
	RequestID string
}

type requestBodyLoadedMsg struct {
	Pending pendingAction
	Path    string
	Content string
	Err     error
}

type requestPickerChoicesLoadedMsg struct {
	Flow    pendingFlow
	Choices []pickerChoice
	Err     error
}

type workspaceInspectLoadedMsg struct {
	Item cli.WorkspaceGetResult
	Err  error
}

type authEditorSeedMsg struct {
	Item   cli.AuthGetResult
	Err    error
	Action pendingFlow
}

type authInspectLoadedMsg struct {
	Item cli.AuthGetResult
	Err  error
}

type mutationCompletedMsg struct {
	Screen           state.ScreenID
	Message          string
	Err              error
	SelectID         string
	UpdatedWorkspace *state.WorkspaceRef
	UpdatedRequest   *state.RequestRef
	DeletedRequestID string
}

type shellActionCompletedMsg struct {
	Message string
	Err     error
}

type secretEditorSeedMsg struct {
	Item   cli.SecretGetResult
	Err    error
	Action pendingFlow
}

type secretInspectLoadedMsg struct {
	Item cli.SecretGetResult
	Err  error
}

type statusMsg string

type cliErrorMsg struct {
	Scope string
	Err   error
}

type pickerChoice struct {
	Key         string
	Title       string
	Description string
}
