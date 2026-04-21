package theme

import (
	"encoding/json"
	"os"
	"path/filepath"
)

var (
	activeTheme  = Default()
	activeStyles = BuildStyles(activeTheme)
)

func ThemePath() string {
	home, err := os.UserHomeDir()
	if err != nil || home == "" {
		return ""
	}
	return filepath.Join(home, ".straumr", "theme.json")
}

func Load() Theme {
	path := ThemePath()
	if path == "" {
		return Default()
	}

	data, err := os.ReadFile(path)
	if err != nil {
		return Default()
	}

	options := ThemeOptions{Theme: Default()}
	if err := json.Unmarshal(data, &options); err != nil {
		return Default()
	}

	return options.Theme
}

func SetActive(value Theme) {
	activeTheme = value
	activeStyles = BuildStyles(value)
}

func Active() Theme {
	return activeTheme
}

func CurrentStyles() Styles {
	return activeStyles
}
