package cli

import (
	"encoding/json"
	"errors"
	"fmt"
	"strings"
)

type CommandError struct {
	Command  string
	ExitCode int
	Message  string
	Stderr   string
	Cause    error
}

func (e *CommandError) Error() string {
	if e.Message != "" {
		return fmt.Sprintf("%s failed (exit %d): %s", e.Command, e.ExitCode, e.Message)
	}
	if e.Stderr != "" {
		return fmt.Sprintf("%s failed (exit %d): %s", e.Command, e.ExitCode, strings.TrimSpace(e.Stderr))
	}
	return fmt.Sprintf("%s failed (exit %d)", e.Command, e.ExitCode)
}

func (e *CommandError) Unwrap() error {
	return e.Cause
}

func IsCommandError(err error) bool {
	var commandError *CommandError
	return errors.As(err, &commandError)
}

func ParseErrorMessage(stderr []byte) string {
	trimmed := strings.TrimSpace(string(stderr))
	if trimmed == "" {
		return ""
	}

	var envelope ErrorEnvelope
	if json.Unmarshal(stderr, &envelope) == nil && envelope.Contents.Message != "" {
		return envelope.Contents.Message
	}

	return trimmed
}
