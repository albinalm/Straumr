package dialogs

import (
	"strings"
	"testing"
)

func TestPathPickerRenderIncludesSectionsAndHelp(t *testing.T) {
	v := PathPickerView{}
	v.Open(
		"Choose export path",
		"Pick a destination",
		PathModeSave,
		"/tmp/report.json",
		"/tmp",
		true,
		[]PathEntry{
			{Name: "Home", Path: "/home/albin", Directory: true, Quick: true},
		},
		[]PathEntry{
			{Name: "reports", Path: "/tmp/reports", Directory: true},
			{Name: "report.json", Path: "/tmp/report.json"},
		},
	)
	v.Help = "Custom footer"

	rendered := v.Render()

	for _, want := range []string{
		"Choose export path",
		"Pick a destination",
		"Mode: choose save target",
		"Current directory: /tmp",
		"Path: /tmp/report.json|",
		"Focus: typed path",
		"Quick locations",
		"Browsable entries",
		"The target path must be valid for saving.",
		"Help: Enter accept",
		"Custom footer",
	} {
		if !strings.Contains(rendered, want) {
			t.Fatalf("rendered output missing %q:\n%s", want, rendered)
		}
	}
}

func TestPathPickerTypedInputEditing(t *testing.T) {
	v := PathPickerView{}
	v.Open("Path", "", PathModeOpen, "abc", "", false, nil, nil)
	v.Focus = PathFocusPath

	v.MoveInputCursor(-1)
	v.InsertText("x")
	if got := v.InputPath; got != "abxc" {
		t.Fatalf("unexpected input after insert: %q", got)
	}

	if !v.DeleteBackward() {
		t.Fatal("expected backspace to delete a rune")
	}
	if got := v.InputPath; got != "abc" {
		t.Fatalf("unexpected input after backspace: %q", got)
	}

	if !v.DeleteForward() {
		t.Fatal("expected delete to remove a rune")
	}
	if got := v.InputPath; got != "ab" {
		t.Fatalf("unexpected input after delete: %q", got)
	}
}

func TestPathPickerSelectionAppliesPath(t *testing.T) {
	v := PathPickerView{}
	v.Open(
		"Path",
		"",
		PathModeOpen,
		"",
		"",
		false,
		[]PathEntry{{Name: "Home", Path: "/home/albin", Directory: true, Quick: true}},
		[]PathEntry{{Name: "config", Path: "/tmp/config", Directory: false}},
	)

	v.Focus = PathFocusQuick
	if kind := v.HandleKey(KeyEnter); kind != ActionOpen {
		t.Fatalf("expected quick directory selection to open, got %q", kind)
	}
	if got := v.InputPath; got != "/home/albin" {
		t.Fatalf("unexpected quick selection path: %q", got)
	}

	v.Focus = PathFocusEntries
	v.Cursor = 0
	if kind := v.HandleKey(KeyEnter); kind != ActionAccept {
		t.Fatalf("expected file selection to accept, got %q", kind)
	}
	if got := v.InputPath; got != "/tmp/config" {
		t.Fatalf("unexpected entry selection path: %q", got)
	}

	result := v.Result(true)
	if result.Path != "/tmp/config" {
		t.Fatalf("unexpected result path: %q", result.Path)
	}
	if result.Entry.Path != "/tmp/config" {
		t.Fatalf("unexpected result entry path: %q", result.Entry.Path)
	}
}
