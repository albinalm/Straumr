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

    /// <summary>
    /// Raised when the screen puts a full-screen surface of its own up — an editor, a response.
    /// The shell needs it to know that a command it dispatched took over the terminal, which is
    /// what makes closing that surface worth a return to the screen the command came from.
    /// </summary>
    event Action? TransientScreenOpened;

    event Action? TransientScreenClosed;
    Task LoadAsync(CancellationToken cancellationToken);
    Task UpdateAsync(CancellationToken cancellationToken);
}
