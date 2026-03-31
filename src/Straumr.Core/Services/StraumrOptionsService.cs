using Straumr.Core.Configuration;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrOptionsService(IStraumrFileService fileService) : IStraumrOptionsService
{
    private static readonly string StraumrDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr");

    public static readonly string OptionsPath = Path.Combine(StraumrDir, "options.json");

    public StraumrOptions Options { get; private set; } = new();

    public async Task Load()
    {
        if (!Directory.Exists(StraumrDir))
        {
            Directory.CreateDirectory(StraumrDir);
        }

        if (!File.Exists(OptionsPath))
        {
            await Save();
            return;
        }

        Options = await fileService.ReadGeneric(OptionsPath, StraumrJsonContext.Default.StraumrOptions);
    }

    public async Task Save()
    {
        await fileService.WriteGeneric(OptionsPath, Options, StraumrJsonContext.Default.StraumrOptions);
    }
}
