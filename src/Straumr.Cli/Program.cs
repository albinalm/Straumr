using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Straumr.Cli.Commands.Request;
using Straumr.Cli.Commands.Workspace;
using Straumr.Cli.Infrastructure;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli;

class Program
{
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Types are registered at startup with known implementations")]
    static Task<int> Main(string[] args) => RunApp(args);

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
        services.AddSingleton<IStraumrRequestService, StraumrRequestService>();
        
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
            });
            config.AddBranch("request", request =>
            {
                request.AddCommand<RequestCreateCommand>("create");
                request.AddCommand<RequestSendCommand>("send");
                request.AddCommand<RequestEditCommand>("edit");
                request.AddCommand<RequestListCommand>("list");
                request.AddCommand<RequestDeleteCommand>("delete");
            });
        });

        return await app.RunAsync(args);
    }
}
