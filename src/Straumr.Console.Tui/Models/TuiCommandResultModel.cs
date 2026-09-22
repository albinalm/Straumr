namespace Straumr.Console.Tui.Models;

public readonly record struct TuiCommandResultModel(string? Message, bool IsError)
{
    public static readonly TuiCommandResultModel None = new(null, false);

    public static readonly TuiCommandResultModel NoWorkspace =
        new("no active workspace; use :ws use <workspace> to choose one", true);

    public static TuiCommandResultModel Ok(string message) => new(message, false);

    public static TuiCommandResultModel Failed(string message) => new(message, true);
}
