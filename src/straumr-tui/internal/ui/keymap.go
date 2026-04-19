package ui

import (
	"straumr-tui/internal/views/auth"
	"straumr-tui/internal/views/request"
	"straumr-tui/internal/views/secret"
	"straumr-tui/internal/views/send"
	"straumr-tui/internal/views/workspace"

	tea "github.com/charmbracelet/bubbletea"
)

type GlobalKey string

const (
	GlobalNone    GlobalKey = ""
	GlobalQuit    GlobalKey = "quit"
	GlobalNext    GlobalKey = "next"
	GlobalPrev    GlobalKey = "prev"
	GlobalRefresh GlobalKey = "refresh"
)

func Global(msg tea.KeyMsg) (GlobalKey, bool) {
	switch msg.String() {
	case "ctrl+c", "q":
		return GlobalQuit, true
	case "tab":
		return GlobalNext, true
	case "shift+tab", "backtab":
		return GlobalPrev, true
	case "r":
		return GlobalRefresh, true
	default:
		return GlobalNone, false
	}
}

func WorkspaceKey(msg tea.KeyMsg) (workspace.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return workspace.KeyUp, true
	case "down", "j":
		return workspace.KeyDown, true
	case "g":
		return workspace.KeyHome, true
	case "G":
		return workspace.KeyEnd, true
	case "enter":
		return workspace.KeyEnter, true
	case "o":
		return workspace.KeyOpen, true
	case "i":
		return workspace.KeyInspect, true
	case "/":
		return workspace.KeySearch, true
	case ":":
		return workspace.KeyCommand, true
	case "s":
		return workspace.KeySet, true
	case "c":
		return workspace.KeyCreate, true
	case "d":
		return workspace.KeyDelete, true
	case "e":
		return workspace.KeyEdit, true
	case "y":
		return workspace.KeyCopy, true
	case "I", "shift+i":
		return workspace.KeyImport, true
	case "x":
		return workspace.KeyExport, true
	case "esc":
		return workspace.KeyCancel, true
	default:
		return "", false
	}
}

func RequestKey(msg tea.KeyMsg) (request.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return request.KeyUp, true
	case "down", "j":
		return request.KeyDown, true
	case "g":
		return request.KeyHome, true
	case "G":
		return request.KeyEnd, true
	case "enter":
		return request.KeyEnter, true
	case "i":
		return request.KeyInspect, true
	case "/":
		return request.KeySearch, true
	case ":":
		return request.KeyCommand, true
	case "s":
		return request.KeySend, true
	case "c":
		return request.KeyCreate, true
	case "d":
		return request.KeyDelete, true
	case "e":
		return request.KeyEdit, true
	case "E", "shift+e":
		return request.KeyEditAlt, true
	case "y":
		return request.KeyCopy, true
	case "esc":
		return request.KeyCancel, true
	default:
		return "", false
	}
}

func AuthKey(msg tea.KeyMsg) (auth.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return auth.KeyUp, true
	case "down", "j":
		return auth.KeyDown, true
	case "g":
		return auth.KeyHome, true
	case "G":
		return auth.KeyEnd, true
	case "enter":
		return auth.KeyEnter, true
	case "i":
		return auth.KeyInspect, true
	case "/":
		return auth.KeySearch, true
	case ":":
		return auth.KeyCommand, true
	case "c":
		return auth.KeyCreate, true
	case "d":
		return auth.KeyDelete, true
	case "e":
		return auth.KeyEdit, true
	case "E", "shift+e":
		return auth.KeyEditAlt, true
	case "y":
		return auth.KeyCopy, true
	case "esc":
		return auth.KeyCancel, true
	default:
		return "", false
	}
}

func SecretKey(msg tea.KeyMsg) (secret.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return secret.KeyUp, true
	case "down", "j":
		return secret.KeyDown, true
	case "g":
		return secret.KeyHome, true
	case "G":
		return secret.KeyEnd, true
	case "enter":
		return secret.KeyEnter, true
	case "i":
		return secret.KeyInspect, true
	case "/":
		return secret.KeySearch, true
	case ":":
		return secret.KeyCommand, true
	case "c":
		return secret.KeyCreate, true
	case "d":
		return secret.KeyDelete, true
	case "e":
		return secret.KeyEdit, true
	case "E", "shift+e":
		return secret.KeyEditAlt, true
	case "y":
		return secret.KeyCopy, true
	case "esc":
		return secret.KeyCancel, true
	default:
		return "", false
	}
}

func SendKey(msg tea.KeyMsg) (send.Key, bool) {
	switch msg.String() {
	case "up", "k":
		return send.KeyUp, true
	case "down", "j":
		return send.KeyDown, true
	case "g":
		return send.KeyHome, true
	case "G":
		return send.KeyEnd, true
	case "tab":
		return send.KeyTab, true
	case "shift+tab", "backtab":
		return send.KeyShiftTab, true
	case "esc":
		return send.KeyCancel, true
	case "c":
		return send.KeyCopyPane, true
	case "p":
		return send.KeyCopyTemplate, true
	case "b":
		return send.KeyBeautify, true
	case "R":
		return send.KeyRevert, true
	case "w":
		return send.KeySaveBody, true
	case "x":
		return send.KeyExport, true
	case "n":
		return send.KeyDryRun, true
	case "s":
		return send.KeyRefresh, true
	default:
		return "", false
	}
}
