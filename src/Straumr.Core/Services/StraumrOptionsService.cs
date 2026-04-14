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

    public async Task LoadAsync()
    {
        if (!Directory.Exists(StraumrDir))
        {
            Directory.CreateDirectory(StraumrDir);
        }

        if (!File.Exists(OptionsPath))
        {
            await SaveAsync();
            return;
        }

        Options = await fileService.ReadGenericAsyncAsync(OptionsPath, StraumrJsonContext.Default.StraumrOptions);

        if (Options.CurrentWorkspace is not null && !File.Exists(Options.CurrentWorkspace.Path))
        {
            Options.CurrentWorkspace = null;
            await SaveAsync();
        }
    }

    public async Task SaveAsync()
    {
        await fileService.WriteGenericAsync(OptionsPath, Options, StraumrJsonContext.Default.StraumrOptions);
    }
}