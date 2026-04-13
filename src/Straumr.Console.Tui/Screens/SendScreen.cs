using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens;

public sealed class SendScreen : Screen
{
    private static readonly string[] SpinnerFrames =
    [
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
    ];

    private const string IdleGlyph = "◆";
    private const string DoneGlyph = "✔";
    private const string FailGlyph = "✖";
    private const string CancelGlyph = "◌";

    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrAuthService _authService;
    private readonly ScreenNavigationContext _navigationContext;
    private readonly StraumrTheme _theme;

    private MarkupLabel? _statusLabel;
    private MarkupLabel? _heroLabel;
    private MarkupLabel? _metaLabel;
    private InteractiveTextView? _summaryView;
    private InteractiveTextView? _bodyView;
    private Timer? _spinnerTimer;
    private int _spinnerIndex;
    private CancellationTokenSource? _sendTokenSource;
    private bool _sendScheduled;

    private SendStage _stage = SendStage.Idle;
    private string _stageText = "Waiting for request";

    public SendScreen(
        IStraumrRequestService requestService,
        IStraumrAuthService authService,
        ScreenNavigationContext navigationContext,
        StraumrTheme theme)
    {
        _requestService = requestService;
        _authService = authService;
        _navigationContext = navigationContext;
        _theme = theme;

        Add(new Banner { Theme = _theme });
        Add(new HintsBar { Text = "esc Back to requests" });
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
        UpdateHero(null, null);
        UpdateMeta(null);
        UpdateSummary("[secondary]Waiting for request context...[/]");
        UpdateBody(string.Empty);
        return Task.CompletedTask;
    }

