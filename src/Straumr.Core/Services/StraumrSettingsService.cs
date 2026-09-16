using Straumr.Core.Configuration;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Tomlyn;

namespace Straumr.Core.Services;

public class StraumrSettingsService : IStraumrSettingsService
{
    private static readonly string StraumrDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".straumr");

    public string SettingsPath { get; } = Path.Combine(StraumrDir, "settings.toml");

    public string SettingsDirectory => StraumrDir;

    public StraumrSettings Settings { get; private set; } = new();

    public string? Problem { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Problem = null;
        string path = await EnsureFileAsync(cancellationToken);
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Settings = new StraumrSettings();
            Problem = $"settings could not be read: {exception.Message}";
            return;
        }

        try
        {
            Settings = TomlSerializer.Deserialize(text, StraumrTomlContext.Default.StraumrSettings)
                       ?? new StraumrSettings();
        }
        catch (TomlException exception)
        {
            // The defaults stand and the caller reports the problem. Refusing to start over a typo
            // in a file whose whole purpose is to be typed in by hand would be the wrong trade.
            Settings = new StraumrSettings();
            Problem = $"settings.toml: {FirstLine(exception.Message)}";
        }
    }

    public async Task<string> EnsureFileAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(StraumrDir))
            Directory.CreateDirectory(StraumrDir);

        if (!File.Exists(SettingsPath))
            await File.WriteAllTextAsync(SettingsPath, StraumrSettings.Template, cancellationToken);

        return SettingsPath;
    }

    /// <summary>
    /// Tomlyn reports every diagnostic it collected. The footer is one row, so it gets the first.
    /// </summary>
    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
