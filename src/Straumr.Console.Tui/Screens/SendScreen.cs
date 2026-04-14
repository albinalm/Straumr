using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Straumr.Console.Shared.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Enums;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Console.Tui.Screens.Prompts;
using Straumr.Core;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Screens;

public sealed class SendScreen : Screen
{
    private static readonly string[] SpinnerFrames =
    [
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
    ];

    private const string HintText =
        "j/k Scroll  g Top  G Bottom  Tab Switch pane  y Copy pane  Y Copy all  b Beautify  B Revert  s Save body  S Export";
    private const string IdleGlyph = "◆";
    private const string DoneGlyph = "✓";
    private const string FailGlyph = "✖";
    private const string CancelGlyph = "◌";

    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrAuthService _authService;
    private readonly ScreenNavigationContext _navigationContext;
    private readonly StraumrTheme _theme;
    private readonly TuiApplicationContext _applicationContext;
    private readonly IInteractiveConsole _interactiveConsole;

    private MarkupLabel? _statusLabel;
    private MarkupLabel? _heroLabel;
    private MarkupLabel? _metaLabel;
    private VirtualTextView? _summaryView;
    private VirtualTextView? _bodyView;
    private FrameView? _summaryFrame;
    private FrameView? _bodyFrame;
    private readonly Dictionary<Border, View> _frameBorderTargets = new();
    private readonly Color _borderFocused;
    private readonly Color _borderUnfocused;
    private readonly Color _borderBackground;
    private Timer? _spinnerTimer;
    private int _spinnerIndex;
    private CancellationTokenSource? _sendTokenSource;
    private bool _sendScheduled;

    private SendStage _stage = SendStage.Idle;
    private string _stageText = "Waiting for request";
    private string? _rawBodyContent;
    private bool _isBodyBeautified;
    private StraumrRequest? _currentRequest;
    private StraumrResponse? _currentResponse;
    private string _currentSummaryText = string.Empty;

    public SendScreen(
        IStraumrRequestService requestService,
        IStraumrAuthService authService,
        ScreenNavigationContext navigationContext,
        StraumrTheme theme,
        TuiApplicationContext applicationContext,
        IInteractiveConsole interactiveConsole)
    {
        _requestService = requestService;
        _authService = authService;
        _navigationContext = navigationContext;
        _theme = theme;
        _applicationContext = applicationContext;
        _interactiveConsole = interactiveConsole;
        _borderFocused = ColorResolver.Resolve(_theme.Accent);
        _borderUnfocused = ColorResolver.Resolve(_theme.Secondary);
        _borderBackground = ColorResolver.Resolve(_theme.Surface);

        Add(new Banner { Theme = _theme });
        Add(new HintsBar { Text = HintText });
        AddView(BuildLayout());
    }

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _sendTokenSource?.Cancel();
        _sendTokenSource?.Dispose();
        _sendTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sendScheduled = true;

