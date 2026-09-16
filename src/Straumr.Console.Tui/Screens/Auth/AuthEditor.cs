using System.Text.Json;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Auth;

/// <summary>
/// What an auth looks like in the shared editor: the pages, the fields on them, and the rules about
/// which of them apply. Everything else — the screen it opens on, the bar, the save gesture,
/// validation display — belongs to the kit and is not repeated here.
/// </summary>
/// <remarks>
/// This is the resource the kit was designed against: four configuration shapes, eleven OAuth2
/// fields, and a whole request inside Custom. The type is a discriminator that decides whole pages
/// and not merely rows on them — a bearer token has no grant flow and no request of its own — which
/// is why <see cref="PagedPane"/> takes a predicate per page as well as per field.
/// </remarks>
internal sealed class AuthEditor
{
    /// <summary>The key that opens the Extract page's help, where a field has not already taken it.</summary>
    private const char HelpLetter = 'h';

    private readonly StraumrAuth _state;

    /// <summary>
    /// Every configuration shape the editor is holding, one per type, so that changing the type puts
    /// away what the old one had rather than throwing it out. Each field writes into its own instance
    /// for the life of the form; the type only decides which of them <see cref="StraumrAuth.Config"/>
    /// currently points at. It is the rule a body type already follows one level down.
    /// </summary>
    private readonly Dictionary<AuthType, StraumrAuthConfig> _configs;

    private readonly ResourceEditorView _view;

    /// <summary>Held because choosing a body type writes into the headers behind this field's back.</summary>
    private readonly KeyValueField _headers;

    /// <summary>
    /// What the auth was when it was opened, as text. It is replaced by every save, because the view
    /// stays open and what was just written is what the next edit has to differ from.
    /// </summary>
    /// <remarks>
    /// An auth is compared as its serialized form rather than field by field: the four shapes have
    /// nothing in common to compare, and the one form that covers all of them is the one Core
    /// persists. The identity and the timestamps are left out, so a copy still reads as unedited.
    /// </remarks>
    private string _opened;

    /// <summary>
    /// The bar's two halves, mirrored out of the state once per update pass. What they read is the
    /// resource being edited, which is a plain object the binding graph knows nothing about, so a
    /// bar bound straight to it would never change as the fields did. See
    /// <see cref="Request.RequestEditor"/>, which found it first.
    /// </summary>
    private readonly State<string> _summaryType;

    private readonly State<string> _summaryDetail;

    /// <summary>Whether <see cref="_summaryDetail"/> holds a real value or the placeholder for one.</summary>
    private readonly State<bool> _summaryEmpty;

    /// <param name="openingGesture">
    /// The key that opened the editor, discarded by the field that takes initial focus. A printable
    /// gesture arrives as a key event and an independent text event, so <c>c</c> would otherwise type
    /// itself into the name.
    /// </param>
    /// <param name="editContent">
    /// Where a custom auth's body goes to be written. The screen owns it because running another
    /// program means putting the terminal down, which a form cannot do from inside a keystroke.
    /// </param>
    public AuthEditor(
        StraumrAuth state,
        string? workspaceName,
        bool isNew,
        char? openingGesture,
        Action save,
        Action closed,
        Action<ExternalContentEdit> editContent,
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
        bool IsGrant(OAuth2GrantType grant) => IsType(AuthType.OAuth2) && oauth.GrantType == grant;

        var name = new TextField("Name", state.Name, value => state.Name = value,
            placeholder: "auth name",
            validate: value => value.Length switch
            {
                0 => "A name is required.",
                // Core refuses it on save; saying so here costs a keystroke rather than a round trip.
                _ when value.Contains('"') => "A name cannot contain a double quote.",
                _ => null
            });
        name.PendingEcho = openingGesture;

        var type = new ChoiceField<AuthType>("Type",
            AuthEditingHelpers.AuthTypes.Select(AuthEditingHelpers.AuthTypeDisplayName).ToList(),
            AuthEditingHelpers.AuthTypes,
            state.Config.Type,
            value => state.Config = _configs[value]);

        // Only a type that goes and gets a value has anything to renew. On a bearer token or a pair
        // of basic credentials the switch would be a control with nothing behind it.
        var autoRenew = new ToggleField("Auto-renew", state.AutoRenewAuth, value => state.AutoRenewAuth = value)
        {
            Visible = () => AuthEditingHelpers.SupportsFetch(state.Config)
        };

        EditorForm authPage = new("Auth",
            name,
            type,
            autoRenew,
            Text("Prefix", bearer.Prefix, value => bearer.Prefix = value, () => IsType(AuthType.Bearer),
                placeholder: "Bearer"),
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
                placeholder: "users.read users.write"),
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
            placeholder: AuthEditingHelpers.ExtractionExpressionHint(custom.Source),
            validate: value => value.Length == 0 ? "An extraction expression is required." : null);

