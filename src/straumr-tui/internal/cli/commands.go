package cli

import (
	"context"
	"time"
)

const defaultMutationCacheTTL = 30 * time.Second

func (c *Client) CreateWorkspace(ctx context.Context, name, outputDir string) (WorkspaceCreateResult, error) {
	args := []string{"create", "workspace", name}
	if outputDir != "" {
		args = append(args, "--output", outputDir)
	}
	args = append(args, "--json")

	workspace, err := RunJSON[WorkspaceCreateResult](c, ctx, args)
	if err != nil {
		return WorkspaceCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return workspace, nil
}

func (c *Client) GetWorkspace(ctx context.Context, identifier string) (WorkspaceGetResult, error) {
	key := c.cacheKey("workspace", "get", identifier)
	if value, ok := c.cached(key); ok {
		if workspace, ok := value.(WorkspaceGetResult); ok {
			return workspace, nil
		}
	}

	workspace, err := RunJSON[WorkspaceGetResult](c, ctx, []string{"get", "workspace", identifier, "--json"})
	if err != nil {
		return WorkspaceGetResult{}, err
	}

	c.storeAliases(workspace, defaultMutationCacheTTL, key, c.cacheKey("workspace", "get", workspace.ID))
	return workspace, nil
}

func (c *Client) EditWorkspace(ctx context.Context, identifier string, opts WorkspaceEditOptions) (WorkspaceCreateResult, error) {
	args := []string{"edit", "workspace", identifier, "--name", opts.Name, "--json"}
	workspace, err := RunJSON[WorkspaceCreateResult](c, ctx, args)
	if err != nil {
		return WorkspaceCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return workspace, nil
}

func (c *Client) CopyWorkspace(ctx context.Context, identifier, newName, outputDir string) (WorkspaceCreateResult, error) {
	args := []string{"copy", "workspace", identifier, newName}
	if outputDir != "" {
		args = append(args, "--output", outputDir)
	}
	args = append(args, "--json")

	workspace, err := RunJSON[WorkspaceCreateResult](c, ctx, args)
	if err != nil {
		return WorkspaceCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return workspace, nil
}

func (c *Client) DeleteWorkspace(ctx context.Context, identifier string) error {
	_, err := RunJSON[struct{}](c, ctx, []string{"delete", "workspace", identifier, "--json"})
	if err != nil {
		return err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return nil
}

func (c *Client) GetRequest(ctx context.Context, workspaceID, identifier string) (RequestGetResult, error) {
	key := c.cacheKey("request", "get", workspaceID, identifier)
	if value, ok := c.cached(key); ok {
		if request, ok := value.(RequestGetResult); ok {
			return request, nil
		}
	}

	args := []string{"get", "request", identifier, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	request, err := RunJSON[RequestGetResult](c, ctx, args)
	if err != nil {
		return RequestGetResult{}, err
	}

	c.storeAliases(request, defaultMutationCacheTTL,
		key,
		c.cacheKey("request", "get", workspaceID, request.ID),
		c.cacheKey("request", "get", workspaceID, identifier))
	return request, nil
}

func (c *Client) CreateRequest(ctx context.Context, workspaceID, name, uri string, opts RequestCreateOptions) (RequestCreateResult, error) {
	args := []string{"create", "request", name, uri}
	args = appendRequestFlags(args, opts.Method, opts.Headers, opts.Params, opts.Data, opts.BodyType, opts.Auth)
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	request, err := RunJSON[RequestCreateResult](c, ctx, args)
	if err != nil {
		return RequestCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("requests:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("request", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return request, nil
}

func (c *Client) EditRequest(ctx context.Context, workspaceID, identifier string, opts RequestEditOptions) (RequestCreateResult, error) {
	args := []string{"edit", "request", identifier}
	args = appendRequestEditFlags(args, opts)
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	request, err := RunJSON[RequestCreateResult](c, ctx, args)
	if err != nil {
		return RequestCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("requests:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("request", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return request, nil
}

func (c *Client) CopyRequest(ctx context.Context, workspaceID, identifier, newName string) (RequestCreateResult, error) {
	args := []string{"copy", "request", identifier, newName}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	request, err := RunJSON[RequestCreateResult](c, ctx, args)
	if err != nil {
		return RequestCreateResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("requests:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("request", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return request, nil
}

func (c *Client) DeleteRequest(ctx context.Context, workspaceID, identifier string) error {
	args := []string{"delete", "request", identifier, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	_, err := RunJSON[struct{}](c, ctx, args)
	if err != nil {
		return err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("requests:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("request", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return nil
}

func (c *Client) GetAuth(ctx context.Context, workspaceID, identifier string) (AuthGetResult, error) {
	key := c.cacheKey("auth", "get", workspaceID, identifier)
	if value, ok := c.cached(key); ok {
		if auth, ok := value.(AuthGetResult); ok {
			return auth, nil
		}
	}

	args := []string{"get", "auth", identifier, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	auth, err := RunJSON[AuthGetResult](c, ctx, args)
	if err != nil {
		return AuthGetResult{}, err
	}

	c.storeAliases(auth, defaultMutationCacheTTL,
		key,
		c.cacheKey("auth", "get", workspaceID, auth.ID),
		c.cacheKey("auth", "get", workspaceID, identifier))
	return auth, nil
}

func (c *Client) CreateAuth(ctx context.Context, workspaceID, name string, opts AuthCreateOptions) (AuthListItem, error) {
	args := []string{"create", "auth", name}
	args = appendAuthCreateFlags(args, opts)
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	auth, err := RunJSON[AuthListItem](c, ctx, args)
	if err != nil {
		return AuthListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("auths:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("auth", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return auth, nil
}

func (c *Client) EditAuth(ctx context.Context, workspaceID, identifier string, opts AuthEditOptions) (AuthListItem, error) {
	args := []string{"edit", "auth", identifier}
	args = appendAuthEditFlags(args, opts)
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	auth, err := RunJSON[AuthListItem](c, ctx, args)
	if err != nil {
		return AuthListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("auths:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("auth", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return auth, nil
}

func (c *Client) CopyAuth(ctx context.Context, workspaceID, identifier, newName string) (AuthListItem, error) {
	args := []string{"copy", "auth", identifier, newName}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}
	args = append(args, "--json")

	auth, err := RunJSON[AuthListItem](c, ctx, args)
	if err != nil {
		return AuthListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("auths:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("auth", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return auth, nil
}

func (c *Client) DeleteAuth(ctx context.Context, workspaceID, identifier string) error {
	args := []string{"delete", "auth", identifier, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	_, err := RunJSON[struct{}](c, ctx, args)
	if err != nil {
		return err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("auths:list", workspaceID))
	c.invalidatePrefix(c.cacheKey("auth", "get", workspaceID))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return nil
}

func (c *Client) GetSecret(ctx context.Context, identifier string) (SecretGetResult, error) {
	key := c.cacheKey("secret", "get", identifier)
	if value, ok := c.cached(key); ok {
		if secret, ok := value.(SecretGetResult); ok {
			return secret, nil
		}
	}

	secret, err := RunJSON[SecretGetResult](c, ctx, []string{"get", "secret", identifier, "--json"})
	if err != nil {
		return SecretGetResult{}, err
	}

	c.storeAliases(secret, defaultMutationCacheTTL, key, c.cacheKey("secret", "get", secret.ID))
	return secret, nil
}

func (c *Client) CreateSecret(ctx context.Context, opts SecretCreateOptions) (SecretListItem, error) {
	secret, err := RunJSON[SecretListItem](c, ctx, []string{"create", "secret", opts.Name, opts.Value, "--json"})
	if err != nil {
		return SecretListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("secrets:list"))
	c.invalidatePrefix(c.cacheKey("secret", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return secret, nil
}

func (c *Client) EditSecret(ctx context.Context, identifier string, opts SecretEditOptions) (SecretListItem, error) {
	args := []string{"edit", "secret", identifier}
	if opts.Name != nil {
		args = append(args, "--name", *opts.Name)
	}
	if opts.Value != nil {
		args = append(args, "--value", *opts.Value)
	}
	args = append(args, "--json")

	secret, err := RunJSON[SecretListItem](c, ctx, args)
	if err != nil {
		return SecretListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("secrets:list"))
	c.invalidatePrefix(c.cacheKey("secret", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return secret, nil
}

func (c *Client) CopySecret(ctx context.Context, identifier, newName string) (SecretListItem, error) {
	secret, err := RunJSON[SecretListItem](c, ctx, []string{"copy", "secret", identifier, newName, "--json"})
	if err != nil {
		return SecretListItem{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("secrets:list"))
	c.invalidatePrefix(c.cacheKey("secret", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return secret, nil
}

func (c *Client) DeleteSecret(ctx context.Context, identifier string) (SecretDeleteResult, error) {
	secret, err := RunJSON[SecretDeleteResult](c, ctx, []string{"delete", "secret", identifier, "--json"})
	if err != nil {
		return SecretDeleteResult{}, err
	}

	c.invalidatePrefix(c.cacheKey("workspace", "get"))
	c.invalidateKeys(c.cacheKey("secrets:list"))
	c.invalidatePrefix(c.cacheKey("secret", "get"))
	c.invalidateKeys(c.cacheKey("workspaces:list"))
	return secret, nil
}

func (c *Client) DryRunRequest(ctx context.Context, requestID, workspaceID string, headers, params []string) (DryRunResult, error) {
	args := []string{"send", requestID, "--dry-run", "--json"}
	for _, header := range headers {
		if header == "" {
			continue
		}
		args = append(args, "--header", header)
	}
	for _, param := range params {
		if param == "" {
			continue
		}
		args = append(args, "--param", param)
	}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	return RunJSON[DryRunResult](c, ctx, args)
}

func (c *Client) SendRequest(ctx context.Context, requestID, workspaceID string) (SendSummary, error) {
	args := []string{"send", requestID, "--json"}
	if workspaceID != "" {
		args = append(args, "--workspace", workspaceID)
	}

	return RunJSON[SendSummary](c, ctx, args)
}

func appendRequestFlags(args []string, method string, headers, params []string, data, bodyType, auth string) []string {
	args = appendIfValue(args, "--method", method)
	for _, header := range headers {
		if header == "" {
			continue
		}
		args = append(args, "--header", header)
	}
	for _, param := range params {
		if param == "" {
			continue
		}
		args = append(args, "--param", param)
	}
	args = appendIfValue(args, "--data", data)
	args = appendIfValue(args, "--type", bodyType)
	args = appendIfValue(args, "--auth", auth)
	return args
}

func appendRequestEditFlags(args []string, opts RequestEditOptions) []string {
	args = appendIfPointer(args, "--name", opts.Name)
	args = appendIfPointer(args, "--url", opts.Uri)
	args = appendIfPointer(args, "--method", opts.Method)
	for _, header := range opts.Headers {
		if header == "" {
			continue
		}
		args = append(args, "--header", header)
	}
	for _, param := range opts.Params {
		if param == "" {
			continue
		}
		args = append(args, "--param", param)
	}
	args = appendIfPointer(args, "--data", opts.Data)
	args = appendIfPointer(args, "--type", opts.BodyType)
	args = appendIfPointer(args, "--auth", opts.Auth)
	return args
}

func appendAuthCreateFlags(args []string, opts AuthCreateOptions) []string {
	args = appendIfValue(args, "-t", opts.Type)
	args = appendAuthMutationFlags(args, opts.Secret, opts.Prefix, opts.Username, opts.Password, opts.GrantType,
		opts.TokenURL, opts.ClientID, opts.ClientSecret, opts.Scope, opts.AuthorizationURL, opts.RedirectURI,
		opts.PKCE, opts.CustomURL, opts.CustomMethod, opts.CustomHeaders, opts.CustomParams, opts.CustomBody,
		opts.CustomBodyType, opts.ExtractionSource, opts.ExtractionExpression, opts.ApplyHeaderName,
		opts.ApplyHeaderTemplate, opts.AutoRenew)
	return args
}

func appendAuthEditFlags(args []string, opts AuthEditOptions) []string {
	args = appendIfPointer(args, "--type", opts.Type)
	args = appendAuthMutationFlags(args, opts.Secret, opts.Prefix, opts.Username, opts.Password, opts.GrantType,
		opts.TokenURL, opts.ClientID, opts.ClientSecret, opts.Scope, opts.AuthorizationURL, opts.RedirectURI,
		opts.PKCE, opts.CustomURL, opts.CustomMethod, opts.CustomHeaders, opts.CustomParams, opts.CustomBody,
		opts.CustomBodyType, opts.ExtractionSource, opts.ExtractionExpression, opts.ApplyHeaderName,
		opts.ApplyHeaderTemplate, opts.AutoRenew)
	return args
}

func appendAuthMutationFlags(
	args []string,
	secret, prefix, username, password, grantType, tokenURL, clientID, clientSecret, scope, authorizationURL,
	redirectURI, pkce, customURL, customMethod *string,
	customHeaders, customParams []string,
	customBody, customBodyType, extractionSource, extractionExpression, applyHeaderName, applyHeaderTemplate *string,
	autoRenew *bool,
) []string {
	args = appendIfPointer(args, "-s", secret)
	args = appendIfPointer(args, "--prefix", prefix)
	args = appendIfPointer(args, "-u", username)
	args = appendIfPointer(args, "-p", password)
	args = appendIfPointer(args, "-g", grantType)
	args = appendIfPointer(args, "--token-url", tokenURL)
	args = appendIfPointer(args, "--client-id", clientID)
	args = appendIfPointer(args, "--client-secret", clientSecret)
	args = appendIfPointer(args, "--scope", scope)
	args = appendIfPointer(args, "--authorization-url", authorizationURL)
	args = appendIfPointer(args, "--redirect-uri", redirectURI)
	args = appendIfPointer(args, "--pkce", pkce)
	args = appendIfPointer(args, "--custom-url", customURL)
	args = appendIfPointer(args, "--custom-method", customMethod)
	for _, header := range customHeaders {
		if header == "" {
			continue
		}
		args = append(args, "--custom-header", header)
	}
	for _, param := range customParams {
		if param == "" {
			continue
		}
		args = append(args, "--custom-param", param)
	}
	args = appendIfPointer(args, "--custom-body", customBody)
	args = appendIfPointer(args, "--custom-body-type", customBodyType)
	args = appendIfPointer(args, "--extraction-source", extractionSource)
	args = appendIfPointer(args, "--extraction-expression", extractionExpression)
	args = appendIfPointer(args, "--apply-header-name", applyHeaderName)
	args = appendIfPointer(args, "--apply-header-template", applyHeaderTemplate)
	if autoRenew != nil {
		if *autoRenew {
			args = append(args, "--auto-renew")
		} else {
			args = append(args, "--no-auto-renew")
		}
	}
	return args
}

func appendIfValue(args []string, flag, value string) []string {
	if value == "" {
		return args
	}

	return append(args, flag, value)
}

func appendIfPointer(args []string, flag string, value *string) []string {
	if value == nil || *value == "" {
		return args
	}

	return append(args, flag, *value)
}
