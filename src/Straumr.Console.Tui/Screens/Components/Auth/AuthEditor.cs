using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Auth;

internal sealed class AuthEditor
{

    private readonly Dictionary<AuthType, StraumrAuthConfig> _configs;

    private readonly KeyValueField _headers;

    private readonly StraumrAuth _state;

    private readonly State<string> _summaryDetail;

    private readonly State<bool> _summaryEmpty;

    private readonly State<string> _summaryType;

    private readonly ResourceEditorView _view;

    private string _opened;

    public AuthEditor(
        StraumrAuth state,
        string? workspaceName,
        bool isNew,
        Action save,
        Action closed,
        Action<ExternalContentEditModel> editContent,
        string? sourceName = null)
    {
        _state = state;
        _opened = Fingerprint(state);
        _summaryType = new State<string>(AuthFormatting.TypeName(state.Config));
        _summaryDetail = new State<string>(Summary(state.Config));
        _summaryEmpty = new State<bool>(SummaryDetail(state.Config).Length == 0);

        _configs = AuthEditingHelpers.AuthTypes.ToDictionary(
            type => type,
            type => state.Config.Type == type ? state.Config : AuthEditingHelpers.CreateConfig(type));

        var bearer = (BearerAuthConfig)_configs[AuthType.Bearer];
        var basic = (BasicAuthConfig)_configs[AuthType.Basic];
        var oauth = (OAuth2Config)_configs[AuthType.OAuth2];
        var custom = (CustomAuthConfig)_configs[AuthType.Custom];

        bool IsType(AuthType type) => state.Config.Type == type;

        var name = new TextField("Name", state.Name, value => state.Name = value,
            "auth name",
            validate: value => value.Length switch
            {
                0 => "A name is required.",
                _ when value.Contains('"') => "A name cannot contain a double quote.",
                _ => null
            });
        name.PendingEcho = TuiKeybindHelpers.OpeningEcho;

        ChoiceField<AuthType> type = new("Type",
            AuthEditingHelpers.AuthTypes.Select(AuthEditingHelpers.AuthTypeDisplayName).ToList(),
            AuthEditingHelpers.AuthTypes,
            state.Config.Type,
            value => state.Config = _configs[value]);

        var autoRenew = new ToggleField("Auto-renew", state.AutoRenewAuth, value => state.AutoRenewAuth = value)
        {
            Visible = () => AuthEditingHelpers.SupportsFetch(state.Config)
        };

        EditorForm authPage = new("Auth",
            name,
            type,
            autoRenew,
            Text("Prefix", bearer.Prefix, value => bearer.Prefix = value, () => IsType(AuthType.Bearer),
                "Bearer"),
            Secret("Token", bearer.Token, value => bearer.Token = value, () => IsType(AuthType.Bearer)),
            Text("Username", basic.Username, value => basic.Username = value, () => IsType(AuthType.Basic)),
            Secret("Password", basic.Password, value => basic.Password = value, () => IsType(AuthType.Basic)),
            new ChoiceField<OAuth2GrantType>("Grant",
                AuthEditingHelpers.OAuth2Grants.Select(AuthEditingHelpers.GrantDisplayName).ToList(),
                AuthEditingHelpers.OAuth2Grants,
                oauth.GrantType,
                value => oauth.GrantType = value)
            {
                Visible = () => IsType(AuthType.OAuth2)
            },
            Url("Token URL", oauth.TokenUrl, value => oauth.TokenUrl = value, () => IsType(AuthType.OAuth2)),
            Text("Client ID", oauth.ClientId, value => oauth.ClientId = value, () => IsType(AuthType.OAuth2)),
            Secret("Client secret", oauth.ClientSecret, value => oauth.ClientSecret = value,
                () => IsType(AuthType.OAuth2)),
            Text("Scope", oauth.Scope, value => oauth.Scope = value, () => IsType(AuthType.OAuth2),
                "users.read users.write"),
            Method(custom, () => IsType(AuthType.Custom)),
            Url("URL", custom.Url, value => custom.Url = value, () => IsType(AuthType.Custom)));

        EditorForm grantPage = new("Grant",
            Url("Authorization URL", oauth.AuthorizationUrl, value => oauth.AuthorizationUrl = value,
                () => IsGrant(OAuth2GrantType.AuthorizationCode)),
            Url("Redirect URI", oauth.RedirectUri, value => oauth.RedirectUri = value,
                () => IsGrant(OAuth2GrantType.AuthorizationCode)),
            new ToggleField("PKCE", oauth.UsePkce, value => oauth.UsePkce = value)
            {
                Visible = () => IsGrant(OAuth2GrantType.AuthorizationCode)
            },
            new ChoiceField<string>("Challenge",
                AuthEditingHelpers.CodeChallengeMethods,
                AuthEditingHelpers.CodeChallengeMethods,
                oauth.CodeChallengeMethod,
                value => oauth.CodeChallengeMethod = value)
            {
                Visible = () => IsGrant(OAuth2GrantType.AuthorizationCode) && oauth.UsePkce
            },
            Text("Username", oauth.Username, value => oauth.Username = value,
                () => IsGrant(OAuth2GrantType.ResourceOwnerPassword)),
            Secret("Password", oauth.Password, value => oauth.Password = value,
                () => IsGrant(OAuth2GrantType.ResourceOwnerPassword)),
            new MessageField("Grant",
                "Client credentials needs nothing beyond the token URL, client and scope on the Auth page.")
            {
                Visible = () => IsGrant(OAuth2GrantType.ClientCredentials)
            })
        {
            Visible = () => IsType(AuthType.OAuth2)
        };

        _headers = new KeyValueField("Headers", "header", custom.Headers);
        var body = new BodyFields(
            () => custom.BodyType,
            value => custom.BodyType = value,
            custom.Bodies,
            custom.Headers,
            () => _headers.Reload(),
            editContent,
            "This auth request sends no body. Choose a type above to give it one.");

        var expression = new TextField("Expression", custom.ExtractionExpression,
            value => custom.ExtractionExpression = value,
            AuthEditingHelpers.ExtractionExpressionHint(custom.Source),
            validate: value => value.Length == 0 ? "An extraction expression is required." : null);

        ChoiceField<ExtractionSource> source = new("Source",
            AuthEditingHelpers.ExtractionSources.Select(AuthEditingHelpers.ExtractionSourceDisplayName).ToList(),
            AuthEditingHelpers.ExtractionSources,
            custom.Source,
            value =>
            {
                custom.Source = value;
                expression.Placeholder = AuthEditingHelpers.ExtractionExpressionHint(value);
            });

        var extractPage = new EditorForm("Extract",
            source,
            expression,
            Text("Apply to", custom.ApplyHeaderName, value => custom.ApplyHeaderName = value,
                () => true, "Authorization",
                value => value.Length == 0 ? "A header name is required." : null),
            Text("Template", custom.ApplyHeaderTemplate, value => custom.ApplyHeaderTemplate = value,
                () => true, "Bearer {{value}}",
                value => value.Contains("{{value}}", StringComparison.Ordinal)
                    ? null
                    : "The template must contain {{value}}, which is where the extracted value goes."))
        {
            Visible = () => IsType(AuthType.Custom)
        };

        extractPage.Root.AddCommand(HelpCommand(
            "Auth.ExtractHelp",
            TuiKeybindHelpers.CombinedLabel("Help", "Auth.ExtractHelp.Function"), CommandPresentation.CommandBar));
        extractPage.Root.AddCommand(HelpCommand(
            "Auth.ExtractHelp.Function",
            "Help", TuiKeybindHelpers.SecondaryPresentation("Auth.ExtractHelp")));

        _view = new ResourceEditorView(
            () => state.Name.Length == 0 ? "new auth" : SecretFormatting.Display(state.Name),
            () => workspaceName,
            BuildSummary(),
            isNew,
            [
                authPage,
                grantPage,
                new EditorForm("Headers", _headers) { Visible = () => IsType(AuthType.Custom) },
                new EditorForm("Params", new KeyValueField("Parameters", "parameter", custom.Params))
                {
                    Visible = () => IsType(AuthType.Custom)
                },
                new EditorForm("Body", body.Fields) { Visible = () => IsType(AuthType.Custom) },
                extractPage
            ],
            save,
            closed,
            () => Fingerprint(_state) != _opened,
            sourceName);

        bool IsGrant(OAuth2GrantType grant) => IsType(AuthType.OAuth2) && oauth.GrantType == grant;
    }

