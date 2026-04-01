using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrAuthTemplateService(
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService) : IStraumrAuthTemplateService
{
    public async Task<IReadOnlyList<StraumrAuthTemplate>> ListAsync()
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        var templates = new List<StraumrAuthTemplate>();
        foreach (Guid id in workspace.AuthTemplates)
        {
            templates.Add(await PeekByIdAsync(id));
        }

        return templates;
    }

    public async Task<StraumrAuthTemplate> GetAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        TemplateLookup lookup = await RequireTemplateAsync(workspace, identifier,
            $"No auth template found with the identifier: {identifier}");

        return await ResolveTemplateAsync(lookup);
    }

    public async Task<StraumrAuthTemplate> PeekByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = TemplatePath(id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth template not found", StraumrError.EntryNotFound);
        }

        try
        {
            return await fileService.PeekStraumrModel(fullPath, StraumrJsonContext.Default.StraumrAuthTemplate);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid auth template", StraumrError.CorruptEntry, jex);
        }
    }

    public async Task CreateAsync(StraumrAuthTemplate template)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string fullPath = TemplatePath(template.Id);

        if (File.Exists(fullPath))
        {
            throw new StraumrException("Auth template already exists", StraumrError.EntryConflict);
        }

        await fileService.WriteStraumrModel(fullPath, template, StraumrJsonContext.Default.StraumrAuthTemplate);
        await AddTemplateToWorkspace(entry, template.Id);
    }

    public async Task UpdateAsync(StraumrAuthTemplate template)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string fullPath = TemplatePath(template.Id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth template not found", StraumrError.EntryNotFound);
        }

        await fileService.WriteStraumrModel(fullPath, template, StraumrJsonContext.Default.StraumrAuthTemplate);
        await StampWorkspaceAccessAsync(entry);
    }

    public async Task DeleteAsync(string identifier)
    {
        (StraumrWorkspaceEntry entry, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        TemplateLookup lookup = await RequireTemplateAsync(workspace, identifier, "No auth template found");
        RemoveTemplateFile(lookup.Id);
        workspace.AuthTemplates.Remove(lookup.Id);
        await PersistWorkspaceAsync(entry, workspace);
    }

    public async Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        TemplateLookup lookup = await RequireTemplateAsync(workspace, identifier, "No auth template found");
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(TemplatePath(lookup.Id), tempPath, true);
        return (lookup.Id, tempPath);
    }

    public void ApplyEdit(Guid templateId, string tempPath)
    {
        File.Copy(tempPath, TemplatePath(templateId), true);
    }

    private async Task<(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)> LoadWorkspaceAsync()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        return (entry, workspace);
    }

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
               ?? throw new StraumrException("No workspace loaded", StraumrError.MissingEntry);
    }

    private async Task AddTemplateToWorkspace(StraumrWorkspaceEntry entry, Guid id)
    {
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.AuthTemplates.Add(id);
        await PersistWorkspaceAsync(entry, workspace);
    }

    private async Task PersistWorkspaceAsync(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)
    {
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }

    private async Task StampWorkspaceAccessAsync(StraumrWorkspaceEntry entry)
    {
        await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
    }

    private string TemplatePath(Guid id)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, $"{id}.auth.json");
    }

    private void RemoveTemplateFile(Guid id)
    {
        string path = TemplatePath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<StraumrAuthTemplate> GetByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = TemplatePath(id);

        try
        {
            return await fileService.ReadStraumrModel(fullPath, StraumrJsonContext.Default.StraumrAuthTemplate);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid auth template", StraumrError.CorruptEntry, jex);
        }
    }

    private async Task<TemplateLookup?> LookupTemplateAsync(StraumrWorkspace workspace, string identifier)
    {
        if (Guid.TryParse(identifier, out Guid templateId) && workspace.AuthTemplates.Contains(templateId))
        {
            return new TemplateLookup(templateId, null);
        }

        foreach (Guid id in workspace.AuthTemplates)
        {
            try
            {
                StraumrAuthTemplate template = await PeekByIdAsync(id);
                if (template.Name == identifier)
                {
                    return new TemplateLookup(id, template);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<TemplateLookup> RequireTemplateAsync(
        StraumrWorkspace workspace, string identifier, string errorMessage)
    {
        TemplateLookup? lookup = await LookupTemplateAsync(workspace, identifier);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrAuthTemplate> ResolveTemplateAsync(TemplateLookup lookup)
    {
        return lookup.Template ?? await GetByIdAsync(lookup.Id);
    }

    private readonly record struct TemplateLookup(Guid Id, StraumrAuthTemplate? Template);
}