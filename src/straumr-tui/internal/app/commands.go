package app

import (
	"context"
	"fmt"

	"straumr-tui/internal/cli"
	"straumr-tui/internal/state"
	"straumr-tui/internal/views/request"

	tea "github.com/charmbracelet/bubbletea"
)

func bootstrapCmd(ctx context.Context, client *cli.Client) tea.Cmd {
	return func() tea.Msg {
		workspaces, err := client.ListWorkspaces(ctx)
		if err != nil {
			return bootstrapMsg{Err: err}
		}

		active, err := client.ResolveActiveWorkspace(ctx)
		if err != nil {
			return bootstrapMsg{Workspaces: workspaces, Err: err}
		}

		if active == nil {
			return bootstrapMsg{Workspaces: workspaces}
		}

		requests, err := client.ListRequests(ctx, active.ID)
		if err != nil {
			return bootstrapMsg{
				Workspaces: workspaces,
				Active:     active,
				Err:        err,
			}
		}

		return bootstrapMsg{
			Workspaces: workspaces,
			Active:     active,
			Requests:   requests,
		}
	}
}

func refreshCmd(ctx context.Context, client *cli.Client, session state.Session) tea.Cmd {
	switch session.Screen {
	case state.ScreenRequests:
		if session.ActiveWorkspace == nil {
			return nil
		}
		return refreshRequestsCmd(ctx, client, session.ActiveWorkspace.ID, session.ActiveWorkspace.Name)
	case state.ScreenAuths:
		if session.ActiveWorkspace == nil {
			return nil
		}
		return refreshAuthsCmd(ctx, client, session.ActiveWorkspace.ID, session.ActiveWorkspace.Name)
	case state.ScreenSecrets:
		return refreshSecretsCmd(ctx, client)
	case state.ScreenSend:
		if session.ActiveRequest == nil {
			return nil
		}
		return sendCmd(ctx, client, *session.ActiveRequest)
	default:
		return bootstrapCmd(ctx, client)
	}
}

func refreshRequestsCmd(ctx context.Context, client *cli.Client, workspaceID, workspaceName string) tea.Cmd {
	return func() tea.Msg {
		requests, err := client.ListRequests(ctx, workspaceID)
		if err != nil {
			return cliErrorMsg{Scope: "requests", Err: err}
		}

		return requestsLoadedMsg{
			Workspace: state.WorkspaceRef{
				ID:   workspaceID,
				Name: workspaceName,
			},
			Requests: requests,
		}
	}
}

func refreshAuthsCmd(ctx context.Context, client *cli.Client, workspaceID, workspaceName string) tea.Cmd {
	return func() tea.Msg {
		auths, err := client.ListAuths(ctx, workspaceID)
		if err != nil {
			return cliErrorMsg{Scope: "auths", Err: err}
		}

		return authsLoadedMsg{
			Workspace: state.WorkspaceRef{
				ID:   workspaceID,
				Name: workspaceName,
			},
			Auths: auths,
		}
	}
}

func refreshSecretsCmd(ctx context.Context, client *cli.Client) tea.Cmd {
	return func() tea.Msg {
		secrets, err := client.ListSecrets(ctx)
		if err != nil {
			return cliErrorMsg{Scope: "secrets", Err: err}
		}

		return secretsLoadedMsg{
			Secrets: secrets,
		}
	}
}

func sendCmd(ctx context.Context, client *cli.Client, request state.RequestRef) tea.Cmd {
	return func() tea.Msg {
		result, err := client.SendRequest(ctx, request.ID, request.WorkspaceID)
		return sendLoadedMsg{
			Request: request,
			Result:  result,
			Err:     err,
		}
	}
}

func dryRunCmd(ctx context.Context, client *cli.Client, request state.RequestRef) tea.Cmd {
	return func() tea.Msg {
		result, err := client.DryRunRequest(ctx, request.ID, request.WorkspaceID, nil, nil)
		return dryRunLoadedMsg{
			Request: request,
			Result:  result,
			Err:     err,
		}
	}
}

func seedRequestEditCmd(ctx context.Context, client *cli.Client, workspaceID, identifier string, action pendingFlow) tea.Cmd {
	return func() tea.Msg {
		item, err := client.GetRequest(ctx, workspaceID, identifier)
		return requestEditorSeedMsg{
			Item:   item,
			Err:    err,
			Action: action,
		}
	}
}

func createRequestCmd(ctx context.Context, client *cli.Client, workspaceID string, draft request.MutationDraft) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CreateRequest(ctx, workspaceID, draft.Name, draft.URL, cli.RequestCreateOptions{
			Method:   draft.Method,
			Headers:  formatRequestHeaders(draft.Headers),
			Params:   formatRequestParams(draft.Params),
			Data:     draft.Body,
			BodyType: draft.BodyType,
			Auth:     draft.Auth,
		})
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenRequests, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenRequests,
			Message: fmt.Sprintf("Created request %s", result.Name),
		}
	}
}

