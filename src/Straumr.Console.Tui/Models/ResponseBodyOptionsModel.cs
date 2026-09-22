using Straumr.Core.Enums;

namespace Straumr.Console.Tui.Models;

internal readonly record struct ResponseBodyOptionsModel(
    ResponseBodyFormat Format,
    bool Highlight,
    int HighlightLimit);
