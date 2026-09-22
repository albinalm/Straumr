namespace Straumr.Core.Services.Interfaces;

public interface IStraumrSettingsService
{
    StraumrSettings Settings { get; }

    string SettingsPath { get; }

    string SettingsDirectory { get; }

    string? DefaultWorkspacePath { get; }

    string DefaultSecretPath { get; }

    ResponseBodyFormat ResponseBodyFormat { get; }

    bool ResponseHighlight { get; }

    int ResponseHighlightLimit { get; }

    int ResponseStoreLimit { get; }

    string? Problem { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task<string> EnsureFileAsync(CancellationToken cancellationToken = default);

    Task SetThemeAsync(string reference, CancellationToken cancellationToken = default);

    Task CompleteQuickStartAsync(string preset, string theme, CancellationToken cancellationToken = default);
}
