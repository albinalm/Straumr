using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Commands.Auth;
using Straumr.Cli.Commands.Autocomplete;
using Straumr.Cli.Commands.Config;
using Straumr.Cli.Commands.Request;
using Straumr.Cli.Commands.Secret;
using Straumr.Cli.Commands.Workspace;
using Straumr.Cli.Infrastructure;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli;

internal class Program
{
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Types are registered at startup with known implementations")]
    private static Task<int> Main(string[] args)
    {
        return RunApp(args);
    }

    [RequiresDynamicCode("Calls Spectre.Console.Cli.CommandApp.CommandApp(ITypeRegistrar)")]
    private static async Task<int> RunApp(string[] args)
    {
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

        // Required for Spectre.Console.Cli to resolve the default settings type under Native AOT.
        services.AddSingleton<EmptyCommandSettings>();

        var app = new CommandApp(new StraumrTypeRegistrar(services));
        app.Configure(config =>
        {
            config.SetApplicationName("Straumr");
            
            config.AddBranch("list", list =>
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

            config.AddBranch("create", create =>
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

            config.AddBranch("delete", delete =>
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

            config.AddBranch("edit", edit =>
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

            config.AddBranch("get", get =>
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

            config.AddBranch("use", use =>
            {
                use.AddCommand<WorkspaceActivateCommand>("workspace");
                use.AddCommand<WorkspaceActivateCommand>("ws");
            });

            config.AddBranch("copy", copy =>
            {
                copy.AddCommand<WorkspaceCopyCommand>("workspace");
                copy.AddCommand<WorkspaceCopyCommand>("ws");
            });

            config.AddBranch("import", import =>
            {
                import.AddCommand<WorkspaceImportCommand>("workspace");
                import.AddCommand<WorkspaceImportCommand>("ws");
            });

            config.AddBranch("export", export =>
            {
                export.AddCommand<WorkspaceExportCommand>("workspace");
                export.AddCommand<WorkspaceExportCommand>("ws");
            });

            config.AddBranch("config", cfg =>
            {
                cfg.AddCommand<ConfigWorkspacePathCommand>("workspace-path");
            });

            config.AddBranch("autocomplete", autocomplete =>
            {
                autocomplete.AddCommand<AutocompleteInstallCommand>("install");
                autocomplete.AddCommand<AutocompleteInstallCommand>("query").IsHidden();
            });

            config.AddCommand<RequestSendCommand>("send");
        });

        
        if (args.Length == 0)
        {
            AnsiConsole.Write(new FigletText("Straumr").Color(Color.Green));

            Assembly assembly = typeof(Program).Assembly;
            string version = assembly.GetName().Version?.ToString() ?? "unknown";

            Panel infoPanel = new Panel(new Markup($"[bold]Version:[/] {Markup.Escape(version)}"))
                .Border(BoxBorder.Square)
                .BorderColor(Color.Green)
                .Expand();

            AnsiConsole.Write(infoPanel);
            AnsiConsole.WriteLine();
        }

        return await app.RunAsync(args);
    }
}