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
        var services = new ServiceCollection();

        var fileService = new StraumrFileService();
        var optionsService = new StraumrOptionsService(fileService);
        await optionsService.Load();

        services.AddSingleton<IStraumrFileService>(fileService);
        services.AddSingleton<IStraumrOptionsService>(optionsService);
        services.AddHttpClient();
        services.AddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.AddSingleton<IStraumrAuthService, StraumrAuthService>();
        services.AddSingleton<IStraumrAuthTemplateService, StraumrAuthTemplateService>();
        services.AddSingleton<IStraumrRequestService, StraumrRequestService>();
        services.AddSingleton<IStraumrSecretService, StraumrSecretService>();

        // Required for Spectre.Console.Cli to resolve the default settings type under Native AOT.
        services.AddSingleton<EmptyCommandSettings>();

        var app = new CommandApp(new StraumrTypeRegistrar(services));
        app.Configure(config =>
        {
            config.SetApplicationName("Straumr");
            config.AddBranch("workspace", workspace =>
            {
                workspace.AddCommand<WorkspaceCreateCommand>("create");
                workspace.AddCommand<WorkspaceActivateCommand>("use");
                workspace.AddCommand<WorkspaceImportCommand>("import");
                workspace.AddCommand<WorkspaceListCommand>("list");
                workspace.AddCommand<WorkspaceDeleteCommand>("delete");
                workspace.AddCommand<WorkspaceExportCommand>("export");
                workspace.AddCommand<WorkspaceEditCommand>("edit");
                workspace.AddCommand<WorkspaceGetCommand>("get");
            });
            config.AddBranch("request", request =>
            {
                request.AddCommand<RequestCreateCommand>("create");
                request.AddCommand<RequestSendCommand>("send");
                request.AddCommand<RequestEditCommand>("edit");
                request.AddCommand<RequestListCommand>("list");
                request.AddCommand<RequestDeleteCommand>("delete");
                request.AddCommand<RequestGetCommand>("get");
            });
            config.AddBranch("auth", auth =>
            {
                auth.AddCommand<AuthCreateCommand>("create");
                auth.AddCommand<AuthEditCommand>("edit");
                auth.AddCommand<AuthListCommand>("list");
                auth.AddCommand<AuthDeleteCommand>("delete");
                auth.AddCommand<AuthGetCommand>("get");
            });
            config.AddBranch("secret", secret =>
            {
                secret.AddCommand<SecretCreateCommand>("create");
                secret.AddCommand<SecretEditCommand>("edit");
                secret.AddCommand<SecretListCommand>("list");
                secret.AddCommand<SecretDeleteCommand>("delete");
                secret.AddCommand<SecretGetCommand>("get");
            });
            config.AddBranch("config", branch =>
            {
                branch.AddCommand<ConfigWorkspacePathCommand>("workspace-path");
            });
            config.AddBranch("autocomplete", autocomplete =>
            {
                autocomplete.HideBranch();
                autocomplete.AddCommand<AutocompleteInstallCommand>("install");
                autocomplete.AddCommand<AutocompleteQueryCommand>("query");
            });
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
