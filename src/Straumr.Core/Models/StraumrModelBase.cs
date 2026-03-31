namespace Straumr.Core.Models;

public partial class StraumrModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;
}