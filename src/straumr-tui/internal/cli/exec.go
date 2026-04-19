package cli

import (
	"bytes"
	"context"
	"errors"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"sync"
)

type CommandSpec struct {
	Args         []string
	WorkingDir   string
	Stdin        io.Reader
	CaptureLimit int64
	BinaryPath   string
	Environment  []string
}

type Output struct {
	Stdout []byte
	Stderr []byte
	Code   int
}

type Executor interface {
	Run(ctx context.Context, spec CommandSpec) (Output, error)
}

type OSExecutor struct {
	BinaryPath   string
	WorkingDir   string
	CaptureLimit int64
	Environment  []string
}

func (e OSExecutor) Run(ctx context.Context, spec CommandSpec) (Output, error) {
	binaryPath := spec.BinaryPath
	if binaryPath == "" {
		binaryPath = e.BinaryPath
	}

	if binaryPath == "" {
		return Output{}, &CommandError{Command: "straumr", ExitCode: -1, Message: "binary path not configured"}
	}

	args := append([]string(nil), spec.Args...)
	cmd := exec.CommandContext(ctx, binaryPath, args...)
	if spec.WorkingDir != "" {
		cmd.Dir = spec.WorkingDir
	} else if e.WorkingDir != "" {
		cmd.Dir = e.WorkingDir
	}
	if len(spec.Environment) > 0 {
		cmd.Env = append([]string(nil), spec.Environment...)
	} else if len(e.Environment) > 0 {
		cmd.Env = append([]string(nil), e.Environment...)
	}
	if spec.Stdin != nil {
		cmd.Stdin = spec.Stdin
	}

	stdoutPipe, err := cmd.StdoutPipe()
	if err != nil {
		return Output{}, err
	}
	stderrPipe, err := cmd.StderrPipe()
	if err != nil {
		return Output{}, err
	}

	limit := spec.CaptureLimit
	if limit <= 0 {
		limit = e.CaptureLimit
	}
	if limit <= 0 {
		limit = 1 << 20
	}

	stdoutCapture := newCaptureBuffer(limit)
	stderrCapture := newCaptureBuffer(limit)

	var wg sync.WaitGroup
	wg.Add(2)
	go func() {
		defer wg.Done()
		_, _ = io.Copy(stdoutCapture, stdoutPipe)
	}()
	go func() {
		defer wg.Done()
		_, _ = io.Copy(stderrCapture, stderrPipe)
	}()

	if err := cmd.Start(); err != nil {
		_ = stdoutCapture.Close()
		_ = stderrCapture.Close()
		return Output{}, err
	}

	waitErr := cmd.Wait()
	wg.Wait()
	stdoutBytes, _ := stdoutCapture.Finalize()
	stderrBytes, _ := stderrCapture.Finalize()

	output := Output{
		Stdout: stdoutBytes,
		Stderr: stderrBytes,
		Code:   0,
	}

	if waitErr == nil {
		return output, nil
	}

	output.Code = exitCode(waitErr)
	if output.Code == 0 {
		output.Code = -1
	}
	return output, waitErr
}

type captureBuffer struct {
	limit int64
	buf   bytes.Buffer
	file  *os.File
	path  string
	size  int64
	mu    sync.Mutex
}

func newCaptureBuffer(limit int64) *captureBuffer {
	return &captureBuffer{limit: limit}
}

func (c *captureBuffer) Write(p []byte) (int, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	c.size += int64(len(p))
	if c.file == nil && int64(c.buf.Len()+len(p)) <= c.limit {
		return c.buf.Write(p)
	}

	if c.file == nil {
		file, err := os.CreateTemp("", "straumr-tui-*")
		if err != nil {
			return 0, err
		}
		if _, err := file.Write(c.buf.Bytes()); err != nil {
			file.Close()
			_ = os.Remove(file.Name())
			return 0, err
		}
		c.buf.Reset()
		c.file = file
		c.path = file.Name()
	}

	return c.file.Write(p)
}

func (c *captureBuffer) Bytes() ([]byte, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	if c.file != nil {
		return os.ReadFile(c.path)
	}

	return append([]byte(nil), c.buf.Bytes()...), nil
}

func (c *captureBuffer) Finalize() ([]byte, error) {
	c.mu.Lock()
	if c.file == nil {
		data := append([]byte(nil), c.buf.Bytes()...)
		c.mu.Unlock()
		return data, nil
	}

	name := c.path
	file := c.file
	c.file = nil
	c.path = ""
	c.mu.Unlock()

	err := file.Close()
	data, readErr := os.ReadFile(name)
	removeErr := os.Remove(name)
	if readErr != nil {
		return nil, readErr
	}
	if removeErr != nil {
		return data, removeErr
	}
	return data, err
}

func (c *captureBuffer) Close() error {
	c.mu.Lock()
	defer c.mu.Unlock()

	if c.file != nil {
		name := c.path
		err := c.file.Close()
		c.file = nil
		c.path = ""
		if removeErr := os.Remove(name); err == nil && removeErr != nil {
			err = removeErr
		}
		return err
	}

	return nil
}

func exitCode(err error) int {
	var exitErr *exec.ExitError
	if errors.As(err, &exitErr) {
		return exitErr.ExitCode()
	}

	return -1
}

func cleanBinaryName(path string) string {
	return filepath.Base(path)
}
