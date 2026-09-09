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
    public async Task<IReadOnlyList<StraumrSecret>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<StraumrSecret> secrets = new List<StraumrSecret>();
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                secrets.Add(await ReadByPathAsync(
                    entry.Path, updateLastAccessed: false, cancellationToken));
            }
            catch (StraumrException exception) when (
                exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
            }
        }

        return secrets;
    }

    public async Task<StraumrSecret> GetAsync(
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrSecretEntry entry = GetSecretEntry(id);
        return await ReadByPathAsync(entry.Path, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrSecret> GetAsync(
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecretLookup lookup = await RequireSecretAsync(
            name, $"No secret found with the name: {name}", cancellationToken);
        return await ResolveSecretAsync(lookup, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrSecret> CreateAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = SecretPath(secret.Id);
        await EnsureNoConflictAsync(secret.Name, fullPath, cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, secret, StraumrJsonContext.Default.StraumrSecret, cancellationToken);
        optionsService.Options.Secrets.Add(new StraumrSecretEntry
        {
            Id = secret.Id,
            Path = fullPath
        });
        await optionsService.SaveAsync(cancellationToken);
        return secret;
    }

    public async Task<StraumrSecret> SaveAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrSecretEntry entry = GetSecretEntry(secret.Id);
        string fullPath = entry.Path;

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Secret not found", StraumrError.EntryNotFound);
        }

        await EnsureNoConflictAsync(secret.Name, fullPath, secret.Id, cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, secret, StraumrJsonContext.Default.StraumrSecret, cancellationToken);
        return secret;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrSecretEntry entry = GetSecretEntry(id);
        RemoveSecretFile(entry.Path);
        optionsService.Options.Secrets.Remove(entry);
        await optionsService.SaveAsync(cancellationToken);
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

    private async Task<StraumrSecret> ReadByPathAsync(
        string path,
        bool updateLastAccessed,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new StraumrException("Secret not found", StraumrError.EntryNotFound);
        }

        try
        {
            return updateLastAccessed
                ? await fileService.ReadStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrSecret, cancellationToken)
                : await fileService.PeekStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrSecret, cancellationToken);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid secret", StraumrError.CorruptEntry, jex);
        }
    }

    private StraumrSecretEntry GetSecretEntry(Guid id)
    {
        StraumrSecretEntry? entry = optionsService.Options.Secrets.FirstOrDefault(entry => entry.Id == id);
        return entry ?? throw new StraumrException(
            $"No secret found with the identifier: {id}", StraumrError.EntryNotFound);
    }

    private async Task EnsureNoConflictAsync(
        string name,
        string fullPath,
        Guid excludeId = default,
        CancellationToken cancellationToken = default)
    {
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrSecret secret = await ReadByPathAsync(
                    entry.Path, updateLastAccessed: false, cancellationToken);
                if (string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("A secret with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound)
            {
            }
        }

        if (excludeId == default && File.Exists(fullPath))
        {
            throw new StraumrException("A secret already exists at this location", StraumrError.EntryConflict);
        }
    }

    private async Task<SecretLookup?> LookupSecretAsync(
        string name,
        CancellationToken cancellationToken)
    {
        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            try
            {
                StraumrSecret secret = await ReadByPathAsync(
                    entry.Path, updateLastAccessed: false, cancellationToken);
                if (string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new SecretLookup(entry.Id, secret);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<SecretLookup> RequireSecretAsync(
        string name, string errorMessage, CancellationToken cancellationToken)
    {
        SecretLookup? lookup = await LookupSecretAsync(name, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrSecret> ResolveSecretAsync(
        SecretLookup lookup,
        bool updateLastAccessed,
        CancellationToken cancellationToken)
    {
        if (updateLastAccessed)
        {
            StraumrSecretEntry entry = GetSecretEntry(lookup.Id);
            await fileService.StampAccessAsync(
                entry.Path, lookup.Secret, StraumrJsonContext.Default.StraumrSecret, cancellationToken);
        }

        return lookup.Secret;
    }

    private readonly record struct SecretLookup(Guid Id, StraumrSecret Secret);
}
