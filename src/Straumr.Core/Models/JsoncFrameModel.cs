namespace Straumr.Core.Models;

internal sealed class JsoncFrameModel(string path, bool isArray)
{
    public string Path { get; } = path;
    public bool IsArray { get; } = isArray;
    public int Index { get; set; }
    public string? InnerIndent { get; set; }
}
