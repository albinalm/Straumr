using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrVariableService(IStraumrFileService fileService) : IStraumrVariableService
{
    public async Task<IReadOnlyList<StraumrVariable>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        List<StraumrVariable> variables = new();
        foreach (Guid id in workspaceModel.Variables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                variables.Add(await ReadByIdAsync(workspace, id, false, cancellationToken));
            }
            catch (StraumrException exception) when (
                exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
            }
        }

        return variables;
    }

    public async Task<StraumrVariable> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Variables.Contains(id))
        {
            throw new StraumrException("Variable not found", StraumrError.EntryNotFound);
        }

        return await ReadByIdAsync(workspace, id, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrVariable> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        VariableLookupModel lookup = await RequireVariableAsync(
            workspaceModel, name, $"No variable found with the name: {name}", workspace, cancellationToken);
        return await ResolveVariableAsync(lookup, workspace, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrVariable> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrVariable variable,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(variable.Name);
        string fullPath = VariablePath(variable.Id, workspace);
        if (File.Exists(fullPath))
        {
            throw new StraumrException("Variable already exists", StraumrError.EntryConflict);
        }

        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        await EnsureNoNameConflictAsync(variable.Name, workspace, workspaceModel: workspaceModel,
            cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, variable, StraumrJsonContext.Default.StraumrVariable, cancellationToken);
        workspaceModel.Variables.Add(variable.Id);
        await PersistWorkspaceAsync(workspace, workspaceModel, cancellationToken);
        return variable;
    }

    public async Task<StraumrVariable> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrVariable variable,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(variable.Name);
        string fullPath = VariablePath(variable.Id, workspace);
        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Variable not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(
            variable.Name, workspace, variable.Id, cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, variable, StraumrJsonContext.Default.StraumrVariable, cancellationToken);
        return variable;
    }

    public async Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Variables.Contains(id))
        {
            throw new StraumrException("Variable not found", StraumrError.EntryNotFound);
        }

        string variablePath = VariablePath(id, workspace);
        if (File.Exists(variablePath))
        {
            File.Delete(variablePath);
        }

        workspaceModel.Variables.Remove(id);
        await PersistWorkspaceAsync(workspace, workspaceModel, cancellationToken);
    }

    public string PathFor(StraumrWorkspaceEntry workspace, Guid id) => VariablePath(id, workspace);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StraumrException("Variable names cannot be empty", StraumrError.InvalidEntry);
        }

        if (name.Contains('"'))
        {
            throw new StraumrException("Variable names cannot contain double quotes", StraumrError.InvalidEntry);
        }

        if (name.Contains('{') || name.Contains('}'))
        {
            throw new StraumrException("Variable names cannot contain braces", StraumrError.InvalidEntry);
        }

        if (VariableHelpers.IsSecretName(name))
        {
            throw new StraumrException(
                $"Variable names cannot start with {VariableHelpers.SecretPrefix}; that prefix resolves a secret",
                StraumrError.InvalidEntry);
        }
    }

    private static string VariablePath(Guid id, StraumrWorkspaceEntry entry)
    {
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, $"{id}.jsonc");
    }

    private async Task<StraumrWorkspace> LoadWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken = default) =>
        await fileService.PeekStraumrModelAsync(
            entry.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

    private async Task PersistWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        StraumrWorkspace workspace,
        CancellationToken cancellationToken = default) =>
        await fileService.WriteStraumrModelAsync(
            entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

    private async Task<StraumrVariable> ReadByIdAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed,
        CancellationToken cancellationToken = default)
    {
        string path = VariablePath(id, workspace);
        if (!File.Exists(path))
        {
            throw new StraumrException("Variable not found", StraumrError.EntryNotFound);
        }

        try
        {
            return updateLastAccessed
                ? await fileService.ReadStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrVariable, cancellationToken)
                : await fileService.PeekStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrVariable, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new StraumrException("Invalid variable", StraumrError.CorruptEntry, exception);
        }
    }

    private async Task EnsureNoNameConflictAsync(
        string name,
        StraumrWorkspaceEntry entry,
        Guid excludeId = default,
        StraumrWorkspace? workspaceModel = null,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspace = workspaceModel ?? await LoadWorkspaceAsync(entry, cancellationToken);
        foreach (Guid id in workspace.Variables)
        {
            if (id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrVariable variable = await ReadByIdAsync(entry, id, false, cancellationToken);
                if (string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("A variable with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound) { }
        }
    }

    private async Task<VariableLookupModel?> LookupVariableAsync(
        StraumrWorkspace workspace,
        string name,
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        foreach (Guid id in workspace.Variables)
        {
            try
            {
                StraumrVariable variable = await ReadByIdAsync(entry, id, false, cancellationToken);
                if (string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new VariableLookupModel(id, variable);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<VariableLookupModel> RequireVariableAsync(
        StraumrWorkspace workspace,
        string name,
        string errorMessage,
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        VariableLookupModel? lookup = await LookupVariableAsync(workspace, name, entry, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrVariable> ResolveVariableAsync(
        VariableLookupModel lookup,
        StraumrWorkspaceEntry workspace,
        bool updateLastAccessed,
        CancellationToken cancellationToken)
    {
        if (updateLastAccessed)
        {
            await fileService.StampAccessAsync(
                VariablePath(lookup.Id, workspace), lookup.Variable,
                StraumrJsonContext.Default.StraumrVariable, cancellationToken);
        }

        return lookup.Variable;
    }
}
