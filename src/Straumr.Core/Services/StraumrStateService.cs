using Straumr.Core.Configuration;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

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
