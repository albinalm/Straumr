using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Commands.About;
using Straumr.Console.Cli.Commands.Auth;
using Straumr.Console.Cli.Commands.Autocomplete;
using Straumr.Console.Cli.Commands.Config;
using Straumr.Console.Cli.Commands.Request;
using Straumr.Console.Cli.Commands.Secret;
using Straumr.Console.Cli.Commands.Workspace;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Shared.Integrations;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Integration;

internal sealed class CliConsoleIntegration : IConsoleIntegration
{
    private static readonly HashSet<string> HelpAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "-h",
        "--help"
    };

    private readonly StraumrCommandRegistry _registry = new();
    private bool _registryInitialized;
    private CommandApp? _commandApp;
    private StraumrTypeRegistrar? _typeRegistrar;

    public string Name => CompletionCatalog.Cli;
    public IReadOnlyCollection<string> Aliases { get; } = [CompletionCatalog.Console];
    public IReadOnlyCollection<string> Commands => EnsureRegistry();
    public bool IsDefault => false;
    public bool OnlyRunOnEntrypoint => false;

    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Spectre.Console.Cli requires dynamic code; CLI assembly is fully preserved via CliRoots.xml.")]
    private IReadOnlyCollection<string> EnsureRegistry()
    {
        if (!_registryInitialized)
        {
            CommandApp dryRunApp = new CommandApp(new StraumrTypeRegistrar(new ServiceCollection()));
            dryRunApp.Configure(ConfigureCommands);
            foreach (string alias in HelpAliases)
            {
                _registry.Add(alias);
            }

            _registryInitialized = true;
        }

        return _registry.Commands;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Spectre.Console.Cli requires dynamic code; CLI assembly is fully preserved via CliRoots.xml.")]
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddStraumrCore();
        services.TryAddSingleton<EmptyCommandSettings>();

        if (_commandApp is null)
        {
            _typeRegistrar = new StraumrTypeRegistrar(services);
            _commandApp = new CommandApp(_typeRegistrar);
            _commandApp.Configure(ConfigureCommands);
        }

        // Spectre defers command type registration until RunAsync, but the
        // shared ServiceProvider is built before that. Pre-register all
        // command and settings types so they're available in the provider.
        RegisterCommandTypes(services);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2062",
        Justification = "Command types are preserved via CliRoots.xml trimmer descriptor.")]
    private static void RegisterCommandTypes(IServiceCollection services)
    {
        Assembly assembly = typeof(CliConsoleIntegration).Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (typeof(ICommand).IsAssignableFrom(type) || typeof(CommandSettings).IsAssignableFrom(type))
                services.AddSingleton(type, type);
        }
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (args.Length > 0 && HelpAliases.Contains(args[0]))
        {
            args[0] = "--help";
        }

        if (args.Contains("--version"))
        {
            Assembly assembly = typeof(CliConsoleIntegration).Assembly;
            string version = assembly.GetName().Version?.ToString() ?? "unknown";
            System.Console.WriteLine(version);
            return 0;
        }

        bool noColor = args.Contains("--no-color");
        if (noColor)
        {
            args = args.Where(a => a != "--no-color").ToArray();
            AnsiConsole.Profile.Capabilities.Ansi = false;
            AnsiConsole.Profile.Capabilities.Links = false;
        }

        if (_commandApp is null || _typeRegistrar is null)
            throw new InvalidOperationException("CLI integration has not been initialized.");

        IStraumrOptionsService optionsService = serviceProvider.GetRequiredService<IStraumrOptionsService>();
        await optionsService.LoadAsync();

        _typeRegistrar.UseServiceProvider(serviceProvider);

        return await _commandApp.RunAsync(args, cancellationToken);
    }

    private void ConfigureCommands(IConfigurator config)
    {
        config.SetApplicationName("Straumr");

        config.AddStraumrBranch(_registry, CompletionCatalog.List, list =>
        {
            list.AddCommand<WorkspaceListCommand>(CompletionCatalog.Workspace);
            list.AddCommand<WorkspaceListCommand>(CompletionCatalog.WorkspaceAlias);

            list.AddCommand<RequestListCommand>(CompletionCatalog.Request);
            list.AddCommand<RequestListCommand>(CompletionCatalog.RequestAlias);

            list.AddCommand<AuthListCommand>(CompletionCatalog.Auth);
            list.AddCommand<AuthListCommand>(CompletionCatalog.AuthAlias);

            list.AddCommand<SecretListCommand>(CompletionCatalog.Secret);
            list.AddCommand<SecretListCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Create, create =>
        {
            create.AddCommand<WorkspaceCreateCommand>(CompletionCatalog.Workspace);
            create.AddCommand<WorkspaceCreateCommand>(CompletionCatalog.WorkspaceAlias);

            create.AddCommand<RequestCreateCommand>(CompletionCatalog.Request);
            create.AddCommand<RequestCreateCommand>(CompletionCatalog.RequestAlias);

            create.AddCommand<AuthCreateCommand>(CompletionCatalog.Auth);
            create.AddCommand<AuthCreateCommand>(CompletionCatalog.AuthAlias);

            create.AddCommand<SecretCreateCommand>(CompletionCatalog.Secret);
            create.AddCommand<SecretCreateCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Delete, delete =>
        {
            delete.AddCommand<WorkspaceDeleteCommand>(CompletionCatalog.Workspace);
            delete.AddCommand<WorkspaceDeleteCommand>(CompletionCatalog.WorkspaceAlias);

            delete.AddCommand<RequestDeleteCommand>(CompletionCatalog.Request);
            delete.AddCommand<RequestDeleteCommand>(CompletionCatalog.RequestAlias);

            delete.AddCommand<AuthDeleteCommand>(CompletionCatalog.Auth);
            delete.AddCommand<AuthDeleteCommand>(CompletionCatalog.AuthAlias);

            delete.AddCommand<SecretDeleteCommand>(CompletionCatalog.Secret);
            delete.AddCommand<SecretDeleteCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Edit, edit =>
        {
            edit.AddCommand<WorkspaceEditCommand>(CompletionCatalog.Workspace);
            edit.AddCommand<WorkspaceEditCommand>(CompletionCatalog.WorkspaceAlias);

            edit.AddCommand<RequestEditCommand>(CompletionCatalog.Request);
            edit.AddCommand<RequestEditCommand>(CompletionCatalog.RequestAlias);

            edit.AddCommand<AuthEditCommand>(CompletionCatalog.Auth);
            edit.AddCommand<AuthEditCommand>(CompletionCatalog.AuthAlias);

            edit.AddCommand<SecretEditCommand>(CompletionCatalog.Secret);
            edit.AddCommand<SecretEditCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Get, get =>
        {
            get.AddCommand<WorkspaceGetCommand>(CompletionCatalog.Workspace);
            get.AddCommand<WorkspaceGetCommand>(CompletionCatalog.WorkspaceAlias);

            get.AddCommand<RequestGetCommand>(CompletionCatalog.Request);
            get.AddCommand<RequestGetCommand>(CompletionCatalog.RequestAlias);

            get.AddCommand<AuthGetCommand>(CompletionCatalog.Auth);
            get.AddCommand<AuthGetCommand>(CompletionCatalog.AuthAlias);

            get.AddCommand<SecretGetCommand>(CompletionCatalog.Secret);
            get.AddCommand<SecretGetCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Use, use =>
        {
            use.AddCommand<WorkspaceActivateCommand>(CompletionCatalog.Workspace);
            use.AddCommand<WorkspaceActivateCommand>(CompletionCatalog.WorkspaceAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Copy, copy =>
        {
            copy.AddCommand<WorkspaceCopyCommand>(CompletionCatalog.Workspace);
            copy.AddCommand<WorkspaceCopyCommand>(CompletionCatalog.WorkspaceAlias);

            copy.AddCommand<RequestCopyCommand>(CompletionCatalog.Request);
            copy.AddCommand<RequestCopyCommand>(CompletionCatalog.RequestAlias);

            copy.AddCommand<AuthCopyCommand>(CompletionCatalog.Auth);
            copy.AddCommand<AuthCopyCommand>(CompletionCatalog.AuthAlias);

            copy.AddCommand<SecretCopyCommand>(CompletionCatalog.Secret);
            copy.AddCommand<SecretCopyCommand>(CompletionCatalog.SecretAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Import, import =>
        {
            import.AddCommand<WorkspaceImportCommand>(CompletionCatalog.Workspace);
            import.AddCommand<WorkspaceImportCommand>(CompletionCatalog.WorkspaceAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Export, export =>
        {
            export.AddCommand<WorkspaceExportCommand>(CompletionCatalog.Workspace);
            export.AddCommand<WorkspaceExportCommand>(CompletionCatalog.WorkspaceAlias);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Config, cfg =>
        {
            cfg.AddCommand<ConfigWorkspacePathCommand>(CompletionCatalog.WorkspacePath);
        });

        config.AddStraumrBranch(_registry, CompletionCatalog.Autocomplete, autocomplete =>
        {
            autocomplete.AddCommand<AutocompleteInstallCommand>(CompletionCatalog.Install);
            autocomplete.AddCommand<AutocompleteQueryCommand>(CompletionCatalog.Query).IsHidden();
        });

        config.AddStraumrCommand<RequestSendCommand>(_registry, CompletionCatalog.Send);
        config.AddStraumrCommand<AboutCommand>(_registry, CompletionCatalog.About);
    }
}

public sealed class CliConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder) => builder.AddIntegration(new CliConsoleIntegration());
}
