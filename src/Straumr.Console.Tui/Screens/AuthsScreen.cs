using System.Diagnostics;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class AuthsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    ScreenNavigationContext navigationContext,
    StraumrTheme theme)
    : ModelScreen<AuthEntry>(theme,
        screenTitle: "Auths",
        emptyStateText: "No auths found",
        itemTypeNamePlural: "auths")
{
    private const string ActionFinish = "Finish";
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionAutoRenew = "Toggle auto-renew";
    private const string ActionFetch = "Fetch token/value";

    private StraumrWorkspaceEntry? _workspaceEntry;
    private bool _editorActive;

    protected override string ModelHintsText => "c Create  d Delete  e Edit  y Copy";

    protected override void OnInitialized()
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            ShowInfo("No workspace selected. Use the workspaces screen to activate one.");
            return;
        }

        try
        {
            StraumrWorkspace workspace =
                workspaceService.PeekWorkspace(_workspaceEntry.Path).GetAwaiter().GetResult();
            int authCount = workspace.Auths.Count;
            ShowSuccess($"{authCount} auth{(authCount == 1 ? string.Empty : "s")} loaded");
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
        }
    }

    protected override string GetDisplayText(AuthEntry entry) => entry.Display;

    protected override bool IsSameEntry(AuthEntry? left, AuthEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, AuthEntry? selectedEntry)
    {
        if (_editorActive)
        {
            return true;
        }

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 'c':
                    CreateAuth();
                    return true;
                case 'd':
                    DeleteAuth(selectedEntry);
                    return true;
                case 'e':
                    EditAuth(selectedEntry);
                    return true;
                case 'y':
                    CopyAuth(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void CreateAuth()
    {
        if (_editorActive)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        AuthEditorState state = AuthEditorState.CreateNew();
        RunAuthEditor(state, workspaceEntry, null);
    }

    private void EditAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null || selectedEntry.IsDamaged)
        {
            return;
        }

        if (_editorActive)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        StraumrAuth auth;
        try
        {
            auth = authService.GetAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
            return;
        }

        AuthEditorState state = AuthEditorState.FromAuth(auth);
        RunAuthEditor(state, workspaceEntry, auth);
    }

    private void DeleteAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? confirm = interactiveConsole.Select(
            $"Delete \"{selectedEntry.Identifier}\"?",
            ["Cancel", "Delete"],
            enableFilter: false,
            enableTypeahead: true);

        if (confirm is not "Delete")
        {
            return;
        }

        try
        {
            authService.DeleteAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Deleted auth \"{selectedEntry.Identifier}\".");
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
        }
    }

    private void CopyAuth(AuthEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot copy damaged auth \"{selectedEntry.Identifier}\".");
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? newName = interactiveConsole.TextInput(
            "New name",
            selectedEntry.Name ?? selectedEntry.Identifier,
            validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
        if (newName is null)
        {
            return;
        }

        try
        {
            authService.CopyAsync(selectedEntry.Id.ToString(), newName, workspaceEntry).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Copied auth to \"{newName}\".");
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
        }
    }

    protected override void OpenSelectedEntry() { }

    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        StraumrAuth? auth = null;
        if (TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry) && !SelectedEntry.IsDamaged)
        {
            try
            {
                auth = authService.GetAsync(SelectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        string typeDisplay = auth is not null ? GetAuthTypeName(auth.Config) : "[secondary]N/A[/]";
        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Type", typeDisplay),
                ("Auto Renew", SelectedEntry.AutoRenew ? "[success]Enabled[/]" : "[secondary]Disabled[/]"),
                ("Modified",
                    SelectedEntry.Modified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[secondary]N/A[/]"),
                ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
            ]);
    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("create", _ => CreateAuth(), "new");
        yield return new ModelCommand("delete", _ => DeleteAuth(SelectedEntry), "rm", "remove");
        yield return new ModelCommand("edit", _ => EditAuth(SelectedEntry));
        yield return new ModelCommand("copy", _ => CopyAuth(SelectedEntry), "cp");
    }

    protected override async Task<IReadOnlyList<AuthEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to the workspaces menu. Set an active workspace to continue.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetWorkspace(_workspaceEntry.Path);
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            interactiveConsole.ShowMessage("Workspace corrupt",
                "Your selected workspace is corrupt. Your active workspace will be cleared and you will be navigated to the workspaces menu.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.MissingEntry)
        {
            interactiveConsole.ShowMessage("Workspace missing",
                "Your selected workspace is missing from disk. Your active workspace will be cleared and you will be navigated to the workspaces menu.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        string workspaceName = string.IsNullOrWhiteSpace(workspace.Name)
            ? _workspaceEntry.Id.ToString()
            : workspace.Name;
        UpdateTitle($"Auths - {workspaceName}");

        var entries = new List<AuthEntry>();
        foreach (Guid authId in workspace.Auths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await BuildAuthEntryAsync(authId));
        }

        return entries
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private async Task<AuthEntry> BuildAuthEntryAsync(Guid authId)
    {
        string identifier = authId.ToString();
        string status = "Valid";
        bool isDamaged = false;
        string? type = null;
        bool autoRenew = false;
        DateTimeOffset? lastAccessed = null;
        DateTimeOffset? modified = null;
        string? name = null;
        string display;

        try
        {
            StraumrAuth auth = await authService.PeekByIdAsync(authId, _workspaceEntry);
            identifier = auth.Name;
            name = auth.Name;
            type = GetAuthTypeName(auth.Config);
            autoRenew = auth.AutoRenewAuth;
            lastAccessed = auth.LastAccessed;
            modified = auth.Modified;

            string state = autoRenew ? "Auto renew" : "Manual";
            string line0 = $"[accent]◇ {type}[/] [bold]{auth.Name}[/]";
            string line1 = $"  [secondary]{auth.Id}[/]";
            string line2 = $"  [secondary]{state}  [/][info]{auth.Modified.LocalDateTime:yyyy-MM-dd}[/]";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]✖[/] [bold]{authId}[/]  [danger](Corrupt)[/]\n  [danger]Auth file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]✖[/] [bold]{authId}[/]  [warning](Missing)[/]\n  [warning]Auth file is missing[/]";
        }

        return new AuthEntry
        {
            Id = authId,
            Display = display,
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            Type = type,
            AutoRenew = autoRenew,
            LastAccessed = lastAccessed,
            Modified = modified,
            Name = name
        };
    }

    private bool TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry)
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to the workspaces menu. Set an active workspace to continue.");
            NavigateTo<WorkspacesScreen>();
            workspaceEntry = null!;
            return false;
        }

        workspaceEntry = _workspaceEntry;
        return true;
    }

    private void RunAuthEditor(AuthEditorState state, StraumrWorkspaceEntry workspaceEntry, StraumrAuth? existingAuth)
    {
        if (_editorActive)
        {
            return;
        }

        _editorActive = true;
        string completionAction = existingAuth is null ? ActionFinish : ActionSave;
        string promptTitle = existingAuth is null ? "Create auth" : "Edit auth";

        try
        {
            while (true)
            {
                string? action = interactiveConsole.Select(
                    promptTitle,
                    BuildAuthEditorChoices(completionAction, state),
                    choice => DescribeAuthMenuChoice(choice, state, completionAction));

                if (action is null)
                {
                    return;
                }

                if (action == completionAction)
                {
                    if (TryPersistAuth(state, workspaceEntry, existingAuth))
                    {
                        return;
                    }

                    continue;
                }

                HandleAuthEditAction(action, state);
            }
        }
        finally
        {
            _editorActive = false;
            FocusList();
        }
    }

    private IReadOnlyList<string> BuildAuthEditorChoices(string completionAction, AuthEditorState state)
    {
        List<string> choices = [completionAction, ActionName, ActionConfigure, ActionAutoRenew];
        if (SupportsAuthFetch(state.Config))
        {
            choices.Add(ActionFetch);
        }

        return choices;
    }

    private string DescribeAuthMenuChoice(string action, AuthEditorState state, string completionAction)
    {
        return action switch
        {
            var value when value == completionAction => completionAction,
            ActionName => $"Name: {FormatAuthName(state.Name)}",
            ActionConfigure => $"Auth: {DescribeAuth(state.Config)}",
            ActionAutoRenew => $"Auto-renew auth: {(state.AutoRenew ? "[success]enabled[/]" : "[secondary]disabled[/]")}",
            _ => action
        };
    }

    private static string FormatAuthName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "[secondary]not set[/]" : $"[blue]{name}[/]";
    }

    private bool TryPersistAuth(AuthEditorState state, StraumrWorkspaceEntry workspaceEntry, StraumrAuth? existingAuth)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("Validation", "A name is required.");
            return false;
        }

        if (state.Config is null)
        {
            interactiveConsole.ShowMessage("Validation", "Configure an auth definition before saving.");
            return false;
        }

        try
        {
            if (existingAuth is null)
            {
                StraumrAuth auth = new()
                {
                    Name = state.Name,
                    Config = state.CloneConfig(),
                    AutoRenewAuth = state.AutoRenew
                };
                authService.CreateAsync(auth, workspaceEntry).GetAwaiter().GetResult();
                _ = RefreshAsync();
                ShowSuccess($"Created auth \"{auth.Name}\".");
            }
            else
            {
                existingAuth.Name = state.Name;
                existingAuth.Config = state.CloneConfig();
                existingAuth.AutoRenewAuth = state.AutoRenew;
                authService.UpdateAsync(existingAuth, workspaceEntry).GetAwaiter().GetResult();
                _ = RefreshAsync();
                ShowSuccess($"Updated auth \"{existingAuth.Name}\".");
            }

            return true;
        }
        catch (StraumrException ex)
        {
            ShowDanger(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            ShowDanger(ex.Message);
            return false;
        }
    }

    private void HandleAuthEditAction(string action, AuthEditorState state)
    {
        switch (action)
        {
            case ActionName:
            {
                string? updated = interactiveConsole.TextInput(
                    "Name",
                    state.Name,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionConfigure:
                ConfigureAuth(state);
                break;
            case ActionAutoRenew:
                state.AutoRenew = !state.AutoRenew;
                break;
            case ActionFetch:
                FetchAuthValue(state);
                break;
        }
    }

    private void ConfigureAuth(AuthEditorState state)
    {
        const string bearerOption = "Bearer";
        const string basicOption = "Basic";
        const string oauthOption = "OAuth 2.0";
        const string customOption = "Custom";
        const string noneOption = "No auth";

        string current = state.Config switch
        {
            BearerAuthConfig => bearerOption,
            BasicAuthConfig => basicOption,
            OAuth2Config => oauthOption,
            CustomAuthConfig => customOption,
            _ => noneOption
        };

        string? selected = interactiveConsole.Select(
            "Auth type",
            [bearerOption, basicOption, oauthOption, customOption, noneOption],
            choice => choice == current ? $"{choice} [accent](current)[/]" : choice);

        if (selected is null)
        {
            return;
        }

        state.Config = selected switch
        {
            bearerOption => state.Config as BearerAuthConfig ?? new BearerAuthConfig(),
            basicOption => state.Config as BasicAuthConfig ?? new BasicAuthConfig(),
            oauthOption => state.Config as OAuth2Config ?? new OAuth2Config(),
            customOption => state.Config as CustomAuthConfig ?? new CustomAuthConfig(),
            _ => null
        };

        switch (state.Config)
        {
            case BearerAuthConfig bearer:
                EditBearerAuth(bearer);
                break;
            case BasicAuthConfig basic:
                EditBasicAuth(basic);
                break;
            case OAuth2Config oauth2:
                EditOAuth2Config(oauth2);
                break;
            case CustomAuthConfig custom:
                EditCustomAuthConfig(custom);
                break;
        }
    }

    private void EditBearerAuth(BearerAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Bearer auth",
                ["Back", "Header prefix", "Token"],
                choice => choice switch
                {
                    "Header prefix" => $"Prefix: [blue]{config.Prefix}[/]",
                    "Token" => string.IsNullOrWhiteSpace(config.Token) ? "Token: [secondary]not set[/]" : "Token: [success]set[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Header prefix":
                {
                    string? prefix = interactiveConsole.TextInput(
                        "Header prefix",
                        config.Prefix,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Prefix cannot be empty." : null);
                    if (prefix is not null)
                    {
                        config.Prefix = prefix;
                    }

                    break;
                }
                case "Token":
                {
                    string? token = interactiveConsole.SecretInput("Token");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        config.Token = token;
                    }

                    break;
                }
            }
        }
    }

    private void EditBasicAuth(BasicAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Basic auth",
                ["Back", "Username", "Password"],
                choice => choice switch
                {
                    "Username" => string.IsNullOrWhiteSpace(config.Username)
                        ? "Username: [secondary]not set[/]"
                        : $"Username: [blue]{config.Username}[/]",
                    "Password" => string.IsNullOrWhiteSpace(config.Password)
                        ? "Password: [secondary]not set[/]"
                        : "Password: [success]set[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Username":
                {
                    string? username = interactiveConsole.TextInput(
                        "Username",
                        config.Username,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Username cannot be empty." : null);
                    if (username is not null)
                    {
                        config.Username = username;
                    }

                    break;
                }
                case "Password":
                {
                    string? password = interactiveConsole.SecretInput("Password");
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        config.Password = password;
                    }

                    break;
                }
            }
        }
    }

    private void EditOAuth2Config(OAuth2Config config)
    {
        while (true)
        {
            List<string> choices =
            [
                "Back",
                "Grant type",
                "Token URL",
                "Client ID",
                "Client secret",
                "Scope"
            ];

            Func<string, string> converter = choice => choice switch
            {
                "Grant type" => $"Grant type: [blue]{FormatGrant(config.GrantType)}[/]",
                "Token URL" => $"Token URL: {(string.IsNullOrWhiteSpace(config.TokenUrl) ? "[secondary]not set[/]" : $"[blue]{config.TokenUrl}[/]")}",
                "Client ID" => $"Client ID: {(string.IsNullOrWhiteSpace(config.ClientId) ? "[secondary]not set[/]" : $"[blue]{config.ClientId}[/]")}",
                "Client secret" => $"Client secret: {(string.IsNullOrWhiteSpace(config.ClientSecret) ? "[secondary]not set[/]" : "[success]set[/]")}",
                "Scope" => $"Scope: {(string.IsNullOrWhiteSpace(config.Scope) ? "[secondary]not set[/]" : $"[blue]{config.Scope}[/]")}",
                _ => choice
            };

            if (config.GrantType == OAuth2GrantType.AuthorizationCode)
            {
                choices.AddRange(["Authorization URL", "Redirect URI", "PKCE"]);
                converter = choice => choice switch
                {
                    "Authorization URL" => $"Authorization URL: {(string.IsNullOrWhiteSpace(config.AuthorizationUrl) ? "[secondary]not set[/]" : $"[blue]{config.AuthorizationUrl}[/]")}",
                    "Redirect URI" => $"Redirect URI: [blue]{config.RedirectUri}[/]",
                    "PKCE" => $"PKCE: {(config.UsePkce ? $"[success]Enabled[/] ({config.CodeChallengeMethod})" : "[secondary]Disabled[/]")}",
                    _ => converter(choice)
                };
            }
            else if (config.GrantType == OAuth2GrantType.ResourceOwnerPassword)
            {
                choices.AddRange(["Username", "Password"]);
                converter = choice => choice switch
                {
                    "Username" => $"Username: {(string.IsNullOrWhiteSpace(config.Username) ? "[secondary]not set[/]" : $"[blue]{config.Username}[/]")}",
                    "Password" => $"Password: {(string.IsNullOrWhiteSpace(config.Password) ? "[secondary]not set[/]" : "[success]set[/]")}",
                    _ => converter(choice)
                };
            }

            string? action = interactiveConsole.Select("OAuth 2.0 configuration", choices, converter);
            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Grant type":
                    config.GrantType = PromptGrantType(config.GrantType);
                    break;
                case "Token URL":
                {
                    string? url = PromptUrl(config.TokenUrl);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.TokenUrl = url;
                    }

                    break;
                }
                case "Client ID":
                {
                    string? clientId = interactiveConsole.TextInput("Client ID", config.ClientId);
                    if (clientId is not null)
                    {
                        config.ClientId = clientId;
                    }

                    break;
                }
                case "Client secret":
                {
                    string? secret = interactiveConsole.SecretInput("Client secret");
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        config.ClientSecret = secret;
                    }

                    break;
                }
                case "Scope":
                {
                    string? scope = interactiveConsole.TextInput("Scope", config.Scope ?? string.Empty, allowEmpty: true);
                    if (scope is not null)
                    {
                        config.Scope = scope;
                    }

                    break;
                }
                case "Authorization URL":
                {
                    string? url = PromptUrl(config.AuthorizationUrl);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.AuthorizationUrl = url;
                    }

                    break;
                }
                case "Redirect URI":
                {
                    string? redirect = interactiveConsole.TextInput("Redirect URI", config.RedirectUri, allowEmpty: false);
                    if (redirect is not null)
                    {
                        config.RedirectUri = redirect;
                    }

                    break;
                }
                case "PKCE":
                {
                    string? pkce = interactiveConsole.Select("PKCE",
                        ["Disabled", "S256", "Plain"],
                        choice => choice == "Disabled"
                            ? "[secondary]Disabled[/]"
                            : $"[blue]{choice}[/]");
                    if (pkce is not null)
                    {
                        config.UsePkce = pkce != "Disabled";
                        config.CodeChallengeMethod = pkce switch
                        {
                            "Plain" => "plain",
                            _ => "S256"
                        };
                    }

                    break;
                }
                case "Username":
                {
                    string? username = interactiveConsole.TextInput("Username", config.Username);
                    if (username is not null)
                    {
                        config.Username = username;
                    }

                    break;
                }
                case "Password":
                {
                    string? password = interactiveConsole.SecretInput("Password");
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        config.Password = password;
                    }

                    break;
                }
            }
        }
    }

    private static string FormatGrant(OAuth2GrantType grantType)
    {
        return grantType switch
        {
            OAuth2GrantType.ClientCredentials => "Client Credentials",
            OAuth2GrantType.AuthorizationCode => "Authorization Code",
            OAuth2GrantType.ResourceOwnerPassword => "Password",
            _ => grantType.ToString()
        };
    }

    private OAuth2GrantType PromptGrantType(OAuth2GrantType current)
    {
        string currentDisplay = FormatGrant(current);
        string? selected = interactiveConsole.Select(
            "Grant type",
            ["Client Credentials", "Authorization Code", "Password"],
            choice => choice == currentDisplay ? $"{choice} [accent](current)[/]" : choice);

        return selected switch
        {
            "Authorization Code" => OAuth2GrantType.AuthorizationCode,
            "Password" => OAuth2GrantType.ResourceOwnerPassword,
            _ => OAuth2GrantType.ClientCredentials
        };
    }

    private void EditCustomAuthConfig(CustomAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Custom auth",
                [
                    "Back",
                    "URL",
                    "Method",
                    "Headers",
                    "Params",
                    "Body",
                    "Extraction source",
                    "Extraction expression",
                    "Apply header name",
                    "Apply header template"
                ],
                choice => choice switch
                {
                    "URL" => $"URL: {(string.IsNullOrWhiteSpace(config.Url) ? "[secondary]not set[/]" : $"[blue]{config.Url}[/]")}",
                    "Method" => $"Method: [blue]{config.Method}[/]",
                    "Headers" => $"Headers: {(config.Headers.Count == 0 ? "[secondary]none[/]" : $"[blue]{config.Headers.Count}[/]")}",
                    "Params" => $"Params: {(config.Params.Count == 0 ? "[secondary]none[/]" : $"[blue]{config.Params.Count}[/]")}",
                    "Body" => $"Body: [blue]{RequestEditingHelpers.BodyTypeDisplayName(config.BodyType)}[/]",
                    "Extraction source" => $"Extract: [blue]{config.Source}[/]",
                    "Extraction expression" => $"Expression: {(string.IsNullOrWhiteSpace(config.ExtractionExpression) ? "[secondary]not set[/]" : $"[blue]{config.ExtractionExpression}[/]")}",
                    "Apply header name" => $"Header: [blue]{config.ApplyHeaderName}[/]",
                    "Apply header template" => $"Template: [blue]{config.ApplyHeaderTemplate}[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "URL":
                {
                    string? url = PromptUrl(config.Url);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.Url = url;
                    }

                    break;
                }
                case "Method":
                {
                    string? method = PromptMethod(config.Method);
                    if (method is not null)
                    {
                        config.Method = method;
                    }

                    break;
                }
                case "Headers":
                    EditKeyValuePairs("Headers", config.Headers);
                    break;
                case "Params":
                    EditKeyValuePairs("Params", config.Params);
                    break;
                case "Body":
                    config.BodyType = EditBody(config.Headers, config.Bodies, config.BodyType);
                    break;
                case "Extraction source":
                {
                    string? selected = interactiveConsole.Select(
                        "Extract value from",
                        ["JSON path", "Response header", "Regex"],
                        choice => config.Source switch
                        {
                            ExtractionSource.JsonPath when choice == "JSON path" => $"{choice} [accent](current)[/]",
                            ExtractionSource.ResponseHeader when choice == "Response header" => $"{choice} [accent](current)[/]",
                            ExtractionSource.Regex when choice == "Regex" => $"{choice} [accent](current)[/]",
                            _ => choice
                        });
                    if (selected is not null)
                    {
                        config.Source = selected switch
                        {
                            "Response header" => ExtractionSource.ResponseHeader,
                            "Regex" => ExtractionSource.Regex,
                            _ => ExtractionSource.JsonPath
                        };
                    }

                    break;
                }
                case "Extraction expression":
                {
                    string? expression = interactiveConsole.TextInput(
                        "Extraction expression",
                        config.ExtractionExpression,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Expression cannot be empty." : null);
                    if (expression is not null)
                    {
                        config.ExtractionExpression = expression;
                    }

                    break;
                }
                case "Apply header name":
                {
                    string? header = interactiveConsole.TextInput(
                        "Header name",
                        config.ApplyHeaderName,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Header name cannot be empty." : null);
                    if (header is not null)
                    {
                        config.ApplyHeaderName = header;
                    }

                    break;
                }
                case "Apply header template":
                {
                    string? template = interactiveConsole.TextInput(
                        "Header value template (use {{value}})",
                        config.ApplyHeaderTemplate,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Template cannot be empty." : null);
                    if (template is not null)
                    {
                        config.ApplyHeaderTemplate = template;
                    }

                    break;
                }
            }
        }
    }

    private void EditKeyValuePairs(string title, IDictionary<string, string> items)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                title,
                ["Back", "Add or update", "Remove", "List"]);

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Add or update":
                {
                    string? key = interactiveConsole.TextInput(
                        $"{title} name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    items.TryGetValue(key, out string? existing);
                    string? value = interactiveConsole.TextInput($"{title} value", existing);
                    if (value is not null)
                    {
                        items[key] = value;
                    }

                    break;
                }
                case "Remove":
                {
                    if (items.Count == 0)
                    {
                        interactiveConsole.ShowMessage($"No {title.ToLowerInvariant()} to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select(
                        $"Select {title.ToLowerInvariant()} to remove",
                        items.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        items.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    interactiveConsole.ShowTable(
                        "Name",
                        "Value",
                        items.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        $"No {title.ToLowerInvariant()} set.");
                    break;
                }
            }
        }
    }

    private BodyType EditBody(
        IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies,
        BodyType currentType)
    {
        while (true)
        {
            string typeDisplay = currentType == BodyType.None
                ? "[secondary]none[/]"
                : $"[blue]{RequestEditingHelpers.BodyTypeDisplayName(currentType)}[/]";

            bool hasContent = currentType != BodyType.None && bodies.ContainsKey(currentType);
            string contentDisplay = hasContent ? "[success]set[/]" : "[secondary]empty[/]";

            string? action = interactiveConsole.Select(
                "Body",
                ["Back", "Body type", "Edit body", "Clear body"],
                choice => choice switch
                {
                    "Body type" => $"Type: {typeDisplay}",
                    "Edit body" => $"Content: {contentDisplay}",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return currentType;
            }

            switch (action)
            {
                case "Body type":
                {
                    string? selected = interactiveConsole.Select(
                        "Select body type",
                        [
                            "No body",
                            "JSON",
                            "XML",
                            "Text",
                            "Form URL Encoded",
                            "Multipart Form",
                            "Raw"
                        ]);

                    if (selected is not null)
                    {
                        currentType = selected switch
                        {
                            "JSON" => BodyType.Json,
                            "XML" => BodyType.Xml,
                            "Text" => BodyType.Text,
                            "Form URL Encoded" => BodyType.FormUrlEncoded,
                            "Multipart Form" => BodyType.MultipartForm,
                            "Raw" => BodyType.Raw,
                            _ => BodyType.None
                        };

                        SyncContentTypeHeader(headers, currentType);
                    }

                    break;
                }
                case "Edit body":
                {
                    bodies.TryGetValue(currentType, out string? current);
                    if (currentType == BodyType.FormUrlEncoded)
                    {
                        string? edited = EditFormBody(current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else if (currentType == BodyType.MultipartForm)
                    {
                        string? edited = EditMultipartBody(current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else
                    {
                        string defaultContent = current ?? string.Empty;
                        string? edited = EditBodyWithEditor(defaultContent);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }

                    break;
                }
                case "Clear body":
                    bodies.Remove(currentType);
                    break;
            }
        }
    }

    private string? EditFormBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            string? action = interactiveConsole.Select(
                "Form fields",
                ["Back", "Add or update", "Remove", "List"]);

            if (action is null or "Back")
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case "Add or update":
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    fields.TryGetValue(key, out string? existing);
                    string? value = interactiveConsole.TextInput("Field value", existing);
                    if (value is not null)
                    {
                        fields[key] = value;
                    }

                    break;
                }
                case "Remove":
                {
                    if (fields.Count == 0)
                    {
                        interactiveConsole.ShowMessage("No fields to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select(
                        "Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    interactiveConsole.ShowTable(
                        "Name",
                        "Value",
                        fields.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        "No fields set.");
                    break;
                }
            }
        }
    }

    private string? EditMultipartBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            string? action = interactiveConsole.Select(
                "Multipart form fields",
                ["Back", "Add text field", "Add file field", "Remove", "List"]);

            if (action is null or "Back")
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case "Add text field":
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput("Field value");
                    if (value is not null)
                    {
                        fields[key] = value;
                    }

                    break;
                }
                case "Add file field":
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput("File path");
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        fields[key] = value.StartsWith('@') ? value : $"@{value}";
                    }

                    break;
                }
                case "Remove":
                {
                    if (fields.Count == 0)
                    {
                        interactiveConsole.ShowMessage("No fields to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select(
                        "Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    IEnumerable<(string Key, string Value)> rows = fields
                        .OrderBy(k => k.Key)
                        .Select(kv =>
                        {
                            string value = kv.Value.StartsWith('@')
                                ? $"@{Path.GetFileName(kv.Value[1..])}"
                                : kv.Value;
                            return (kv.Key, value);
                        });

                    interactiveConsole.ShowTable("Name", "Value", rows, "No fields set.");
                    break;
                }
            }
        }
    }

    private string? EditBodyWithEditor(string content)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            interactiveConsole.ShowMessage("Editor", "Set the $EDITOR environment variable to edit body content.");
            return content;
        }

        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, content);
            Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
            {
                UseShellExecute = false
            });

            if (process is null)
            {
                interactiveConsole.ShowMessage("Editor", "Failed to launch editor.");
                return content;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                interactiveConsole.ShowMessage("Editor", "Editor exited with an error. Changes discarded.");
                return content;
            }

            string edited = File.ReadAllText(tempPath);
            return string.IsNullOrWhiteSpace(edited) ? null : edited;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Dictionary<string, string> ParseFormFields(string? body)
    {
        Dictionary<string, string> fields = new();
        if (string.IsNullOrWhiteSpace(body))
        {
            return fields;
        }

        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0)
            {
                fields[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                fields[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return fields;
    }

    private static string? SerializeFormFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
        {
            return null;
        }

        return string.Join('&', fields.Select(kv =>
            $"{RequestEditingHelpers.EscapeFormFieldComponent(kv.Key)}={RequestEditingHelpers.EscapeFormFieldComponent(kv.Value)}"));
    }

    private static void SyncContentTypeHeader(IDictionary<string, string> headers, BodyType bodyType)
    {
        const string contentTypeHeader = "Content-Type";
        switch (bodyType)
        {
            case BodyType.Json:
                headers[contentTypeHeader] = "application/json";
                break;
            case BodyType.Xml:
                headers[contentTypeHeader] = "application/xml";
                break;
            case BodyType.Text:
                headers[contentTypeHeader] = "text/plain";
                break;
            case BodyType.FormUrlEncoded:
                headers[contentTypeHeader] = "application/x-www-form-urlencoded";
                break;
            case BodyType.MultipartForm:
                headers[contentTypeHeader] = "multipart/form-data";
                break;
            case BodyType.Raw:
            default:
                headers.Remove(contentTypeHeader);
                break;
        }
    }

    private void FetchAuthValue(AuthEditorState state)
    {
        if (state.Config is OAuth2Config oauth2)
        {
            try
            {
                OAuth2Token token = authService.FetchTokenAsync(oauth2).GetAwaiter().GetResult();
                oauth2.Token = token;
                string expires = token.ExpiresAt.HasValue
                    ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                    : "N/A";
                interactiveConsole.ShowMessage("OAuth 2.0", $"Token fetched successfully.\nExpires: {expires}");
            }
            catch (Exception ex)
            {
                interactiveConsole.ShowMessage("OAuth 2.0", $"Failed to fetch token: {ex.Message}");
            }
        }
        else if (state.Config is CustomAuthConfig custom)
        {
            try
            {
                string value = authService.ExecuteCustomAuthAsync(custom).GetAwaiter().GetResult();
                custom.CachedValue = value;
                interactiveConsole.ShowMessage("Custom auth", "Value fetched and cached successfully.");
            }
            catch (Exception ex)
            {
                interactiveConsole.ShowMessage("Custom auth", $"Failed to fetch value: {ex.Message}");
            }
        }
        else
        {
            interactiveConsole.ShowMessage("Fetch auth", "Selected auth type does not support fetching values.");
        }
    }

    private static bool SupportsAuthFetch(StraumrAuthConfig? config)
        => config is OAuth2Config or CustomAuthConfig;

    private static string DescribeAuth(StraumrAuthConfig? config)
    {
        return config switch
        {
            null => "[secondary]none[/]",
            BearerAuthConfig bearer => string.IsNullOrWhiteSpace(bearer.Token)
                ? "[blue]Bearer[/] [secondary]token not set[/]"
                : "[blue]Bearer[/] [success]token set[/]",
            BasicAuthConfig basic => string.IsNullOrWhiteSpace(basic.Username)
                ? "[blue]Basic[/] [secondary]username not set[/]"
                : $"[blue]Basic[/] [green]{basic.Username}[/]",
            OAuth2Config => "[blue]OAuth 2.0[/]",
            CustomAuthConfig => "[blue]Custom[/]",
            _ => "[secondary]none[/]"
        };
    }

    private static string GetAuthTypeName(StraumrAuthConfig? config)
    {
        return config switch
        {
            null => "None",
            BearerAuthConfig => "Bearer",
            BasicAuthConfig => "Basic",
            OAuth2Config => "OAuth 2.0",
            CustomAuthConfig => "Custom",
            _ => "Unknown"
        };
    }

    private string? PromptUrl(string? current)
    {
        return interactiveConsole.TextInput(
            "URL",
            current,
            validate: value => IsValidAbsoluteUrl(value) ? null : "Enter a valid absolute URL.");
    }

    private string? PromptMethod(string? current)
    {
        string? selected = interactiveConsole.Select(
            "HTTP method",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"],
            choice => choice == current ? $"{choice} [accent](current)[/]" : choice);

        return selected;
    }

    private static bool IsValidAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private sealed class AuthEditorState
    {
        public string Name { get; set; } = string.Empty;
        public StraumrAuthConfig? Config { get; set; }
        public bool AutoRenew { get; set; } = true;

        public static AuthEditorState CreateNew() => new();

        public static AuthEditorState FromAuth(StraumrAuth auth)
        {
            return new AuthEditorState
            {
                Name = auth.Name,
                AutoRenew = auth.AutoRenewAuth,
                Config = CloneConfig(auth.Config)
            };
        }

        public StraumrAuthConfig CloneConfig()
        {
            return CloneConfig(Config) ?? throw new InvalidOperationException("Auth config must be set.");
        }

        private static StraumrAuthConfig? CloneConfig(StraumrAuthConfig? config)
        {
            return config switch
            {
                null => null,
                BearerAuthConfig bearer => new BearerAuthConfig
                {
                    Prefix = bearer.Prefix,
                    Token = bearer.Token
                },
                BasicAuthConfig basic => new BasicAuthConfig
                {
                    Username = basic.Username,
                    Password = basic.Password
                },
                OAuth2Config oauth2 => new OAuth2Config
                {
                    GrantType = oauth2.GrantType,
                    TokenUrl = oauth2.TokenUrl,
                    ClientId = oauth2.ClientId,
                    ClientSecret = oauth2.ClientSecret,
                    Scope = oauth2.Scope,
                    AuthorizationUrl = oauth2.AuthorizationUrl,
                    RedirectUri = oauth2.RedirectUri,
                    UsePkce = oauth2.UsePkce,
                    CodeChallengeMethod = oauth2.CodeChallengeMethod,
                    Username = oauth2.Username,
                    Password = oauth2.Password,
                    Token = oauth2.Token is null
                        ? null
                        : new OAuth2Token
                        {
                            AccessToken = oauth2.Token.AccessToken,
                            ExpiresAt = oauth2.Token.ExpiresAt,
                            RefreshToken = oauth2.Token.RefreshToken,
                            TokenType = oauth2.Token.TokenType
                        }
                },
                CustomAuthConfig custom => new CustomAuthConfig
                {
                    Url = custom.Url,
                    Method = custom.Method,
                    BodyType = custom.BodyType,
                    Bodies = new Dictionary<BodyType, string>(custom.Bodies),
                    Headers = new Dictionary<string, string>(custom.Headers, StringComparer.OrdinalIgnoreCase),
                    Params = new Dictionary<string, string>(custom.Params, StringComparer.Ordinal),
                    Source = custom.Source,
                    ExtractionExpression = custom.ExtractionExpression,
                    ApplyHeaderName = custom.ApplyHeaderName,
                    ApplyHeaderTemplate = custom.ApplyHeaderTemplate,
                    CachedValue = custom.CachedValue
                },
                _ => null
            };
        }
    }
}
