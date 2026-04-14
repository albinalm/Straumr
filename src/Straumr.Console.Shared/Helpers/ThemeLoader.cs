using Straumr.Console.Shared.Infrastructure;
using Straumr.Console.Shared.Theme;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Shared.Helpers;

public static class ThemeLoader
{
    public static async Task<StraumrThemeOptions> LoadAsync(IStraumrFileService fileService)
    {
        if (!File.Exists(ThemeConfigPath))
        {
            return new StraumrThemeOptions();
        }

        try
        {
            return await fileService.ReadGenericAsyncAsync(ThemeConfigPath, StraumrConsoleSharedJsonContext.Default.StraumrThemeOptions);
        }
        catch
        {
            return new StraumrThemeOptions();
        }
    }

    public static StraumrThemeOptions Load(IStraumrFileService fileService)
    {
        if (!File.Exists(ThemeConfigPath))
        {
            return new StraumrThemeOptions();
        }

        try
        {
            return fileService.ReadGeneric(ThemeConfigPath, StraumrConsoleSharedJsonContext.Default.StraumrThemeOptions);
        }
        catch
        {
            return new StraumrThemeOptions();
        }
    }
    
    private static string ThemeConfigPath => Path.Combine(
    Path.GetDirectoryName(StraumrOptionsService.OptionsPath)!,
    "theme.json");
}
