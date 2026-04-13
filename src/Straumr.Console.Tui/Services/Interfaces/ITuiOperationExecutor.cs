using System.Diagnostics.CodeAnalysis;

namespace Straumr.Console.Tui.Services.Interfaces;

public interface ITuiOperationExecutor
{
    bool TryExecute(Action action, Action<string> onError);

    bool TryExecute<TResult>(Func<TResult> action, Action<string> onError, [MaybeNullWhen(false)] out TResult result);
}