    public override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc)
        {
            _sendTokenSource?.Cancel();
            NavigateTo<RequestsScreen>();
            return true;
        }

        return base.OnKeyDown(key);
    }

    private async Task RunSendFlowAsync(CancellationToken cancellationToken)
    {
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
            UpdateSummary(BuildSummary(request, auth, response, notes));
            UpdateBody(string.IsNullOrEmpty(response.Content)
                ? "No content returned from the server."
                : response.Content);
            SetStage(SendStage.Completed, "Request completed");
        }
        catch (OperationCanceledException)
        {
            SetStage(SendStage.Canceled, "Send canceled");
        }
        catch (StraumrException ex)
        {
            UpdateSummary(BuildErrorSummary(ex.Message));
            UpdateBody(string.Empty);
            SetStage(SendStage.Failed, "Failed to send request");
        }
        catch (Exception ex)
        {
            UpdateSummary(BuildErrorSummary(ex.Message));
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
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        _heroLabel = new MarkupLabel
        {
            Theme = _theme,
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        _metaLabel = new MarkupLabel
        {
            Theme = _theme,
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 1,
            Markup = string.Empty,
        };

        FrameView summaryFrame = new()
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
        summaryFrame.Add(_summaryView, summaryScrollBar);

        FrameView bodyFrame = new()
        {
            X = 0,
            Y = Pos.Bottom(summaryFrame),
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
        bodyFrame.Add(_bodyView, bodyScrollBar);

        frame.DrawComplete += (_, _) =>
        {
            if (!_sendScheduled || _sendTokenSource is null)
            {
                return;
            }

            _sendScheduled = false;
            _ = RunSendFlowAsync(_sendTokenSource.Token);
        };

        frame.Add(_statusLabel, _heroLabel, _metaLabel, summaryFrame, bodyFrame);
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
        };

        void Sync()
        {
            scrollBar.ScrollableContentSize = textView.GetContentSize().Height;
            scrollBar.VisibleContentSize = textView.Viewport.Height;
            scrollBar.Value = textView.Viewport.Y;
        }

        textView.ContentSizeChanged += (_, _) => Sync();
        textView.ViewportChanged += (_, _) => Sync();

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

    private string BuildSummary(
        StraumrRequest request,
        StraumrAuth? auth,
        StraumrResponse response,
        IReadOnlyCollection<string> notes)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine($"  ▸ Method    {request.Method.Method.ToUpperInvariant()}");
        builder.AppendLine($"  ▸ URL       {request.Uri}");
        builder.AppendLine($"  ▸ Auth      {auth?.Name ?? "None"}");
        builder.AppendLine($"  ▸ Status    {FormatStatus(response)}");
        if (!string.IsNullOrEmpty(response.ReasonPhrase))
        {
            builder.AppendLine($"  ▸ Reason    {response.ReasonPhrase}");
        }

        builder.AppendLine($"  ▸ Duration  {Math.Max(0, response.Duration.TotalMilliseconds):N0} ms");
        if (response.HttpVersion is not null)
        {
            builder.AppendLine($"  ▸ HTTP      {response.HttpVersion}");
        }

        if (notes.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Notes");
            foreach (string note in notes)
            {
                builder.AppendLine($"  • {note}");
            }
        }

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Warnings");
            foreach (string warning in response.Warnings)
            {
                builder.AppendLine($"  • {warning}");
            }
        }

        if (response.Exception is not null)
        {
            builder.AppendLine();
            AppendSection(builder, "Response exception");
            builder.AppendLine($"  {response.Exception.Message}");
        }

        if (response.RequestHeaders.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Request headers");
            AppendHeaderLines(builder, response.RequestHeaders);
        }

        if (response.ResponseHeaders.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Response headers");
            AppendHeaderLines(builder, response.ResponseHeaders);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildErrorSummary(string message)
    {
        var builder = new StringBuilder();
        AppendSection(builder, "Error");
        builder.AppendLine("  An error occurred while sending the request.");
        builder.AppendLine();
        builder.AppendLine($"  {message}");
        builder.AppendLine();
        builder.Append("  Press Esc to return and try again.");
        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string title)
    {
        const int underlineWidth = 48;
        builder.AppendLine($"── {title} " + new string('─', Math.Max(1, underlineWidth - title.Length - 4)));
    }

    private static void AppendHeaderLines(
        StringBuilder builder,
        IReadOnlyDictionary<string, IEnumerable<string>> headers)
    {
        foreach ((string key, IEnumerable<string> value) in headers)
        {
            string joined = string.Join(", ", value);
            builder.AppendLine($"  {key}: {joined}");
        }
    }

    private static string FormatStatus(StraumrResponse response)
    {
        if (response.StatusCode is { } statusCode)
        {
            string statusName = Enum.IsDefined(typeof(HttpStatusCode), statusCode)
                ? statusCode.ToString()
                : "Unknown";
            return $"{(int)statusCode} {statusName}";
        }

        return response.Exception is not null ? "Error" : "No response";
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

        _statusLabel.Markup = $"[{color}][bold]{glyph}[/]  {_stageText}[/]";
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
            string methodColor = upper switch
            {
                "GET" => "info",
                "POST" => "success",
                "PUT" => "warning",
                "PATCH" => "accent",
                "DELETE" => "danger",
                "HEAD" or "OPTIONS" => "secondary",
                _ => "primary",
            };

            _heroLabel.Markup = $"[{methodColor}][bold]{upper,-6}[/][/] [surface]{uri}[/]";
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

            string statusText = FormatStatus(response);
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
            if (_summaryView is not null)
            {
                _summaryView.Text = Straumr.Console.Tui.Helpers.MarkupText.ToPlain(text);
            }
        });

    private void UpdateBody(string text)
        => InvokeOnUi(() =>
        {
            if (_bodyView is not null)
            {
                _bodyView.Text = text;
            }
        });

    private static void InvokeOnUi(Action action)
    {
        try
        {
            Application.Invoke(action);
        }
        catch (InvalidOperationException)
        {
            action();
        }
    }

    private enum SendStage
    {
        Idle,
        Preparing,
        Loading,
        Sending,
        Processing,
        Completed,
        Canceled,
        Failed,
    }
}
