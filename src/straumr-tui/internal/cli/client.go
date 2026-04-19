package cli

import (
	"context"
	"encoding/json"
	"fmt"
	"path/filepath"
	"time"

	"straumr-tui/internal/cache"
)

type Client struct {
	executor     Executor
	cache        *cache.Store
	binaryPath   string
	workingDir   string
	captureLimit int64
}

type Option func(*Client)

func WithCache(store *cache.Store) Option {
	return func(c *Client) {
		c.cache = store
	}
}

func WithWorkingDir(dir string) Option {
	return func(c *Client) {
		c.workingDir = dir
	}
}

func WithCaptureLimit(limit int64) Option {
	return func(c *Client) {
		c.captureLimit = limit
	}
}

func NewClient(binaryPath string, opts ...Option) *Client {
	client := &Client{
		executor: OSExecutor{
			BinaryPath:   binaryPath,
			CaptureLimit: 1 << 20,
			WorkingDir:   "",
			Environment:  nil,
		},
		binaryPath:   binaryPath,
		captureLimit: 1 << 20,
	}

	for _, opt := range opts {
		opt(client)
	}

	exec := OSExecutor{
		BinaryPath:   client.binaryPath,
		WorkingDir:   client.workingDir,
		CaptureLimit: client.captureLimit,
	}
	client.executor = exec
	return client
}

func RunJSON[T any](c *Client, ctx context.Context, args []string) (T, error) {
	var zero T
	output, err := c.executor.Run(ctx, CommandSpec{
		Args:         append([]string(nil), args...),
		WorkingDir:   c.workingDir,
		CaptureLimit: c.captureLimit,
		BinaryPath:   c.binaryPath,
	})
	if err != nil {
		message := ParseErrorMessage(output.Stderr)
		if message == "" {
			message = err.Error()
		}
		return zero, &CommandError{
			Command:  fmt.Sprintf("%s %s", filepath.Base(c.binaryPath), joinArgs(args)),
			ExitCode: output.Code,
			Message:  message,
			Stderr:   string(output.Stderr),
			Cause:    err,
		}
	}

	if len(output.Stdout) == 0 {
		return zero, nil
	}

	if err := json.Unmarshal(output.Stdout, &zero); err != nil {
		return zero, err
	}

	return zero, nil
}

func (c *Client) ListWorkspaces(ctx context.Context) ([]WorkspaceSummary, error) {
	if value, ok := c.cached("workspaces:list"); ok {
		if workspaces, ok := value.([]WorkspaceSummary); ok {
			return workspaces, nil
		}
	}

	workspaces, err := RunJSON[[]WorkspaceSummary](c, ctx, []string{"list", "workspace", "--json"})
	if err != nil {
		return nil, err
	}

	c.store("workspaces:list", workspaces, 30*time.Second)
	return workspaces, nil
}

func (c *Client) ListRequests(ctx context.Context, workspaceID string) ([]RequestSummary, error) {
	key := "requests:list:" + workspaceID
	if value, ok := c.cached(key); ok {
		if requests, ok := value.([]RequestSummary); ok {
			return requests, nil
		}
	}

	requests, err := RunJSON[[]RequestSummary](c, ctx, []string{"list", "request", "--json", "--workspace", workspaceID})
	if err != nil {
		return nil, err
	}

	c.store(key, requests, 30*time.Second)
	return requests, nil
}

func (c *Client) ListAuths(ctx context.Context, workspaceID string) ([]AuthSummary, error) {
	key := "auths:list:" + workspaceID
	if value, ok := c.cached(key); ok {
		if auths, ok := value.([]AuthSummary); ok {
			return auths, nil
		}
	}

	auths, err := RunJSON[[]AuthSummary](c, ctx, []string{"list", "auth", "--json", "--workspace", workspaceID})
	if err != nil {
		return nil, err
	}

	c.store(key, auths, 30*time.Second)
	return auths, nil
}

func (c *Client) ListSecrets(ctx context.Context) ([]SecretSummary, error) {
	if value, ok := c.cached("secrets:list"); ok {
		if secrets, ok := value.([]SecretSummary); ok {
			return secrets, nil
		}
	}

	secrets, err := RunJSON[[]SecretSummary](c, ctx, []string{"list", "secret", "--json"})
	if err != nil {
		return nil, err
	}

	c.store("secrets:list", secrets, 30*time.Second)
	return secrets, nil
}

func (c *Client) SendRequest(ctx context.Context, requestID, workspaceID string) (SendSummary, error) {
	args := []string{"send", requestID, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	return RunJSON[SendSummary](c, ctx, args)
}

func (c *Client) ResolveActiveWorkspace(ctx context.Context) (*WorkspaceSummary, error) {
	workspaces, err := c.ListWorkspaces(ctx)
	if err != nil {
		return nil, err
	}

	for index := range workspaces {
		if workspaces[index].IsCurrent {
			return &workspaces[index], nil
		}
	}

	return nil, nil
}

func (c *Client) Invalidate(pattern string) {
	if c.cache == nil {
		return
	}
	c.cache.Delete(pattern)
}

func (c *Client) InvalidateAll() {
	if c.cache == nil {
		return
	}
	c.cache.Clear()
}

func (c *Client) cached(key string) (any, bool) {
	if c.cache == nil {
		return nil, false
	}

	return c.cache.Get(key)
}

func (c *Client) store(key string, value any, ttl time.Duration) {
	if c.cache == nil {
		return
	}

	c.cache.Set(key, value, ttl)
}

func joinArgs(args []string) string {
	if len(args) == 0 {
		return ""
	}

	out := args[0]
	for _, arg := range args[1:] {
		out += " " + arg
	}
	return out
}
