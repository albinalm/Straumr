namespace Straumr.Console.Tui.Models;

public readonly record struct TuiCommandCompletionModel(
    IReadOnlyList<string> Candidates,
    int ReplaceStart,
    int ReplaceLength)
{
    public static readonly TuiCommandCompletionModel None = new([], 0, 0);
}
