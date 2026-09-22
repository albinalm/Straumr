using Straumr.Core.Configuration;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

public sealed class QuickStartService(
    IStraumrSettingsService settings,
    IStraumrStateService state,
    IStraumrWorkspaceService workspaces)
{
    public bool Active { get; private set; }
    public int Card { get; set; }
    public string Preset { get; set; } = "vim";
    public string ThemeReference { get; set; } = StraumrThemeService.DefaultReference;
    public List<QuickStartThemeModel> Themes { get; } = [];
    public IReadOnlyList<QuickStartWorkspaceModel> Workspaces { get; private set; } = [];
    public int WorkspaceIndex { get; set; }
    public string WorkspaceName { get; set; } = "";
    public string WorkspaceLocation { get; set; } = "";
    public bool CreatingWorkspace { get; set; }
    public string? Problem { get; set; }

    public async Task BeginAsync(CancellationToken token)
    {
        Card = 0;
        Problem = settings.Problem;
        Preset = StraumrKeybindPresets.Identify(settings.Settings.Keybinds);
        ThemeReference = string.IsNullOrWhiteSpace(settings.Settings.Theme)
            ? StraumrThemeService.DefaultReference : settings.Settings.Theme.Trim();
        WorkspaceName = "";
        WorkspaceLocation = settings.DefaultWorkspacePath ?? Path.Combine(settings.SettingsDirectory, "workspaces");
        Guid? current = state.State.CurrentWorkspace?.Id;
        Workspaces = (await workspaces.ListAsync(token)).Select(w => Describe(w, current)).ToList();
        CreatingWorkspace = Workspaces.Count == 0;
        WorkspaceIndex = Math.Max(0, Workspaces.ToList().FindIndex(w => w.IsCurrent));
        Themes.Clear();
        foreach (string name in StraumrThemeService.BuiltInNames)
        {
            AddTheme(name, name);
        }

        foreach ((string name, string path) in StraumrThemeService.Installed(settings.SettingsDirectory))
        {
            AddTheme(StraumrThemeService.BuiltInNames.Contains(name, StringComparer.OrdinalIgnoreCase) ? path : name,
                name + " (installed)");
        }

        if (!Themes.Any(t => t.Reference == ThemeReference))
        {
            AddTheme(ThemeReference, ThemeReference);
        }

        Active = true;
    }

    private QuickStartWorkspaceModel Describe(StraumrWorkspace workspace, Guid? current)
    {
        string? directory = null;
        try
        {
            string path = workspaces.GetEntry(workspace.Id).Path;
            directory = PathFormatting.Display(Path.GetDirectoryName(path) ?? path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or KeyNotFoundException)
        {
        }

        return new QuickStartWorkspaceModel(workspace.Id, workspace.Name, workspace.Requests.Count,
            workspace.Auths.Count, directory, workspace.Id == current);
    }

    private void AddTheme(string reference, string label)
    {
        StraumrThemeService.TryResolve(reference, settings.SettingsDirectory, out StraumrThemeModel theme, out string? problem);
        Themes.Add(new QuickStartThemeModel(reference, label, theme, problem));
    }

    public async Task ActivateWorkspaceAsync(CancellationToken token)
    {
        Guid id;
        if (!CreatingWorkspace && Workspaces.Count > 0)
        {
            id = Workspaces[Math.Clamp(WorkspaceIndex, 0, Workspaces.Count - 1)].Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(WorkspaceName))
            {
                throw new InvalidOperationException("Give your workspace a name.");
            }

            if (string.IsNullOrWhiteSpace(WorkspaceLocation))
            {
                throw new InvalidOperationException("Choose a workspace location.");
            }

            StraumrWorkspace created = await workspaces.CreateAsync(
                new StraumrWorkspace { Name = WorkspaceName.Trim() }, WorkspaceLocation.Trim(), token);
            Workspaces = [Describe(created, created.Id)];
            WorkspaceIndex = 0;
            CreatingWorkspace = false;
            id = created.Id;
        }
        await workspaces.ActivateAsync(id, token);
    }

    public void PreviewPreset(string name)
    {
        Preset = name;
        StraumrKeybinds.Apply(StraumrKeybindPresets.Resolve(name));
    }

    public async Task CompleteAsync(CancellationToken token)
    {
        await settings.CompleteQuickStartAsync(Preset, ThemeReference, token);
        Active = false;
    }
}
