using System.Text;
using System.Text.RegularExpressions;
using Humanizer;
using Straumr.Core.Extensions;

namespace Straumr.Core.Models;

public partial class StraumrModelBase
{
    public string Id => Name.ToStraumrId();
    public required string Name { get; set; }
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    

}