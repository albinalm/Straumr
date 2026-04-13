using Straumr.Console.Tui.Services.Interfaces;
using Straumr.Core.Exceptions;

namespace Straumr.Console.Tui.Services;

public sealed class TuiOperationExecutor : ITuiOperationExecutor
{
    public bool TryExecute(Action action, Action<string> onError)
    {
        try
        {
            action();
            return true;
        }
        catch (StraumrException ex)
        {
            onError(ex.Message);
        }
        catch (Exception ex)
        {
            onError(ex.Message);
        }

        return false;
    }

    public bool TryExecute<TResult>(Func<TResult> action, Action<string> onError, out TResult result)
    {
        try
        {
            result = action();
            return true;
        }
        catch (StraumrException ex)
        {
            onError(ex.Message);
        }
        catch (Exception ex)
        {
            onError(ex.Message);
        }

        result = default!;
        return false;
    }
}
