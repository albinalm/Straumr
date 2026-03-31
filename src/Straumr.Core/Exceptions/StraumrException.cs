using Straumr.Core.Enums;

namespace Straumr.Core.Exceptions;

public class StraumrException : Exception
{
    public StraumrError Reason { get; }

    public StraumrException(string message, StraumrError error) : base(message)
    {
        Reason = error;
    }

    public StraumrException(string message, StraumrError error, Exception innerException) : base(message, innerException)
    {
        Reason = error;
    }
}