func editRequestCmd(ctx context.Context, client *cli.Client, workspaceID, identifier string, draft request.MutationDraft) tea.Cmd {
	return func() tea.Msg {
		name := draft.Name
		url := draft.URL
		method := draft.Method
		data := draft.Body
		bodyType := draft.BodyType
		auth := draft.Auth

		result, err := client.EditRequest(ctx, workspaceID, identifier, cli.RequestEditOptions{
			Name:     &name,
			Uri:      &url,
			Method:   &method,
			Headers:  formatRequestHeaders(draft.Headers),
			Params:   formatRequestParams(draft.Params),
			Data:     &data,
			BodyType: &bodyType,
			Auth:     &auth,
		})
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenRequests, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenRequests,
			Message: fmt.Sprintf("Updated request %s", result.Name),
		}
	}
}

func createWorkspaceCmd(ctx context.Context, client *cli.Client, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CreateWorkspace(ctx, name, "")
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenWorkspaces, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenWorkspaces,
			Message: fmt.Sprintf("Created workspace %s", result.Name),
		}
	}
}

func editWorkspaceCmd(ctx context.Context, client *cli.Client, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.EditWorkspace(ctx, identifier, cli.WorkspaceEditOptions{Name: name})
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenWorkspaces, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenWorkspaces,
			Message: fmt.Sprintf("Renamed workspace to %s", result.Name),
		}
	}
}

func copyWorkspaceCmd(ctx context.Context, client *cli.Client, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CopyWorkspace(ctx, identifier, name, "")
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenWorkspaces, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenWorkspaces,
			Message: fmt.Sprintf("Copied workspace to %s", result.Name),
		}
	}
}

func deleteWorkspaceCmd(ctx context.Context, client *cli.Client, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		err := client.DeleteWorkspace(ctx, identifier)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenWorkspaces, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenWorkspaces,
			Message: fmt.Sprintf("Deleted workspace %s", name),
		}
	}
}

func deleteRequestCmd(ctx context.Context, client *cli.Client, workspaceID, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		err := client.DeleteRequest(ctx, workspaceID, identifier)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenRequests, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenRequests,
			Message: fmt.Sprintf("Deleted request %s", name),
		}
	}
}

func formatRequestHeaders(headers []request.Pair) []string {
	values := make([]string, 0, len(headers))
	for _, header := range headers {
		if header.Key == "" {
			continue
		}
		values = append(values, fmt.Sprintf("%s: %s", header.Key, header.Value))
	}
	return values
}

func formatRequestParams(params []request.Pair) []string {
	values := make([]string, 0, len(params))
	for _, param := range params {
		if param.Key == "" {
			continue
		}
		values = append(values, fmt.Sprintf("%s=%s", param.Key, param.Value))
	}
	return values
}

func copyRequestCmd(ctx context.Context, client *cli.Client, workspaceID, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CopyRequest(ctx, workspaceID, identifier, name)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenRequests, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenRequests,
			Message: fmt.Sprintf("Copied request to %s", result.Name),
		}
	}
}

func deleteAuthCmd(ctx context.Context, client *cli.Client, workspaceID, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		err := client.DeleteAuth(ctx, workspaceID, identifier)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenAuths, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenAuths,
			Message: fmt.Sprintf("Deleted auth %s", name),
		}
	}
}

func copyAuthCmd(ctx context.Context, client *cli.Client, workspaceID, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CopyAuth(ctx, workspaceID, identifier, name)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenAuths, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenAuths,
			Message: fmt.Sprintf("Copied auth to %s", result.Name),
		}
	}
}

func seedSecretEditCmd(ctx context.Context, client *cli.Client, identifier string, action pendingFlow) tea.Cmd {
	return func() tea.Msg {
		item, err := client.GetSecret(ctx, identifier)
		return secretEditorSeedMsg{
			Item:   item,
			Err:    err,
			Action: action,
		}
	}
}

func createSecretCmd(ctx context.Context, client *cli.Client, name, value string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CreateSecret(ctx, cli.SecretCreateOptions{Name: name, Value: value})
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenSecrets, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenSecrets,
			Message: fmt.Sprintf("Created secret %s", result.Name),
		}
	}
}

func editSecretCmd(ctx context.Context, client *cli.Client, identifier, name, value string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.EditSecret(ctx, identifier, cli.SecretEditOptions{
			Name:  &name,
			Value: &value,
		})
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenSecrets, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenSecrets,
			Message: fmt.Sprintf("Updated secret %s", result.Name),
		}
	}
}

func copySecretCmd(ctx context.Context, client *cli.Client, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.CopySecret(ctx, identifier, name)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenSecrets, Err: err}
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenSecrets,
			Message: fmt.Sprintf("Copied secret to %s", result.Name),
		}
	}
}

func deleteSecretCmd(ctx context.Context, client *cli.Client, identifier, name string) tea.Cmd {
	return func() tea.Msg {
		result, err := client.DeleteSecret(ctx, identifier)
		if err != nil {
			return mutationCompletedMsg{Screen: state.ScreenSecrets, Err: err}
		}
		deletedName := result.Name
		if deletedName == "" {
			deletedName = name
		}
		return mutationCompletedMsg{
			Screen:  state.ScreenSecrets,
			Message: fmt.Sprintf("Deleted secret %s", deletedName),
		}
	}
}
