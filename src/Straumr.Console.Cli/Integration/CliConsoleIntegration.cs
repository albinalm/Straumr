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
using Straumr.Core.Services;
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

    public string Name => "cli";
    public IReadOnlyCollection<string> Aliases { get; } = ["console"];
    public IReadOnlyCollection<string> Commands => EnsureRegistry();
    public bool IsDefault => false;

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
        services.TryAddSingleton<IStraumrFileService, StraumrFileService>();
        services.TryAddSingleton<IStraumrOptionsService, StraumrOptionsService>();
        services.AddHttpClient();
        services.TryAddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.TryAddSingleton<IStraumrAuthService, StraumrAuthService>();
        services.TryAddSingleton<IStraumrRequestService, StraumrRequestService>();
        services.TryAddSingleton<IStraumrSecretService, StraumrSecretService>();
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
        await optionsService.Load();

        _typeRegistrar.UseServiceProvider(serviceProvider);

        return await _commandApp.RunAsync(args, cancellationToken);
    }

    private void ConfigureCommands(IConfigurator config)
    {
        config.SetApplicationName("Straumr");

        config.AddStraumrBranch(_registry, "list", list =>
        {
            list.AddCommand<WorkspaceListCommand>("workspace");
            list.AddCommand<WorkspaceListCommand>("ws");

            list.AddCommand<RequestListCommand>("request");
            list.AddCommand<RequestListCommand>("rq");

            list.AddCommand<AuthListCommand>("auth");
            list.AddCommand<AuthListCommand>("au");

            list.AddCommand<SecretListCommand>("secret");
            list.AddCommand<SecretListCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "create", create =>
        {
            create.AddCommand<WorkspaceCreateCommand>("workspace");
            create.AddCommand<WorkspaceCreateCommand>("ws");

            create.AddCommand<RequestCreateCommand>("request");
            create.AddCommand<RequestCreateCommand>("rq");

            create.AddCommand<AuthCreateCommand>("auth");
            create.AddCommand<AuthCreateCommand>("au");

            create.AddCommand<SecretCreateCommand>("secret");
            create.AddCommand<SecretCreateCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "delete", delete =>
        {
            delete.AddCommand<WorkspaceDeleteCommand>("workspace");
            delete.AddCommand<WorkspaceDeleteCommand>("ws");

            delete.AddCommand<RequestDeleteCommand>("request");
            delete.AddCommand<RequestDeleteCommand>("rq");

            delete.AddCommand<AuthDeleteCommand>("auth");
            delete.AddCommand<AuthDeleteCommand>("au");

            delete.AddCommand<SecretDeleteCommand>("secret");
            delete.AddCommand<SecretDeleteCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "edit", edit =>
        {
            edit.AddCommand<WorkspaceEditCommand>("workspace");
            edit.AddCommand<WorkspaceEditCommand>("ws");

            edit.AddCommand<RequestEditCommand>("request");
            edit.AddCommand<RequestEditCommand>("rq");

            edit.AddCommand<AuthEditCommand>("auth");
            edit.AddCommand<AuthEditCommand>("au");

            edit.AddCommand<SecretEditCommand>("secret");
            edit.AddCommand<SecretEditCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "get", get =>
        {
            get.AddCommand<WorkspaceGetCommand>("workspace");
            get.AddCommand<WorkspaceGetCommand>("ws");

            get.AddCommand<RequestGetCommand>("request");
            get.AddCommand<RequestGetCommand>("rq");

            get.AddCommand<AuthGetCommand>("auth");
            get.AddCommand<AuthGetCommand>("au");

            get.AddCommand<SecretGetCommand>("secret");
            get.AddCommand<SecretGetCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "use", use =>
        {
            use.AddCommand<WorkspaceActivateCommand>("workspace");
            use.AddCommand<WorkspaceActivateCommand>("ws");
        });

        config.AddStraumrBranch(_registry, "copy", copy =>
        {
            copy.AddCommand<WorkspaceCopyCommand>("workspace");
            copy.AddCommand<WorkspaceCopyCommand>("ws");

            copy.AddCommand<RequestCopyCommand>("request");
            copy.AddCommand<RequestCopyCommand>("rq");

            copy.AddCommand<AuthCopyCommand>("auth");
            copy.AddCommand<AuthCopyCommand>("au");

            copy.AddCommand<SecretCopyCommand>("secret");
            copy.AddCommand<SecretCopyCommand>("sc");
        });

        config.AddStraumrBranch(_registry, "import", import =>
        {
            import.AddCommand<WorkspaceImportCommand>("workspace");
            import.AddCommand<WorkspaceImportCommand>("ws");
        });

        config.AddStraumrBranch(_registry, "export", export =>
        {
            export.AddCommand<WorkspaceExportCommand>("workspace");
            export.AddCommand<WorkspaceExportCommand>("ws");
        });

        config.AddStraumrBranch(_registry, "config", cfg =>
        {
            cfg.AddCommand<ConfigWorkspacePathCommand>("workspace-path");
        });

        config.AddStraumrBranch(_registry, "autocomplete", autocomplete =>
        {
            autocomplete.AddCommand<AutocompleteInstallCommand>("install");
            autocomplete.AddCommand<AutocompleteQueryCommand>("query").IsHidden();
        });

        config.AddStraumrCommand<RequestSendCommand>(_registry, "send");
        config.AddStraumrCommand<AboutCommand>(_registry, "about");
    }
}

public sealed class CliConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder) => builder.AddIntegration(new CliConsoleIntegration());
}
