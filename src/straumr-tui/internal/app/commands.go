package app

import (
	"context"

	"straumr-tui/internal/cli"
	"straumr-tui/internal/state"

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
