using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record VariableJsonRenameModel(VariableScreenItemModel Item, StraumrVariable Variable, string Edited,
    string Original, ReferenceRenameModel? Rename);
