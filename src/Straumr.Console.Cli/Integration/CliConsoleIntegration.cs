using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Commands.About;
using Straumr.Console.Cli.Commands.Auth;
using Straumr.Console.Cli.Commands.Autocomplete;
using Straumr.Console.Cli.Commands.Config;
using Straumr.Console.Cli.Commands.Request;
using Straumr.Console.Cli.Commands.Secret;
using Straumr.Console.Cli.Commands.Workspace;
using Straumr.Console.Cli.Console;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Shared.Console;
using Straumr.Console.Shared.Integrations;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Integration;

internal sealed class CliConsoleIntegration : IConsoleIntegration
{
    private readonly StraumrCommandRegistry _registry = new();
    private bool _registryInitialized;

    public string Name => "cli";
    public IReadOnlyCollection<string> Aliases { get; } = ["console"];
    public IReadOnlyCollection<string> Commands => EnsureRegistry();
    public bool IsDefault => false;
    
    private IReadOnlyCollection<string> EnsureRegistry()
    {
        if (!_registryInitialized)
        {
            var dryRunApp = new CommandApp(new StraumrTypeRegistrar(new ServiceCollection()));
            dryRunApp.Configure(ConfigureCommands);
            _registryInitialized = true;
        }

        return _registry.Commands;
    }
    
    [RequiresDynamicCode("Calls Spectre.Console.Cli.CommandApp.CommandApp(ITypeRegistrar)")]
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var services = new ServiceCollection();

        var fileService = new StraumrFileService();
        var optionsService = new StraumrOptionsService(fileService);
        await optionsService.Load();

        services.AddSingleton<IStraumrFileService>(fileService);
        services.AddSingleton<IStraumrOptionsService>(optionsService);
        services.AddHttpClient();
        services.AddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.AddSingleton<IStraumrAuthService, StraumrAuthService>();
        services.AddSingleton<IStraumrRequestService, StraumrRequestService>();
        services.AddSingleton<IStraumrSecretService, StraumrSecretService>();

        InteractiveConsoleFactory.TrySetFactory(() => new CliInteractiveConsole());
        services.AddSingleton<IInteractiveConsole>(_ => InteractiveConsoleFactory.Create());

        services.AddSingleton<EmptyCommandSettings>();

        var app = new CommandApp(new StraumrTypeRegistrar(services));
        app.Configure(ConfigureCommands);

        return await app.RunAsync(args, cancellationToken);
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
