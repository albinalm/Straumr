using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Infrastructure;

public sealed record TuiExternalAction(
    Func<CancellationToken, Task<TuiCommandResult>> ExecuteAsync,
    Visual FocusTarget);
