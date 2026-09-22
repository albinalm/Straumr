namespace Straumr.Console.Tui.Exceptions;

public sealed class ExternalEditorException : Exception
{
    public ExternalEditorException(string message)
        : base(message)
    {
    }

    public ExternalEditorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
