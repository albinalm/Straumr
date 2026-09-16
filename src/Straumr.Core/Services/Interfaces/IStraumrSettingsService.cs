using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrSettingsService
{
    StraumrSettings Settings { get; }

    /// <summary>The file a reader edits. It is created from the template if it is not there.</summary>
    string SettingsPath { get; }

    /// <summary>The directory a relative path in the settings file is resolved against.</summary>
    string SettingsDirectory { get; }

    /// <summary>
    /// Reads the file, or the template's defaults if it cannot be parsed. Never throws for bad TOML:
    /// <paramref name="problem"/> carries what was wrong so the caller can say so and carry on with
    /// the defaults, because a mistyped settings file must not be a refusal to start.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    string? Problem { get; }

    /// <summary>Writes the template if the file does not exist, and returns its path.</summary>
    Task<string> EnsureFileAsync(CancellationToken cancellationToken = default);
}
