using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Services;

internal sealed class ResponseBodyActionService
{
    private readonly bool _bounded;
    private readonly Action<string, bool> _notify;
    private readonly Func<ResponseBodyOptionsModel> _options;
    private readonly PreviewPane _preview;
    private string? _body;
    private bool _highlighted;
    private bool _oversized;
    private bool _pretty;

    public ResponseBodyActionService(PreviewPane preview, Action<string, bool> notify,
        Func<ResponseBodyOptionsModel> options, bool bounded = true)
    {
        (_preview, _notify, _options) = (preview, notify, options);
        _bounded = bounded;
        AddCommand("Format", "Beautify / minify", ToggleFormat);
        AddCommand("Highlight", "Highlight", ToggleHighlight);
        AddCommand("Copy", "Copy body", Copy);
    }

    public void SetBody(string? body)
    {
        ResponseBodyOptionsModel options = _options();
        _body = body;
        _pretty = false;
        if (options.Format is not ResponseBodyFormat.None
            && RequestEditingHelpers.TryFormatJson(body, options.Format is ResponseBodyFormat.Beautify,
                out string? formatted))
        {
            _body = formatted;
            _pretty = options.Format is ResponseBodyFormat.Beautify;
        }

        UpdatePreview();
        Highlight(options.Highlight && !_oversized);
    }

    private void UpdatePreview()
    {
        string drawn = Drawn();
        _oversized = drawn.Length > _options().HighlightLimit;
        _preview.SetPageText(0, drawn);
    }

    private string Drawn() =>
        _bounded ? ContentFormatting.Preview(_body) : string.IsNullOrEmpty(_body) ? "No body." : _body;

    private void Highlight(bool on)
    {
        _highlighted = on && RequestEditingHelpers.IsJson(_body);
        _preview.SetPageHighlighter(0, _highlighted ? Line : null);
    }

    private static StyledRun[] Line(string line) =>
        line == ContentFormatting.Truncated
            ? [new StyledRun(0, line.Length, StraumrStyleService.CodeNote)]
            : JsonHighlighting.Line(line);

    private void AddCommand(string id, string label, Action execute) =>
        _preview.Page(0).AddCommand(new Command
        {
            Id = $"ResponseBody.{id}",
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get($"ResponseBody.{id}"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !string.IsNullOrEmpty(_body),
            IsVisible = _ => !string.IsNullOrEmpty(_body),
            Execute = _ => execute()
        });

    private void ToggleFormat()
    {
        if (!RequestEditingHelpers.TryFormatJson(_body, !_pretty, out string? formatted))
        {
            _notify("This body is not valid JSON; formatting is unavailable.", true);
            return;
        }

        _body = formatted;
        _pretty = !_pretty;
        UpdatePreview();
        _notify(_pretty ? "JSON beautified" : "JSON minified", false);
    }

    private void ToggleHighlight()
    {
        bool wanted = !_highlighted;
        Highlight(wanted);
        if (wanted && !_highlighted)
        {
            _notify("This body is not valid JSON; highlighting is unavailable.", true);
            return;
        }

        _notify(_highlighted
            ? _oversized
                ? $"Highlighting on — {ContentFormatting.Size(Drawn().Length)} of text may scroll slowly"
                : "Highlighting on"
            : "Highlighting off", false);
    }

    private void Copy()
    {
        try
        {
            bool copied = _preview.Root.App?.Terminal.Clipboard.TrySetText(_body) == true;
            _notify(copied ? "Copied full response body" : "Clipboard is unavailable in this terminal.", !copied);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _notify($"Cannot copy body: {exception.Message}", true);
        }
    }
}
