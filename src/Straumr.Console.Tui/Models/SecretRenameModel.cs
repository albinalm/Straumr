namespace Straumr.Console.Tui.Models;

internal sealed record SecretRenameModel(string OldName, IReadOnlyList<SecretUsageModel> Usages);
