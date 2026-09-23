using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Services;

internal sealed class BodyPreviewService
{
    private readonly bool _bounded;
    private readonly Action<string, bool> _notify;
    private readonly Func<ResponseBodyOptionsModel> _options;
    private readonly PreviewPane _preview;
    private string? _body;
    private Func<string, StyledRun[]>? _highlighter;
    private ContentLanguage _language;
    private bool _oversized;
    private bool _pretty;

    public BodyPreviewService(PreviewPane preview, Action<string, bool> notify,
        Func<ResponseBodyOptionsModel> options, bool bounded = true)
    {
        (_preview, _notify, _options) = (preview, notify, options);
        _bounded = bounded;
        AddCommand("Format", "Beautify / minify", ToggleFormat);
        AddCommand("Highlight", "Highlight", ToggleHighlight);
        AddCommand("Copy", "Copy body", Copy);
    }

    public void SetBody(string? body, string? contentType = null)
    {
        ResponseBodyOptionsModel options = _options();
        _language = ContentFormatHelpers.Detect(contentType, body);
        _body = body;
        _pretty = false;
        if (options.Format is not ResponseBodyFormat.None
            && ContentFormatHelpers.TryFormat(_language, body, options.Format is ResponseBodyFormat.Beautify,
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
        _highlighter = on ? SyntaxHighlighting.For(_language) : null;
        _preview.SetPageHighlighter(0, _highlighter is null ? null : Line);
    }

    private StyledRun[] Line(string line) =>
        line == ContentFormatting.Truncated
            ? [new StyledRun(0, line.Length, StraumrStyleService.CodeNote)]
            : _highlighter!(line);

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
        if (!ContentFormatHelpers.TryFormat(_language, _body, !_pretty, out string? formatted))
        {
            _notify(ContentFormatHelpers.CanFormat(_language)
                ? $"This body is not valid {ContentFormatHelpers.DisplayName(_language)}; formatting is unavailable."
                : $"Beautify and minify cover {ContentFormatHelpers.FormattableNames()} bodies.", true);
            return;
        }

        _body = formatted;
        _pretty = !_pretty;
        bool highlighted = _highlighter is not null;
        UpdatePreview();
        Highlight(highlighted && !_oversized);
        _notify($"{ContentFormatHelpers.DisplayName(_language)} {(_pretty ? "beautified" : "minified")}", false);
    }

    private void ToggleHighlight()
    {
        bool wanted = _highlighter is null;
        Highlight(wanted);
        if (wanted && _highlighter is null)
        {
            _notify("Straumr has no highlighting for this kind of body.", true);
            return;
        }

        _notify(_highlighter is not null
            ? _oversized
                ? $"{ContentFormatHelpers.DisplayName(_language)} highlighting on — {ContentFormatting.Size(Drawn().Length)} of text may scroll slowly"
                : $"{ContentFormatHelpers.DisplayName(_language)} highlighting on"
            : "Highlighting off", false);
    }

    private void Copy()
    {
        try
        {
            bool copied = _preview.Root.App?.Terminal.Clipboard.TrySetText(_body) == true;
            _notify(copied ? "Copied the full body" : "Clipboard is unavailable in this terminal.", !copied);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _notify($"Cannot copy body: {exception.Message}", true);
        }
    }
}
