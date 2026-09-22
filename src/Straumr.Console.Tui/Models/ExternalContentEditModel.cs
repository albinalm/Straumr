namespace Straumr.Console.Tui.Models;

internal sealed record ExternalContentEditModel(
    EditorDocumentModel Document, string Extension, Action<string> Apply);
