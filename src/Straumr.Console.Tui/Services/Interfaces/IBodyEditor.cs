using Straumr.Core.Enums;

namespace Straumr.Console.Tui.Services.Interfaces;

public interface IBodyEditor
{
    BodyType Edit(
        IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies,
        BodyType currentType);
}
