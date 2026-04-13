using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
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
    private static readonly string[] SpinnerFrames = ["|", "/", "-", "\\"];

    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrAuthService _authService;
    private readonly ScreenNavigationContext _navigationContext;
    private readonly StraumrTheme _theme;

    private Label? _statusLabel;
    private Label? _spinnerLabel;
    private TextView? _summaryView;
    private TextView? _bodyView;
    private Timer? _spinnerTimer;
    private int _spinnerIndex;
    private CancellationTokenSource? _sendTokenSource;
    private bool _sendScheduled;

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

        UpdateStatus("Preparing request...");
        UpdateSummary("Waiting for request context...");
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
            UpdateStatus("No request selected.");
            UpdateSummary("No request details were provided via the navigation context. Return to the list and try again.");
            return;
        }

        if (workspaceEntry is null)
        {
            UpdateStatus("No active workspace.");
            UpdateSummary("Unable to resolve an active workspace. Load a workspace before sending requests.");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartSpinner();

            UpdateStatus("Loading request...");
            StraumrRequest request = await _requestService.GetAsync(requestId.Value.ToString(), workspaceEntry);
            cancellationToken.ThrowIfCancellationRequested();

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

            UpdateStatus("Sending request...");
            StraumrResponse response = await _requestService.SendAsync(request, new SendOptions(), workspaceEntry);
            cancellationToken.ThrowIfCancellationRequested();

            UpdateStatus("Processing response...");
            UpdateSummary(BuildSummary(request, auth, response, notes));
            UpdateBody(string.IsNullOrEmpty(response.Content)
                ? "No content returned from the server."
                : response.Content);
            UpdateStatus("Request completed.");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Send canceled.");
        }
        catch (StraumrException ex)
        {
            UpdateSummary(BuildErrorSummary(ex.Message));
            UpdateBody(string.Empty);
            UpdateStatus("Failed to send request.");
        }
        catch (Exception ex)
        {
            UpdateSummary(BuildErrorSummary(ex.Message));
            UpdateBody(string.Empty);
            UpdateStatus("Failed to send request.");
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
            Title = "Send Request",
            X = 2,
            Y = Banner.FigletHeight + 5,
            Width = Dim.Fill(4),
            Height = Dim.Fill(2),
        };

        _spinnerLabel = new Label
        {
            X = 2,
            Y = 1,
            Width = 2,
            Text = string.Empty,
        };

        _statusLabel = new Label
        {
            X = Pos.Right(_spinnerLabel) + 1,
            Y = _spinnerLabel.Y,
            Width = Dim.Fill(2),
            Text = "Waiting for request...",
        };

        FrameView summaryFrame = new()
        {
            Title = "Request details",
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Percent(45),
        };

        _summaryView = new TextView
        {
            ReadOnly = true,
            Enabled = false,
            WordWrap = true,
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(),
            Text = string.Empty,
        };
        summaryFrame.Add(_summaryView);

        FrameView bodyFrame = new()
        {
            Title = "Response body",
            X = 1,
            Y = Pos.Bottom(summaryFrame) + 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        _bodyView = new TextView
        {
            ReadOnly = true,
            Enabled = false,
            WordWrap = true,
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(),
            Text = string.Empty,
        };
        bodyFrame.Add(_bodyView);

        frame.DrawComplete += (_, _) =>
        {
            if (!_sendScheduled || _sendTokenSource is null)
            {
                return;
            }

            _sendScheduled = false;
            _ = RunSendFlowAsync(_sendTokenSource.Token);
        };

        frame.Add(_spinnerLabel, _statusLabel, summaryFrame, bodyFrame);
        return frame;
    }

    private string BuildSummary(
        StraumrRequest request,
        StraumrAuth? auth,
        StraumrResponse response,
        IReadOnlyCollection<string> notes)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Request: {request.Method.Method.ToUpperInvariant()} {request.Uri}");
        builder.AppendLine($"Auth: {auth?.Name ?? "None"}");
        builder.AppendLine($"Status: {FormatStatus(response)}");
        if (!string.IsNullOrEmpty(response.ReasonPhrase))
        {
            builder.AppendLine($"Reason: {response.ReasonPhrase}");
        }

        builder.AppendLine($"Duration: {Math.Max(0, response.Duration.TotalMilliseconds):N0} ms");
        if (response.HttpVersion is not null)
        {
            builder.AppendLine($"HTTP Version: {response.HttpVersion}");
        }

        if (notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Notes:");
            foreach (string note in notes)
            {
                builder.AppendLine($"  • {note}");
            }
        }

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (string warning in response.Warnings)
            {
                builder.AppendLine($"  • {warning}");
            }
        }

        if (response.Exception is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Response exception:");
            builder.AppendLine(response.Exception.Message);
        }

        if (response.RequestHeaders.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Request headers:");
            AppendHeaderLines(builder, response.RequestHeaders);
        }

        if (response.ResponseHeaders.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Response headers:");
            AppendHeaderLines(builder, response.ResponseHeaders);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildErrorSummary(string message)
    {
        var builder = new StringBuilder();
        builder.AppendLine("An error occurred while sending the request.");
        builder.AppendLine();
        builder.AppendLine(message);
        builder.AppendLine();
        builder.Append("Press Esc to return and try again.");
        return builder.ToString();
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

            string frame = SpinnerFrames[normalized];
            InvokeOnUi(() =>
            {
                if (_spinnerLabel is not null)
                {
                    _spinnerLabel.Text = frame;
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(120));
    }

    private void StopSpinner()
    {
        _spinnerTimer?.Dispose();
        _spinnerTimer = null;
        InvokeOnUi(() =>
        {
            if (_spinnerLabel is not null)
            {
                _spinnerLabel.Text = string.Empty;
            }
        });
    }

    private void UpdateStatus(string text)
        => InvokeOnUi(() =>
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = text;
            }
        });

    private void UpdateSummary(string text)
        => InvokeOnUi(() =>
        {
            if (_summaryView is not null)
            {
                _summaryView.Text = text;
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
}
