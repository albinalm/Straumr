using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Enums;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Autocomplete;

public class AutocompleteInstallCommand : AsyncCommand<AutocompleteInstallCommand.Settings>
{
    private const string BeginMarker = "# Straumr autocomplete - DO NOT REMOVE (used to detect existing installation)";
    private const string EndMarker = "# End of Straumr autocomplete - DO NOT REMOVE";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellationToken)
    {
        ShellKind? detected = settings.Shell ?? DetectShell();
        if (detected is null)
        {
            AnsiConsole.MarkupLine("[red]Could not determine your shell.[/]");
            AnsiConsole.MarkupLine("Please specify it explicitly with [bold]--shell[/]: [grey]zsh[/], [grey]bash[/], or [grey]pwsh[/]");
            return 1;
        }
        ShellKind shell = detected.Value;

        string function = ReadEmbeddedScript(GetScriptResourceName(shell));

        string[] names = ["straumr", .. settings.Aliases];
        string block = BuildBlock(shell, function, names);

        string profilePath = settings.ProfilePath ?? GetProfilePath(shell);
        string? dir = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(profilePath))
        {
            string existing = await File.ReadAllTextAsync(profilePath, cancellationToken);
            int beginIdx = existing.IndexOf(BeginMarker, StringComparison.Ordinal);

            if (beginIdx >= 0)
            {
                int endIdx = existing.IndexOf(EndMarker, beginIdx, StringComparison.Ordinal);
                string after = endIdx >= 0
                    ? existing[(endIdx + EndMarker.Length)..].TrimStart('\r', '\n')
                    : existing[(beginIdx + BeginMarker.Length)..];

                string updated = existing[..beginIdx] + block + (after.Length > 0 ? "\n" + after : "");
                await File.WriteAllTextAsync(profilePath, updated, cancellationToken);
                AnsiConsole.MarkupLine($"[green]Autocomplete updated[/] in [bold]{Markup.Escape(profilePath)}[/]");
                PrintActivationHint(profilePath);
                return 0;
            }
        }

        await File.AppendAllTextAsync(profilePath, "\n" + block, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Autocomplete installed[/] in [bold]{Markup.Escape(profilePath)}[/]");
        PrintActivationHint(profilePath);
        return 0;
    }

    private static string BuildBlock(ShellKind shell, string function, string[] names)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BeginMarker);
        sb.AppendLine(function.Trim());

        switch (shell)
        {
            case ShellKind.Zsh:
                sb.AppendLine("if ! (( $+functions[compdef] )); then autoload -Uz compinit && compinit; fi");
                sb.AppendLine($"compdef _straumr_complete {string.Join(" ", names)}");
                break;
            case ShellKind.Bash:
                sb.AppendLine($"complete -F _straumr_completion {string.Join(" ", names)}");
                break;
            case ShellKind.Pwsh:
                sb.AppendLine(
                    $"Register-ArgumentCompleter -Native -CommandName {string.Join(", ", names)} -ScriptBlock $_straumrCompleter");
                break;
        }

        sb.AppendLine(EndMarker);
        return sb.ToString();
    }

    private static void PrintActivationHint(string profilePath)
    {
        AnsiConsole.MarkupLine($"Run [bold]source {Markup.Escape(profilePath)}[/] or restart your shell to activate.");
    }

    private static ShellKind? DetectShell()
    {
        string? shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shell))
        {
            if (shell.EndsWith("zsh",  StringComparison.OrdinalIgnoreCase))
            {
                return ShellKind.Zsh;
            }

            if (shell.EndsWith("bash", StringComparison.OrdinalIgnoreCase))
            {
                return ShellKind.Bash;
            }

            if (shell.EndsWith("pwsh", StringComparison.OrdinalIgnoreCase))
            {
                return ShellKind.Pwsh;
            }
        }
        
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PSModulePath")))
        {
            return ShellKind.Pwsh;
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsPwshOnPath())
        {
            return ShellKind.Pwsh;
        }

        return null;
    }

    private static bool IsPwshOnPath()
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null)
        {
            return false;
        }

        foreach (string dir in pathEnv.Split(Path.PathSeparator))
        {
            if (File.Exists(Path.Combine(dir, "pwsh.exe")))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetProfilePath(ShellKind shell)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return shell switch
        {
            ShellKind.Zsh => Path.Combine(home, ".zshrc"),
            ShellKind.Bash => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(home, ".bash_profile")
                : Path.Combine(home, ".bashrc"),
            ShellKind.Pwsh => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerShell",
                    "Microsoft.PowerShell_profile.ps1")
                : Path.Combine(home, ".config", "powershell", "Microsoft.PowerShell_profile.ps1"),
            _ => throw new ArgumentOutOfRangeException(nameof(shell))
        };
    }

    private static string GetScriptResourceName(ShellKind shell)
    {
        return shell switch
        {
            ShellKind.Zsh => "Straumr.Cli.Commands.Autocomplete.Scripts.completion.zsh",
            ShellKind.Bash => "Straumr.Cli.Commands.Autocomplete.Scripts.completion.bash",
            ShellKind.Pwsh => "Straumr.Cli.Commands.Autocomplete.Scripts.completion.ps1",
            _ => throw new ArgumentOutOfRangeException(nameof(shell))
        };
    }

    private static string ReadEmbeddedScript(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null!;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-s|--shell")]
        [Description("Shell to install autocomplete for (zsh, bash, pwsh)")]
        public ShellKind? Shell { get; set; }

        [CommandOption("-p|--profile")]
        [Description("The path to your profile file.")]
        public string? ProfilePath { get; set; }
        
        [CommandOption("-a|--alias")]
        [Description("Additional aliases to register for autocomplete")]
        public string[] Aliases { get; set; } = [];
    }
}
