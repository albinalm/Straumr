using Straumr.Core.Configuration;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

/// <summary>
/// What the app knows about itself between runs: the workspace and secret registries, which
/// workspace is current, where panes were dragged to.
/// </summary>
/// <remarks>
/// None of it is written by a person, and all of it is rewritten whenever any of it moves. What a
/// person writes lives in <c>settings.toml</c> and is only ever read. The two used to be one file
/// called <c>options.json</c>, which was both the wrong shape and the wrong word — "options" and
/// "settings" are the same word, and nobody would have remembered which held the registry.
/// </remarks>
public class StraumrStateService(IStraumrFileService fileService) : IStraumrStateService
{
    private static readonly string StraumrDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr");

    public static readonly string StatePath = Path.Combine(StraumrDir, "state.json");

    public StraumrState State { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(StraumrDir))
        {
            Directory.CreateDirectory(StraumrDir);
        }

        if (!File.Exists(StatePath))
        {
            await SaveAsync(cancellationToken);
            return;
        }

        State = await fileService.ReadGenericAsync(
            StatePath, StraumrJsonContext.Default.StraumrState, cancellationToken);

        if (State.CurrentWorkspace is not null && !File.Exists(State.CurrentWorkspace.Path))
        {
            State.CurrentWorkspace = null;
            await SaveAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await fileService.WriteGenericAsync(
            StatePath, State, StraumrJsonContext.Default.StraumrState, cancellationToken);
    }
}
