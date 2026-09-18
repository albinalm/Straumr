using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Request;

internal sealed class ResponseBodyActions
{
    private readonly PreviewPane _preview;
    private readonly Action<string, bool> _notify;
    private readonly Func<ResponseBodyFormat> _arrivalFormat;
    private readonly bool _bounded;
    private string? _body;
    private bool _pretty;

    public ResponseBodyActions(PreviewPane preview, Action<string, bool> notify,
        Func<ResponseBodyFormat> arrivalFormat, bool bounded = true)
    {
        (_preview, _notify, _arrivalFormat) = (preview, notify, arrivalFormat);
        _bounded = bounded;
        AddCommand("Format", "Beautify / minify", 'b', ToggleFormat);
        AddCommand("Copy", "Copy body", 'y', Copy);
    }

    public void SetBody(string? body)
    {
        ResponseBodyFormat format = _arrivalFormat();
        _body = body;
        _pretty = false;
        if (format is not ResponseBodyFormat.None
            && RequestEditingHelpers.TryFormatJson(body, indented: format is ResponseBodyFormat.Beautify,
                out string? formatted))
        {
            _body = formatted;
            _pretty = format is ResponseBodyFormat.Beautify;
        }

        UpdatePreview();
    }

    private void UpdatePreview() => _preview.SetPageText(0,
        _bounded ? ContentFormatting.Preview(_body) : string.IsNullOrEmpty(_body) ? "No body." : _body);

    private void AddCommand(string id, string label, char key, Action execute) =>
        _preview.Page(0).AddCommand(new Command
        {
            Id = $"ResponseBody.{id}", LabelMarkup = label, Gesture = new KeyGesture(key),
            Importance = CommandImportance.Primary, Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !string.IsNullOrEmpty(_body), IsVisible = _ => !string.IsNullOrEmpty(_body),
            Execute = _ => execute()
        });

    private void ToggleFormat()
    {
        if (!RequestEditingHelpers.TryFormatJson(_body, indented: !_pretty, out string? formatted))
        {
            _notify("This body is not valid JSON; formatting is unavailable.", true);
            return;
        }

        _body = formatted;
        _pretty = !_pretty;
        UpdatePreview();
        _notify(_pretty ? "JSON beautified" : "JSON minified", false);
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
