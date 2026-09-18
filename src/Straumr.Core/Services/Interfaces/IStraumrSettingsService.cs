using Straumr.Core.Enums;
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
    /// Where a new workspace is offered, expanded, or <see langword="null"/> when the reader has
    /// named nowhere and the app should not suggest one.
    /// </summary>
    string? DefaultWorkspacePath { get; }

    /// <summary>Where the global secret store lives, expanded. Always a path.</summary>
    string DefaultSecretPath { get; }

    ResponseBodyFormat ResponseBodyFormat { get; }

    /// <summary>
    /// Reads the file, or the template's defaults if it cannot be parsed. Never throws for bad TOML:
    /// <paramref name="problem"/> carries what was wrong so the caller can say so and carry on with
    /// the defaults, because a mistyped settings file must not be a refusal to start.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    string? Problem { get; }

    /// <summary>Writes the template if the file does not exist, and returns its path.</summary>
    Task<string> EnsureFileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the one setting the app offers a command for, leaving the rest of the file exactly
    /// as the reader wrote it — comments, ordering and spacing included.
    /// </summary>
    /// <remarks>
    /// The only value the program writes into a file that is otherwise the reader's. It is done
    /// through the TOML syntax tree rather than by serialising the model, because serialising would
    /// return a bare two-line file and throw away everything around it.
    /// </remarks>
    Task SetThemeAsync(string reference, CancellationToken cancellationToken = default);
}
