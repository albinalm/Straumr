namespace Straumr.Console.Tui.Models;

internal sealed record ReferenceRenameModel(string OldName, IReadOnlyList<ReferenceUsageModel> Usages);
