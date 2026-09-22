using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record SecretJsonRenameModel(SecretScreenItemModel Item, StraumrSecret Secret, string Edited,
    string Original, SecretRenameModel? Rename);