        SetStage(SendStage.Preparing, "Preparing request");
        StartSpinner();
        UpdateRequestContext(null, null);
        UpdateHero(null, null);
        UpdateMeta(null);
        UpdateSummary("[secondary]Waiting for request context...[/]");
        UpdateBody(string.Empty);
        return Task.CompletedTask;
    }

    public override bool OnKeyDown(Key key) => HandleKeyBinding(key) || base.OnKeyDown(key);

    private async Task RunSendFlowAsync(CancellationToken cancellationToken)
    {
        UpdateRequestContext(null, null);
        Guid? requestId = _navigationContext.ConsumeRequestId();
        StraumrWorkspaceEntry? workspaceEntry = _navigationContext.GetWorkspaceEntry();

        if (requestId is null)
        {
            SetStage(SendStage.Failed, "No request selected");
            UpdateSummary("[danger]No request details were provided via the navigation context.[/]\n[secondary]Return to the list and try again.[/]");
            return;
        }

        if (workspaceEntry is null)
        {
            SetStage(SendStage.Failed, "No active workspace");
            UpdateSummary("[danger]Unable to resolve an active workspace.[/]\n[secondary]Load a workspace before sending requests.[/]");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetStage(SendStage.Loading, "Loading request");
            StraumrRequest request = await _requestService.GetAsync(requestId.Value.ToString(), workspaceEntry);
            cancellationToken.ThrowIfCancellationRequested();

            UpdateHero(request.Method.Method, request.Uri);

            List<string> notes = [];
            StraumrAuth? auth = null;
            if (request.AuthId.HasValue)
            {
                try
                {
                    auth = await _authService.PeekByIdAsync(request.AuthId.Value, workspaceEntry);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (StraumrException ex)
                {
                    notes.Add($"Unable to load auth configuration: {ex.Message}");
                }
                catch (Exception ex)
                {
                    notes.Add($"Auth configuration error: {ex.Message}");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            SetStage(SendStage.Sending, "Sending request");
            StraumrResponse response = await _requestService.SendAsync(request, new SendOptions(), workspaceEntry);
            cancellationToken.ThrowIfCancellationRequested();

            SetStage(SendStage.Processing, "Processing response");
            UpdateMeta(response);
            UpdateSummary(SendResultFormatter.BuildSummary(request, auth, response, notes));
            string bodyText = string.IsNullOrEmpty(response.Content)
                ? "No content returned from the server."
                : response.Content!;
            UpdateRequestContext(request, response);
            UpdateBody(bodyText, response.Content ?? string.Empty);
            SetStage(SendStage.Completed, "Request completed");
        }
        catch (OperationCanceledException)
        {
            SetStage(SendStage.Canceled, "Send canceled");
        }
        catch (StraumrException ex)
        {
            UpdateSummary(SendResultFormatter.BuildErrorSummary(ex.Message));
            UpdateBody(string.Empty);
            SetStage(SendStage.Failed, "Failed to send request");
        }
        catch (Exception ex)
        {
            UpdateSummary(SendResultFormatter.BuildErrorSummary(ex.Message));
            UpdateBody(string.Empty);
            SetStage(SendStage.Failed, "Failed to send request");
        }
        finally
        {
            StopSpinner();
            _sendTokenSource?.Dispose();
            _sendTokenSource = null;
        }
    }

    private View BuildLayout()
    {
        FrameView frame = new()
        {
            BorderStyle = LineStyle.None,
            X = 2,
            Y = Banner.FigletHeight + 2,
            Width = Dim.Fill(4),
            Height = Dim.Fill(1),
        };

        _statusLabel = new MarkupLabel
        {
            Theme = _theme,
            X = 2,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        _heroLabel = new MarkupLabel
        {
            Theme = _theme,
            X = 2,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        _metaLabel = new MarkupLabel
        {
            Theme = _theme,
            X = 2,
            Y = 2,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        _summaryFrame = new FrameView
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Percent(45),
        };

        _summaryView = new VirtualTextView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(3),
            Height = Dim.Fill(),
        };
        _summaryView.ApplyTheme(
            ColorResolver.Resolve(_theme.OnSurface),
            ColorResolver.Resolve(_theme.Surface));

        ScrollBar summaryScrollBar = BuildSiblingScrollBar(_summaryView);
        _summaryFrame.Add(_summaryView, summaryScrollBar);

        _bodyFrame = new FrameView
        {
            X = 0,
            Y = Pos.Bottom(_summaryFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        _bodyView = new VirtualTextView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(3),
            Height = Dim.Fill(),
        };
        _bodyView.ApplyTheme(
            ColorResolver.Resolve(_theme.OnSurface),
            ColorResolver.Resolve(_theme.Surface));

        ScrollBar bodyScrollBar = BuildSiblingScrollBar(_bodyView);
        _bodyFrame.Add(_bodyView, bodyScrollBar);

        frame.DrawComplete += (_, _) =>
        {
            if (!_sendScheduled || _sendTokenSource is null)
            {
                return;
            }

            _sendScheduled = false;
            _ = RunSendFlowAsync(_sendTokenSource.Token);
        };

        AttachKeyHandler(frame);
        AttachKeyHandler(_summaryFrame);
        AttachKeyHandler(_bodyFrame);
        AttachKeyHandler(_summaryView);
        AttachKeyHandler(_bodyView);
        ApplyFocusAwareBorder(_summaryFrame, _summaryView);
        ApplyFocusAwareBorder(_bodyFrame, _bodyView);

        frame.Add(_statusLabel, _heroLabel, _metaLabel, _summaryFrame, _bodyFrame);
        return frame;
    }

    private static ScrollBar BuildSiblingScrollBar(VirtualTextView textView)
    {
        ScrollBar scrollBar = new()
        {
            Orientation = Orientation.Vertical,
            X = Pos.Right(textView),
            Y = Pos.Top(textView),
            Width = 1,
            Height = Dim.Height(textView),
            VisibilityMode = ScrollBarVisibilityMode.Auto,
        };

        textView.ScrollStateChanged += (_, _) => SyncScrollBar(textView, scrollBar);
        textView.ViewportChanged += (_, _) => SyncScrollBar(textView, scrollBar);

        scrollBar.ValueChanged += (_, args) => textView.SetTopLine(args.NewValue);

        return scrollBar;
    }

    private static void SyncScrollBar(VirtualTextView textView, ScrollBar scrollBar)
    {
        scrollBar.ScrollableContentSize = textView.TotalLines;
        scrollBar.VisibleContentSize = textView.VisibleLines;
        scrollBar.Value = textView.TopLine;
    }

    private void ApplyFocusAwareBorder(FrameView? frame, View? child)
    {
        if (frame?.Border is null || child is null)
        {
            return;
        }

        ConfigureFrameBorder(frame.Border, child);
    }

    private void ConfigureFrameBorder(Border border, View child)
    {
        _frameBorderTargets[border] = child;
        border.GettingAttributeForRole -= BorderOnGettingAttributeForRole;
        border.GettingAttributeForRole += BorderOnGettingAttributeForRole;
    }

    private void BorderOnGettingAttributeForRole(object? sender, VisualRoleEventArgs args)
    {
        if (sender is not Border border)
        {
            return;
        }

        if (!_frameBorderTargets.TryGetValue(border, out View? child))
        {
            return;
        }

        args.Result = new Attribute(child.HasFocus ? _borderFocused : _borderUnfocused, _borderBackground);
        args.Handled = true;
    }

    private void AttachKeyHandler(View? view)
    {
        if (view is null)
        {
            return;
        }

        view.KeyDown += (_, key) =>
        {
            if (HandleKeyBinding(key))
            {
                key.Handled = true;
            }
        };
    }

    private void CopyActivePaneToClipboard()
    {
        VirtualTextView? target = GetActiveTextView() ?? _bodyView ?? _summaryView;
        if (target is null)
        {
            return;
        }

        TryCopyToClipboard(target.Text);
    }

    private void CopyRequestTemplateToClipboard()
    {
        if (_currentRequest is null || _currentResponse is null)
        {
            return;
        }

        string template = SendResultFormatter.BuildRequestTemplate(_currentRequest, _currentResponse, GetBodyForTemplate());
        TryCopyToClipboard(template);
    }

    private string GetBodyForTemplate()
    {
        if (_isBodyBeautified && _bodyView is not null)
        {
            return _bodyView.Text;
        }

        if (_rawBodyContent is not null)
        {
            return _rawBodyContent;
        }

        return _bodyView?.Text ?? string.Empty;
    }

    private void BeautifyBody()
    {
        if (_bodyView is null || _rawBodyContent is null || _isBodyBeautified)
        {
            return;
        }

        string beautified = NormalizeLineEndings(BeautifyContent(_rawBodyContent));
        if (string.Equals(beautified, _rawBodyContent, StringComparison.Ordinal))
        {
            return;
        }

        _bodyView.Text = beautified;
        _isBodyBeautified = true;
    }

    private void RevertBeautifiedBody()
    {
        if (!_isBodyBeautified || _bodyView is null || _rawBodyContent is null)
        {
            return;
        }

        _bodyView.Text = _rawBodyContent;
        _isBodyBeautified = false;
    }

    private bool HandleKeyBinding(Key key)
    {
        if (KeyHelpers.IsEscape(key))
        {
            _sendTokenSource?.Cancel();
            NavigateTo<RequestsScreen>();
            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        int charValue = KeyHelpers.GetCharValue(key);
        switch (charValue)
        {
            case 'j':
                if (ScrollActiveTextView(reverse: false))
                {
                    return true;
                }
                break;
            case 'k':
                if (ScrollActiveTextView(reverse: true))
                {
                    return true;
                }
                break;
            case 'g':
                if (ScrollActiveTextViewToBoundary(toBottom: false))
                {
                    return true;
                }
                break;
            case 'G':
                if (ScrollActiveTextViewToBoundary(toBottom: true))
                {
                    return true;
                }
                break;
        }

        if (KeyHelpers.IsTabNavigation(key))
        {
            SwitchScrollFocus(KeyHelpers.IsTabBackward(key));
            return true;
        }

        switch (charValue)
        {
            case 'y':
                CopyActivePaneToClipboard();
                return true;
            case 'Y':
                CopyRequestTemplateToClipboard();
                return true;
            case 'b':
                BeautifyBody();
                return true;
            case 'B':
                RevertBeautifiedBody();
                return true;
            case 's':
                SaveBodyToFile();
                return true;
            case 'S':
                ExportFullResponse();
                return true;
            default:
                return false;
        }
    }

    private void SaveBodyToFile()
    {
        byte[]? bodyBytes = _currentResponse?.RawContent;
        if (bodyBytes is null || bodyBytes.Length == 0)
        {
            string body = GetBodyForTemplate();
            if (string.IsNullOrWhiteSpace(body))
            {
                ShowMessage("Save response body", "No response body to save.");
                return;
            }

            bodyBytes = Encoding.UTF8.GetBytes(body);
        }

        List<IAllowedType> allowedTypes = BuildBodyAllowedTypes().ToList();
        if (!TryPromptSaveFile("Save response body", BuildSuggestedBodyPath(), allowedTypes, mustExist: false,
                out string? selectedPath))
        {
            return;
        }

        try
        {
            File.WriteAllBytes(selectedPath, bodyBytes);
            ShowMessage("Save response body", $"Saved response body to \"{selectedPath}\".");
        }
        catch (Exception ex)
        {
            ShowMessage("Save response body", $"Failed to save response body: {ex.Message}");
        }
    }

    private void ExportFullResponse()
    {
        if (_currentRequest is null || _currentResponse is null)
        {
            ShowMessage("Export response", "No completed request to export.");
            return;
        }

        List<IAllowedType> allowedTypes =
        [
            new AllowedType("Text files", ".txt"),
            new AllowedTypeAny()
        ];

        if (!TryPromptSaveFile("Export response", BuildSuggestedExportPath(), allowedTypes, mustExist: false,
                out string? selectedPath))
        {
            return;
        }

        try
        {
            string summary = string.IsNullOrWhiteSpace(_currentSummaryText)
                ? "No summary available."
                : _currentSummaryText.TrimEnd();

            string body = GetBodyForTemplate();
            if (string.IsNullOrWhiteSpace(body))
            {
                body = "No body content.";
            }

            StringBuilder builder = new();
            builder.AppendLine("Straumr response export");
            builder.AppendLine("=======================");
            builder.AppendLine($"Request: {_currentRequest.Name}");
            builder.AppendLine($"Method: {_currentRequest.Method.Method.ToUpperInvariant()}");
            builder.AppendLine($"URL: {_currentRequest.Uri}");
            builder.AppendLine($"Status: {SendResultFormatter.FormatStatus(_currentResponse)}");
            builder.AppendLine($"Duration: {Math.Max(0, _currentResponse.Duration.TotalMilliseconds):N0} ms");
            builder.AppendLine();
            builder.AppendLine("Summary");
            builder.AppendLine("-------");
            builder.AppendLine(summary);
            builder.AppendLine();
            builder.AppendLine("Body");
            builder.AppendLine("----");
            builder.AppendLine(body);

            File.WriteAllText(selectedPath, builder.ToString());
            ShowMessage("Export response", $"Exported response to \"{selectedPath}\".");
        }
        catch (Exception ex)
        {
            ShowMessage("Export response", $"Failed to export response: {ex.Message}");
        }
    }

    private string BuildSuggestedBodyPath()
    {
        string baseName = ToSafeFileName(ResolveRequestBaseName("response"), "response");
        string extension = GuessBodyExtension();
        return Path.ChangeExtension(baseName, extension);
    }

    private string BuildSuggestedExportPath()
    {
        string baseName = ToSafeFileName(ResolveRequestBaseName("response-export"), "response-export");
        return Path.ChangeExtension(baseName, ".txt");
    }

    private string ResolveRequestBaseName(string fallback)
    {
        if (_currentRequest is null)
        {
            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(_currentRequest.Name))
        {
            return _currentRequest.Name;
        }

        if (Uri.TryCreate(_currentRequest.Uri, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return _currentRequest.Uri;
    }

    private static string ToSafeFileName(string? candidate, string fallback)
    {
        string baseName = string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(baseName.Length);

        foreach (char c in baseName)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return builder.Length == 0 ? fallback : builder.ToString();
    }

    private IEnumerable<IAllowedType> BuildBodyAllowedTypes()
    {
        List<IAllowedType> allowed = [];

        string extension = GuessBodyExtension();
        if (!string.IsNullOrWhiteSpace(extension))
        {
            string label = $"{extension.Trim('.').ToUpperInvariant()} files";
            allowed.Add(new AllowedType(label, extension));
        }

        allowed.Add(new AllowedTypeAny());
        return allowed;
    }

    private string GuessBodyExtension()
    {
        if (_currentResponse is null)
        {
            return ".txt";
        }

        KeyValuePair<string, IEnumerable<string>> header = _currentResponse.ResponseHeaders
            .FirstOrDefault(pair => pair.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

        string? contentType = header.Value?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return ".txt";
        }

        string mediaType = contentType.Split(';', 2)[0].Trim();
        string? extension = MimeTypes.GetMimeTypeExtensions(mediaType).FirstOrDefault();

        return string.IsNullOrWhiteSpace(extension) ? ".txt" : $".{extension.TrimStart('.')}";
    }

    private bool TryPromptSaveFile(
        string title,
        string? initialPath,
        IReadOnlyList<IAllowedType> allowedTypes,
        bool mustExist,
        [NotNullWhen(true)] out string? selectedPath)
    {
        selectedPath = null;

        FileSavePromptScreen screen = new(title, initialPath, allowedTypes, _theme, mustExist);

        try
        {
            if (!_applicationContext.TryRunPrompt<string?>(screen, out string? result))
            {
                ShowMessage(title, "Application context is not available.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                return false;
            }

            selectedPath = result;
            return true;
        }
        catch (Exception ex)
        {
            ShowMessage(title, $"Unable to show dialog: {ex.Message}");
            return false;
        }
    }

    private void ShowMessage(string title, string message)
    {
        var prompt = new MessagePromptScreen(title, message, _theme);
        if (_applicationContext.TryRunPrompt(prompt, out _))
        {
            return;
        }

        _interactiveConsole.ShowMessage(title, message);
    }

    private void SwitchScrollFocus(bool reverse)
    {
        if (_summaryView is null || _bodyView is null)
        {
            return;
        }

        View? current = _bodyView.HasFocus ? _bodyView : _summaryView.HasFocus ? _summaryView : null;
        if (current is null)
        {
            _summaryView.SetFocus();
            return;
        }

        if (!reverse)
        {
            if (current == _summaryView)
            {
                _bodyView.SetFocus();
            }
            else
            {
                _summaryView.SetFocus();
            }
        }
        else
        {
            if (current == _bodyView)
            {
                _summaryView.SetFocus();
            }
            else
            {
                _bodyView.SetFocus();
            }
        }

        _summaryFrame?.SetNeedsDraw();
        _bodyFrame?.SetNeedsDraw();
    }

    private bool ScrollActiveTextViewToBoundary(bool toBottom)
    {
        VirtualTextView? target = GetActiveTextView();
        if (target is null)
        {
            return false;
        }

        if (toBottom)
        {
            target.ScrollToEnd();
        }
        else
        {
            target.ScrollToStart();
        }

        return true;
    }

    private bool ScrollActiveTextView(bool reverse)
    {
        VirtualTextView? target = GetActiveTextView();
        if (target is null)
        {
            return false;
        }

        target.ScrollLines(reverse ? -1 : 1);
        return true;
    }

    private VirtualTextView? GetActiveTextView()
    {
        if (_bodyView?.HasFocus == true)
        {
            return _bodyView;
        }

        if (_summaryView?.HasFocus == true)
        {
            return _summaryView;
        }

        return null;
    }

    private void UpdateRequestContext(StraumrRequest? request, StraumrResponse? response)
        => InvokeOnUi(() =>
        {
            _currentRequest = request;
            _currentResponse = response;
        });

    private static string BeautifyContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(content);
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                doc.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException) { }

        try
        {
            XDocument doc = XDocument.Parse(content);
            return doc.ToString();
        }
        catch (XmlException) { }

        return content;
    }

    private void TryCopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text) || _bodyView?.App?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            clipboard.TrySetClipboardData(text);
        }
        catch
        {
            // Ignore clipboard errors; UI remains unchanged.
        }
    }

    private void StartSpinner()
    {
        StopSpinner();
        _spinnerIndex = -1;
        _spinnerTimer = new Timer(_ =>
        {
            int frameIndex = Interlocked.Increment(ref _spinnerIndex);
            int normalized = frameIndex % SpinnerFrames.Length;
            if (normalized < 0)
            {
                normalized += SpinnerFrames.Length;
            }

            InvokeOnUi(() => RenderStatus(SpinnerFrames[normalized]));
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(80));
    }

    private void StopSpinner()
    {
        _spinnerTimer?.Dispose();
        _spinnerTimer = null;
        InvokeOnUi(() => RenderStatus(null));
    }

    private void SetStage(SendStage stage, string text)
    {
        _stage = stage;
        _stageText = text;
        InvokeOnUi(() => RenderStatus(null));
    }

    private void RenderStatus(string? spinnerFrame)
    {
        if (_statusLabel is null)
        {
            return;
        }

        string color = _stage switch
        {
            SendStage.Idle => "secondary",
            SendStage.Preparing or SendStage.Loading or SendStage.Sending => "info",
            SendStage.Processing => "accent",
            SendStage.Completed => "success",
            SendStage.Canceled => "warning",
            SendStage.Failed => "danger",
            _ => "secondary",
        };

        string glyph = _stage switch
        {
            SendStage.Completed => DoneGlyph,
            SendStage.Failed => FailGlyph,
            SendStage.Canceled => CancelGlyph,
            SendStage.Idle => IdleGlyph,
            _ => spinnerFrame ?? SpinnerFrames[0],
        };

        _statusLabel.Markup = $"[{color}][bold]{glyph}[/][/]  {_stageText}";
    }

    private void UpdateHero(string? method, string? uri)
        => InvokeOnUi(() =>
        {
            if (_heroLabel is null)
            {
                return;
            }

            if (method is null || uri is null)
            {
                _heroLabel.Markup = string.Empty;
                return;
            }

            string upper = method.ToUpperInvariant();
            string methodTag = HttpMethodMarkup.TagFor(method);
            _heroLabel.Markup = $"[{methodTag}][bold]{upper,-6}[/][/] [surface]{uri}[/]";
        });

    private void UpdateMeta(StraumrResponse? response)
        => InvokeOnUi(() =>
        {
            if (_metaLabel is null)
            {
                return;
            }

            if (response is null)
            {
                _metaLabel.Markup = string.Empty;
                return;
            }

            string statusText = SendResultFormatter.FormatStatus(response);
            string statusColor = response.StatusCode is { } code
                ? ((int)code) switch
                {
                    >= 200 and < 300 => "success",
                    >= 300 and < 400 => "accent",
                    >= 400 and < 500 => "warning",
                    >= 500 => "danger",
                    _ => "info",
                }
                : "danger";

            string duration = $"{Math.Max(0, response.Duration.TotalMilliseconds):N0} ms";
            string http = response.HttpVersion?.ToString() is { Length: > 0 } v ? $"HTTP/{v}" : "HTTP/?";

            _metaLabel.Markup =
                $"[{statusColor}][bold]{statusText}[/][/]  [secondary]·[/]  [info]{duration}[/]  [secondary]·[/]  [secondary]{http}[/]";
        });

    private void UpdateSummary(string text)
        => InvokeOnUi(() =>
        {
            string plain = NormalizeLineEndings(MarkupText.ToPlain(text));
            _currentSummaryText = plain;
            _summaryView?.Text = plain;
        });

    private void UpdateBody(string text, string? responseBody = null)
        => InvokeOnUi(() =>
        {
            _bodyView?.Text = NormalizeLineEndings(text);

            _rawBodyContent = responseBody is null ? null : NormalizeLineEndings(responseBody);
            _isBodyBeautified = false;
        });

    private static string NormalizeLineEndings(string value)
        => string.IsNullOrEmpty(value) ? value : value.Replace("\r\n", "\n").Replace("\r", "\n");

    private void InvokeOnUi(Action action)
    {
        try
        {
            if (_applicationContext.Application is { } app)
            {
                app.Invoke(action);
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // Fallback to direct invocation below.
        }

        action();
    }
}