        // The expression means something different in each of the three sources and nothing else on
        // the page says which, so the placeholder follows the source rather than being written once.
        var source = new ChoiceField<ExtractionSource>("Source",
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
                () => true, placeholder: "Authorization",
                validate: value => value.Length == 0 ? "A header name is required." : null),
            Text("Template", custom.ApplyHeaderTemplate, value => custom.ApplyHeaderTemplate = value,
                () => true, placeholder: "Bearer {{value}}",
                validate: value => value.Contains("{{value}}", StringComparison.Ordinal)
                    ? null
                    : "The template must contain {{value}}, which is where the extracted value goes."))
        {
            Visible = () => IsType(AuthType.Custom)
        };

        // The help belongs to this page and is registered on it, so it is offered while the reader
        // is on Extract and nowhere else: commands are collected from the focus chain, and every
        // page stays attached whether or not it is the one on show.
        extractPage.Root.AddCommand(HelpCommand(
            "Auth.ExtractHelp", new KeyGesture(HelpLetter),
            $"{StraumrStyles.KeyMarkup("/F1")} Help", CommandPresentation.CommandBar));
        extractPage.Root.AddCommand(HelpCommand(
            "Auth.ExtractHelp.Function", new KeyGesture(TerminalKey.F1),
            "Help", CommandPresentation.None));

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
    }

    /// <summary>
    /// One action under two keys, because neither key alone reaches the whole page.
    /// </summary>
    /// <remarks>
    /// Every letter is a character a focused field swallows — the rule the editor's own save gesture
    /// is built around — so <c>h</c> opens the help from the Source dropdown and never from the three
    /// text boxes, which is exactly where a reader wondering what to type is standing. <c>F1</c> is
    /// not a character and works on all four fields. <c>Ctrl+H</c>, the obvious pairing, is unusable:
    /// a terminal sends it as the C0 byte for Backspace, so it would delete a character rather than
    /// explain one. The bar renders one keycap per hint, from the gesture, so the second key rides in
    /// the label in the bar's own key colour and its command goes unpresented — the shape
    /// <c>Enter /e Edit</c> already has on the Requests list.
    /// </remarks>
    private static Command HelpCommand(
        string id,
        KeyGesture gesture,
        string label,
        CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = gesture,
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

    /// <summary>
    /// The bar's left half, live: the type and the one thing that says which service this auth is
    /// for. It is the pair worth checking before saving, as the method and URL are on a request.
    /// </summary>
    private Visual BuildSummary() =>
        new HStack(
                new TextBlock(() => _summaryType.Value)
                    .Style(StraumrStyles.AccentText)
                    .MinWidth(AuthEditingHelpers.AuthTypes
                        .Max(type => AuthEditingHelpers.AuthTypeDisplayName(type).Length)),
                new TextBlock(() => _summaryDetail.Value)
                    .Style(() => _summaryEmpty.Value
                        ? StraumrStyles.MutedText
                        : StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(2)
            .HorizontalAlignment(Align.Stretch);

    /// <summary>
    /// What the bar says about the auth: what it points at, or what it is waiting to be given.
    /// </summary>
    private static string Summary(StraumrAuthConfig config) =>
        SummaryDetail(config) is { Length: > 0 } detail ? detail : SummaryPlaceholder(config);

    /// <summary>What the auth points at, which is a different field in each of the four shapes.</summary>
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

    /// <remarks>
    /// The identity and the timestamps are left out. A copy is a different auth with the same
    /// configuration, and comparing what Core would write would call one edited before it was.
    /// </remarks>
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

    /// <remarks>
    /// Masked, and revealed while the field itself has focus: a credential is hidden from someone
    /// reading over a shoulder and readable to whoever is typing it. A secret reference typed here
    /// is masked too, which is right — it is the value this field holds either way.
    /// </remarks>
    private static TextField Secret(string label, string initial, Action<string> set, Func<bool> visible) =>
        new(label, initial, set, placeholder: "value or {{secret:name}}", secret: true) { Visible = visible };

    private static TextField Url(string label, string initial, Action<string> set, Func<bool> visible) =>
        new(label, initial, set,
            placeholder: "https://identity.example.com/oauth/token",
            validate: value => RequestEditingHelpers.IsValidAbsoluteUrl(value)
                ? null
                : "Enter an absolute URL, for example https://identity.example.com/oauth/token.")
        {
            Visible = visible
        };

    /// <remarks>
    /// A method the list does not offer is still the one this auth request uses, so it joins the list
    /// rather than being quietly replaced by the first entry the moment the editor opens.
    /// </remarks>
    private static ChoiceField<string> Method(CustomAuthConfig custom, Func<bool> visible)
    {
        List<string> methods = [.. RequestEditingHelpers.HttpMethods];
        if (custom.Method.Length > 0 && !methods.Contains(custom.Method, StringComparer.Ordinal))
            methods.Add(custom.Method);

        return new ChoiceField<string>("Method", methods, methods, custom.Method,
            value => custom.Method = value)
        {
            Visible = visible
        };
    }
}
