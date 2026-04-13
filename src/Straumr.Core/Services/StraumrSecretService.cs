using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrSecretService(
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService) : IStraumrSecretService
{
    public async Task<StraumrSecret> GetAsync(string identifier)
    {
        SecretLookup lookup = await RequireSecretAsync(identifier,
            $"No secret found with the identifier: {identifier}");

        return await ResolveSecretAsync(lookup);
    }

    public async Task<StraumrSecret> PeekByIdAsync(Guid id)
    {
        StraumrSecretEntry entry = GetSecretEntry(id);
        return await PeekByPathAsync(entry.Path);
    }

    public async Task CreateAsync(StraumrSecret secret)
    {
        string fullPath = SecretPath(secret.Id);
        await EnsureNoConflictAsync(secret.Name, fullPath, secret.Id);

        if (File.Exists(fullPath))
        {
            throw new StraumrException("Secret already exists", StraumrError.EntryConflict);
        }

        await fileService.WriteStraumrModel(fullPath, secret, StraumrJsonContext.Default.StraumrSecret);
        optionsService.Options.Secrets.Add(new StraumrSecretEntry
        {
            Id = secret.Id,
            Path = fullPath
        });
        await optionsService.Save();
    }

    public async Task UpdateAsync(StraumrSecret secret)
    {
        StraumrSecretEntry entry = GetSecretEntry(secret.Id);
        string fullPath = entry.Path;

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Secret not found", StraumrError.EntryNotFound);
        }

        await EnsureNoConflictAsync(secret.Name, fullPath, secret.Id);

        await fileService.WriteStraumrModel(fullPath, secret, StraumrJsonContext.Default.StraumrSecret);
    }

    public async Task<StraumrSecret> CopyAsync(string identifier, string newName)
    {
        SecretLookup lookup = await RequireSecretAsync(identifier, "No secret found");
        StraumrSecret source = await GetByIdAsync(lookup.Id);

        StraumrSecret copy = new StraumrSecret
        {
            Name = newName,
            Value = source.Value
        };

        await CreateAsync(copy);
        return copy;
    }

    public async Task DeleteAsync(string identifier)
    {
        SecretLookup lookup = await RequireSecretAsync(identifier, "No secret found");
        StraumrSecretEntry entry = GetSecretEntry(lookup.Id);
        RemoveSecretFile(entry.Path);
        optionsService.Options.Secrets.Remove(entry);
        await optionsService.Save();
    }

    public async Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier)
    {
        SecretLookup lookup = await RequireSecretAsync(identifier, "No secret found");
        StraumrSecretEntry entry = GetSecretEntry(lookup.Id);
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(entry.Path, tempPath, true);
        return (lookup.Id, tempPath);
    }

    public void ApplyEdit(Guid secretId, string tempPath)
    {
        StraumrSecretEntry entry = GetSecretEntry(secretId);
        File.Copy(tempPath, entry.Path, true);
    }

    private string SecretPath(Guid id)
    {
        return Path.Combine(optionsService.Options.DefaultSecretPath, id.ToString(), $"{id}.secret.json");
    }

    private void RemoveSecretFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<StraumrSecret> GetByIdAsync(Guid id)
    {
        try
        {
            StraumrSecretEntry entry = GetSecretEntry(id);
            return await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrSecret);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid secret", StraumrError.CorruptEntry, jex);
        }
    }

    private async Task<StraumrSecret> PeekByPathAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new StraumrException("Secret not found", StraumrError.EntryNotFound);
        }

        try
        {
            return await fileService.PeekStraumrModel(path, StraumrJsonContext.Default.StraumrSecret);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid secret", StraumrError.CorruptEntry, jex);
        }
    }

    private StraumrSecretEntry GetSecretEntry(Guid id)
    {
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        throw new StraumrException($"No secret found with the identifier: {id}", StraumrError.EntryNotFound);
    }

    private async Task EnsureNoConflictAsync(string name, string fullPath, Guid excludeId = default)
    {
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrSecret secret = await PeekByPathAsync(entry.Path);
                if (string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("A secret with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound)
            {
            }
        }

        if (File.Exists(fullPath))
        {
            throw new StraumrException("A secret already exists at this location", StraumrError.EntryConflict);
        }
    }

    private async Task<SecretLookup?> LookupSecretAsync(string identifier)
    {
        if (Guid.TryParse(identifier, out Guid secretId) && optionsService.Options.Secrets.Any(x => x.Id == secretId))
        {
            return new SecretLookup(secretId, null);
        }

        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            try
            {
                StraumrSecret secret = await PeekByPathAsync(entry.Path);
                if (string.Equals(secret.Name, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return new SecretLookup(entry.Id, secret);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<SecretLookup> RequireSecretAsync(
        string identifier, string errorMessage)
    {
        SecretLookup? lookup = await LookupSecretAsync(identifier);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrSecret> ResolveSecretAsync(SecretLookup lookup)
    {
        return lookup.Secret ?? await GetByIdAsync(lookup.Id);
    }

    private readonly record struct SecretLookup(Guid Id, StraumrSecret? Secret);
}
