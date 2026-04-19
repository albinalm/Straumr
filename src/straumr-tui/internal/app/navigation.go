package app

import (
	"straumr-tui/internal/state"
	"straumr-tui/internal/views/workspace"
)

var screenOrder = []state.ScreenID{
	state.ScreenWorkspaces,
	state.ScreenRequests,
	state.ScreenAuths,
	state.ScreenSecrets,
	state.ScreenSend,
}

func nextScreen(current state.ScreenID, delta int) state.ScreenID {
	if len(screenOrder) == 0 {
		return current
	}

	index := 0
	for i, screen := range screenOrder {
		if screen == current {
			index = i
			break
		}
	}

	index += delta
	if index < 0 {
		index = len(screenOrder) - 1
	}
	if index >= len(screenOrder) {
		index = 0
	}

	return screenOrder[index]
}

func screenFromWorkspaceTarget(target workspace.OpenTarget) state.ScreenID {
	switch target {
	case workspace.TargetRequests:
		return state.ScreenRequests
	case workspace.TargetAuths:
		return state.ScreenAuths
	case workspace.TargetSecrets:
		return state.ScreenSecrets
	case workspace.TargetActivate:
		return state.ScreenWorkspaces
	default:
		return state.ScreenWorkspaces
	}
}
