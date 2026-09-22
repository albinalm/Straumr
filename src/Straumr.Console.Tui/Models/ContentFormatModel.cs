namespace Straumr.Console.Tui.Models;

internal sealed record ContentFormatModel(string Extension, Func<string, EditorDocumentModel> Prepare);
