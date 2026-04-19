package app

import (
	"context"
	"errors"
	"os"
	"os/exec"
	"path/filepath"

	"straumr-tui/internal/cache"
	"straumr-tui/internal/cli"

	tea "github.com/charmbracelet/bubbletea"
)

func Run(ctx context.Context) error {
	binaryPath, err := ResolveStraumrBinary()
	if err != nil {
		return err
	}

	store := cache.NewStore()
	client := cli.NewClient(binaryPath, cli.WithCache(store))
	model := NewModel(ctx, client, store)

	program := tea.NewProgram(model, tea.WithAltScreen())
	_, err = program.Run()
	return err
}

func ResolveStraumrBinary() (string, error) {
	if candidate := os.Getenv("STRAUMR_CLI_PATH"); candidate != "" {
		return candidate, nil
	}

	if executable, err := os.Executable(); err == nil {
		dir := filepath.Dir(executable)
		for _, name := range []string{"straumr.exe", "straumr"} {
			candidate := filepath.Join(dir, name)
			if info, err := os.Stat(candidate); err == nil && !info.IsDir() {
				return candidate, nil
			}
		}
	}

	for _, name := range []string{"straumr.exe", "straumr"} {
		if candidate, err := exec.LookPath(name); err == nil {
			return candidate, nil
		}
	}

	return "", errors.New("unable to locate the straumr CLI binary")
}
