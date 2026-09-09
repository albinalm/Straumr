using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Spectre.Console;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Console.PromptHelpers;

namespace Straumr.Console.Cli.Commands.Request;

internal static class RequestCommandHelpers
{
    internal static async Task<StraumrWorkspace> GetWorkspaceAsync(
        IStraumrWorkspaceService service,
        string identifier,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(identifier, out Guid id))
        {
            try
            {
                return await service.GetAsync(id, updateLastAccessed, cancellationToken);
            }
            catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
            {
            }
        }

        return await service.GetAsync(identifier, updateLastAccessed, cancellationToken);
    }

    internal static async Task<StraumrRequest> GetRequestAsync(
        IStraumrRequestService service,
        StraumrWorkspaceEntry workspace,
        string identifier,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(identifier, out Guid id))
        {
            try
            {
                return await service.GetAsync(workspace, id, updateLastAccessed, cancellationToken);
            }
            catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
            {
            }
        }

        return await service.GetAsync(workspace, identifier, updateLastAccessed, cancellationToken);
    }

    internal static async Task<StraumrAuth> GetAuthAsync(
        IStraumrAuthService service,
        StraumrWorkspaceEntry workspace,
        string identifier,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(identifier, out Guid id))
        {
            try
            {
                return await service.GetAsync(workspace, id, updateLastAccessed, cancellationToken);
            }
            catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
            {
            }
        }

        return await service.GetAsync(workspace, identifier, updateLastAccessed, cancellationToken);
    }

    internal static async Task<StraumrSecret> GetSecretAsync(
        IStraumrSecretService service,
        string identifier,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(identifier, out Guid id))
        {
            try
            {
                return await service.GetAsync(id, updateLastAccessed, cancellationToken);
            }
            catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
            {
            }
        }

        return await service.GetAsync(identifier, updateLastAccessed, cancellationToken);
    }

    internal static async Task<string> CreateEditorFileAsync<T>(
        T value,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        string json = JsonSerializer.Serialize(value, jsonTypeInfo);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }

    internal static async Task<int?> LaunchEditorAsync(string editor, string path, CancellationToken cancellation)
    {
        Process? process = Process.Start(new ProcessStartInfo(editor, path)
        {
            UseShellExecute = false
        });

        if (process is null)
        {
            AnsiConsole.MarkupLine("[red]Editor exited with an error.[/]");
            return 1;
        }

        await process.WaitForExitAsync(cancellation);

        if (process.ExitCode != 0)
        {
            ShowTransientMessage("[red]Editor exited with an error. Changes discarded.[/]");
            return process.ExitCode;
        }

        return null;
    }

    internal static async Task<StraumrWorkspaceEntry?> ResolveWorkspaceEntryAsync(
        string identifier,
        IStraumrWorkspaceService workspaceService)
    {
        if (Guid.TryParse(identifier, out Guid guid))
        {
            try
            {
                return workspaceService.GetEntry(guid);
            }
            catch (StraumrException) { }
        }

        try
        {
            StraumrWorkspace workspace = await workspaceService.GetAsync(identifier);
            return workspaceService.GetEntry(workspace.Id);
        }
        catch (StraumrException) { }

        return null;
    }
}
