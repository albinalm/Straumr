using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Interfaces;

public interface ITuiScreen
{
    TuiScreen Kind { get; }
    Visual Root { get; }
    Visual FocusTarget { get; }
    string? ActiveWorkspaceName { get; }
    IReadOnlyList<TuiCommandModel> PromptCommands { get; }
    event Action<TuiCommandResultModel>? NotificationRequested;
    event Action<TuiExternalActionModel>? ExternalActionRequested;

    event Action? TransientScreenOpened;

    event Action? TransientScreenClosed;
    Task LoadAsync(CancellationToken cancellationToken);
    Task UpdateAsync(CancellationToken cancellationToken);
}
