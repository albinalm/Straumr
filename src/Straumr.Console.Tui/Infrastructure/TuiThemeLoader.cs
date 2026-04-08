using Straumr.Console.Tui.Theme;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Infrastructure;

public static class TuiThemeLoader
{
    public static async Task<StraumrThemeOptions> LoadAsync(IStraumrFileService fileService)
    {
        string path = Path.Combine(
            Path.GetDirectoryName(StraumrOptionsService.OptionsPath)!,
            "theme.json");

        if (!File.Exists(path))
        {
            return new StraumrThemeOptions();
        }

        try
        {
            return await fileService.ReadGeneric(path, StraumrGuiJsonContext.Default.StraumrThemeOptions);
        }
        catch
        {
            return new StraumrThemeOptions();
        }
    }
}