    private static Command HelpCommand(
        string id,
        string label,
        CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Secondary,
        Presentation = presentation,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => new ExtractionHelpDialog().Show()
    };

    public void Show() => _view.Show();

    public void Update()
    {
        _summaryType.Value = AuthFormatting.TypeName(_state.Config);
        _summaryDetail.Value = Summary(_state.Config);
        _summaryEmpty.Value = SummaryDetail(_state.Config).Length == 0;
        _view.Update();
    }

    public void Saved()
    {
        _opened = Fingerprint(_state);
        _view.Saved();
    }

    public void Failed(string message) => _view.Failed(message);

    public void Suspend() => _view.Suspend();

    public void Report(string message, bool error) => _view.Report(message, error);

    private Visual BuildSummary() =>
        new HStack(
                new TextBlock(() => _summaryType.Value)
                    .Style(StraumrStyleService.AccentText)
                    .MinWidth(AuthEditingHelpers.AuthTypes
                        .Max(type => AuthEditingHelpers.AuthTypeDisplayName(type).Length)),
                new TextBlock(() => _summaryDetail.Value)
                    .Style(() => _summaryEmpty.Value
                        ? StraumrStyleService.MutedText
                        : StraumrStyleService.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(2)
            .HorizontalAlignment(Align.Stretch);

    private static string Summary(StraumrAuthConfig config) =>
        SummaryDetail(config) is { Length: > 0 } detail ? detail : SummaryPlaceholder(config);

    private static string SummaryDetail(StraumrAuthConfig config) => config switch
    {
        BearerAuthConfig bearer => bearer.Prefix.Length == 0
            ? string.Empty
            : $"Authorization: {bearer.Prefix}",
        BasicAuthConfig basic => SecretFormatting.Display(basic.Username),
        OAuth2Config oauth => SecretFormatting.Display(oauth.TokenUrl),
        CustomAuthConfig custom => custom.Url.Length == 0
            ? string.Empty
            : $"{custom.Method} {SecretFormatting.Display(custom.Url)}",
        _ => string.Empty
    };

    private static string SummaryPlaceholder(StraumrAuthConfig config) => config switch
    {
        BearerAuthConfig => "no header prefix yet",
        BasicAuthConfig => "no username yet",
        OAuth2Config => "no token URL yet",
        CustomAuthConfig => "no auth URL yet",
        _ => "nothing configured yet"
    };

    private static string Fingerprint(StraumrAuth auth) =>
        $"{auth.Name} {auth.AutoRenewAuth} " +
        JsonSerializer.Serialize(auth.Config, StraumrJsonContext.Default.StraumrAuthConfig);

    private static TextField Text(
        string label,
        string initial,
        Action<string> set,
        Func<bool> visible,
        string? placeholder = null,
        Func<string, string?>? validate = null) =>
        new(label, initial, set, placeholder, validate: validate) { Visible = visible };

    private static TextField Secret(string label, string initial, Action<string> set, Func<bool> visible) =>
        new(label, initial, set, "value or {{secret:name}}", true) { Visible = visible };

    private static TextField Url(string label, string initial, Action<string> set, Func<bool> visible) =>
        new(label, initial, set,
            "https://identity.example.com/oauth/token",
            validate: value => RequestEditingHelpers.IsValidAbsoluteUrl(value)
                ? null
                : "Enter an absolute URL, for example https://identity.example.com/oauth/token.")
        {
            Visible = visible
        };

    private static ChoiceField<string> Method(CustomAuthConfig custom, Func<bool> visible)
    {
        List<string> methods = [.. RequestEditingHelpers.HttpMethods];
        if (custom.Method.Length > 0 && !methods.Contains(custom.Method, StringComparer.Ordinal))
        {
            methods.Add(custom.Method);
        }

        return new ChoiceField<string>("Method", methods, methods, custom.Method,
            value => custom.Method = value)
        {
            Visible = visible
        };
    }
}
