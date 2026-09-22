namespace Straumr.Core.Models;

internal sealed class JsoncSlotModel
{
    public int MemberStart { get; init; }
    public int MemberEnd { get; set; }
    public int ContentEnd { get; set; }
    public int CloseStart { get; set; }
    public string Indent { get; init; } = string.Empty;
    public string? InnerIndent { get; set; }
    public bool IsContainer { get; set; }
}
