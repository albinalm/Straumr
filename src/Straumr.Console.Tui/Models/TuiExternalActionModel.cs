using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

public sealed record TuiExternalActionModel(
    Func<CancellationToken, Task<TuiCommandResultModel>> ExecuteAsync,
    Visual FocusTarget);
