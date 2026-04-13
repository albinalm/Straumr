using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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
        "j/k Scroll  g Top  G Bottom  Tab Switch pane  y Copy pane  Y Copy all  b Beautify  B Revert";
    private const string IdleGlyph = "◆";
    private const string DoneGlyph = "✓";
    private const string FailGlyph = "✖";
    private const string CancelGlyph = "◌";

    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrAuthService _authService;
    private readonly ScreenNavigationContext _navigationContext;
    private readonly StraumrTheme _theme;
    private readonly TuiApplicationContext _applicationContext;

    private MarkupLabel? _statusLabel;
    private MarkupLabel? _heroLabel;
    private MarkupLabel? _metaLabel;
    private InteractiveTextView? _summaryView;
    private InteractiveTextView? _bodyView;
    private FrameView? _summaryFrame;
    private FrameView? _bodyFrame;
    private readonly Dictionary<Border, View> _frameBorderTargets = new();
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

    public SendScreen(
        IStraumrRequestService requestService,
        IStraumrAuthService authService,
        ScreenNavigationContext navigationContext,
        StraumrTheme theme,
        TuiApplicationContext applicationContext)
    {
        _requestService = requestService;
        _authService = authService;
        _navigationContext = navigationContext;
        _theme = theme;
        _applicationContext = applicationContext;

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

        _summaryView = new InteractiveTextView
        {
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true,
            X = 1,
            Y = 0,
            Width = Dim.Fill(3),
            Height = Dim.Fill(),
            Text = string.Empty,
        };
        _summaryView.ApplyTheme(
            ColorResolver.Resolve(_theme.Surface),
            ColorResolver.Resolve(_theme.OnSurface));

        ScrollBar summaryScrollBar = BuildSiblingScrollBar(_summaryView);
        _summaryFrame.Add(_summaryView, summaryScrollBar);

        _bodyFrame = new FrameView
        {
            X = 0,
            Y = Pos.Bottom(_summaryFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        _bodyView = new InteractiveTextView
        {
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true,
            X = 1,
            Y = 0,
            Width = Dim.Fill(3),
            Height = Dim.Fill(),
            Text = string.Empty,
        };
        _bodyView.ApplyTheme(
            ColorResolver.Resolve(_theme.Surface),
            ColorResolver.Resolve(_theme.OnSurface));

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

    private static ScrollBar BuildSiblingScrollBar(TextView textView)
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

        textView.ContentSizeChanged += (_, _) => SyncScrollBar(textView, scrollBar);
        textView.ViewportChanged += (_, _) => SyncScrollBar(textView, scrollBar);

        scrollBar.ValueChanged += (_, args) =>
        {
            int delta = args.NewValue - textView.Viewport.Y;
            if (delta != 0)
            {
                textView.ScrollVertical(delta);
            }
        };

        return scrollBar;
    }

    private static void SyncScrollBar(TextView textView, ScrollBar scrollBar)
    {
        scrollBar.ScrollableContentSize = textView.GetContentSize().Height;
        scrollBar.VisibleContentSize = textView.Viewport.Height;
        scrollBar.Value = textView.Viewport.Y;
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

        Color foreground = ColorResolver.Resolve(child.HasFocus ? _theme.Accent : _theme.Secondary);
        Color background = ColorResolver.Resolve(_theme.Surface);
        args.Result = new Attribute(foreground, background);
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
        TextView? target = GetActiveTextView() ?? _bodyView ?? _summaryView;
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

        string beautified = BeautifyContent(_rawBodyContent);
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
        if (key == Key.Esc)
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

        if (key == Key.Tab || key == Key.Tab.WithShift)
        {
            SwitchScrollFocus(key == Key.Tab.WithShift);
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
            default:
                return false;
        }
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
        TextView? target = GetActiveTextView();
        if (target is null)
        {
            return false;
        }

        int contentHeight = target.GetContentSize().Height;
        int viewportHeight = target.Viewport.Height;
        int desired = toBottom
            ? Math.Max(0, contentHeight - viewportHeight)
            : 0;
        int delta = desired - target.Viewport.Y;
        if (delta != 0)
        {
            target.ScrollVertical(delta);
        }

        return true;
    }

    private bool ScrollActiveTextView(bool reverse)
    {
        TextView? target = GetActiveTextView();
        if (target is null)
        {
            return false;
        }

        int delta = reverse ? -1 : 1;
        target.ScrollVertical(delta);
        return true;
    }

    private TextView? GetActiveTextView()
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
            _summaryView?.Text = MarkupText.ToPlain(text);
        });

    private void UpdateBody(string text, string? responseBody = null)
        => InvokeOnUi(() =>
        {
            _bodyView?.Text = text;

            _rawBodyContent = responseBody;
            _isBodyBeautified = false;
        });

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
