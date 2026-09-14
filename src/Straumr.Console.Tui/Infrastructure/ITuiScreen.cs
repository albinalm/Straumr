using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Infrastructure;

public interface ITuiScreen
{
    TuiScreen Kind { get; }
    Visual Root { get; }
    Visual FocusTarget { get; }
    string? ActiveWorkspaceName { get; }
    IReadOnlyList<TuiCommand> PromptCommands { get; }
    event Action<TuiCommandResult>? NotificationRequested;
    event Action<TuiExternalAction>? ExternalActionRequested;
    Task LoadAsync(CancellationToken cancellationToken);
    Task UpdateAsync(CancellationToken cancellationToken);
}
