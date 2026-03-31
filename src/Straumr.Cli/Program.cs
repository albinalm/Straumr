using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
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
        services.AddSingleton<IStraumrScope, StraumrScope>();

        var fileService = new StraumrFileService();
        var optionsService = new StraumrOptionsService(fileService);
        await optionsService.Load();

        services.AddSingleton<IStraumrFileService>(fileService);
        services.AddSingleton<IStraumrOptionsService>(optionsService);
        services.AddTransient<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.AddTransient<IStraumrRequestService, StraumrRequestService>();

        var app = new CommandApp(new StraumrTypeRegistrar(services)); 
        app.Configure(config =>
        {
            config.SetApplicationName("straumr");

            config.AddBranch("workspace", workspace =>
            {
                workspace.AddCommand<WorkspaceCreateCommand>("create");
                workspace.AddCommand<WorkspaceLoadCommand>("load");
                workspace.AddCommand<WorkspaceImportCommand>("import");
                workspace.AddCommand<WorkspaceListCommand>("list");
                workspace.AddCommand<WorkspaceDeleteCommand>("delete");
                workspace.AddCommand<WorkspaceExportCommand>("export");
            });
        });

        return await app.RunAsync(args);
    }
}