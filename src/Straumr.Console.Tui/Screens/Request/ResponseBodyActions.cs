using System.Text;
using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Request;

internal sealed class ResponseBodyActions
{
    private readonly PreviewPane _preview;
    private readonly Action<string, bool> _notify;
    private readonly bool _bounded;
    private string? _body;
    private bool _pretty;

    public ResponseBodyActions(PreviewPane preview, Action<string, bool> notify, bool bounded = true)
    {
        (_preview, _notify) = (preview, notify);
        _bounded = bounded;
        AddCommand("Format", "Beautify / minify", 'b', ToggleFormat);
        AddCommand("Copy", "Copy body", 'y', Copy);
    }

    public void SetBody(string? body)
    {
        _body = body;
        _pretty = false;
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
        try
        {
            using JsonDocument document = JsonDocument.Parse(_body!);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = !_pretty }))
                document.WriteTo(writer);
            _body = Encoding.UTF8.GetString(stream.ToArray());
            _pretty = !_pretty;
            UpdatePreview();
            _notify(_pretty ? "JSON beautified" : "JSON minified", false);
        }
        catch (JsonException)
        {
            _notify("This body is not valid JSON; formatting is unavailable.", true);
        }
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
