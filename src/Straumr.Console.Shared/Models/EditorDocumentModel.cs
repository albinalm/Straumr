namespace Straumr.Console.Shared.Models;

public readonly record struct EditorDocumentModel(string Text, int Line, int Column)
{

    public bool HasPosition => Line > 1 || Column > 1;
    public static EditorDocumentModel AtStart(string text) => new(text, 1, 1);
}
