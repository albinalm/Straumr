using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrSecretService(
    IStraumrFileService fileService,
    IStraumrStateService stateService,
    IStraumrSettingsService settingsService) : IStraumrSecretService
{
    public async Task<IReadOnlyList<StraumrSecret>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<StraumrSecret> secrets = new();
        foreach (StraumrSecretEntry entry in stateService.State.Secrets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                secrets.Add(await ReadByPathAsync(
                    entry.Path, false, cancellationToken));
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
        SecretLookupModel lookup = await RequireSecretAsync(
            name, $"No secret found with the name: {name}", cancellationToken);
        return await ResolveSecretAsync(lookup, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrSecret> CreateAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(secret.Name);
        string fullPath = SecretPath(secret.Id);
        await EnsureNoConflictAsync(secret.Name, fullPath, cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, secret, StraumrJsonContext.Default.StraumrSecret, cancellationToken);
        stateService.State.Secrets.Add(new StraumrSecretEntry
        {
            Id = secret.Id,
            Path = fullPath
        });
        await stateService.SaveAsync(cancellationToken);
        return secret;
    }

    public async Task<StraumrSecret> SaveAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(secret.Name);
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
        stateService.State.Secrets.Remove(entry);
        await stateService.SaveAsync(cancellationToken);
    }

    public string PathFor(Guid id) => SecretPath(id);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StraumrException("Secret names cannot be empty", StraumrError.InvalidEntry);
        }

        // Names must fit in one quoted CLI or TUI command argument.
        if (name.Contains('"'))
        {
            throw new StraumrException("Secret names cannot contain double quotes", StraumrError.InvalidEntry);
        }
    }

    private string SecretPath(Guid id) => Path.Combine(settingsService.DefaultSecretPath, id.ToString(), $"{id}.secret.jsonc");

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
        StraumrSecretEntry? entry = stateService.State.Secrets.FirstOrDefault(entry => entry.Id == id);
        return entry ?? throw new StraumrException(
            $"No secret found with the identifier: {id}", StraumrError.EntryNotFound);
    }

    private async Task EnsureNoConflictAsync(
        string name,
        string fullPath,
        Guid excludeId = default,
        CancellationToken cancellationToken = default)
    {
        foreach (StraumrSecretEntry entry in stateService.State.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrSecret secret = await ReadByPathAsync(
                    entry.Path, false, cancellationToken);
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

    private async Task<SecretLookupModel?> LookupSecretAsync(
        string name,
        CancellationToken cancellationToken)
    {
        foreach (StraumrSecretEntry entry in stateService.State.Secrets.Where(entry => File.Exists(entry.Path)))
        {
            try
            {
                StraumrSecret secret = await ReadByPathAsync(
                    entry.Path, false, cancellationToken);
                if (string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new SecretLookupModel(entry.Id, secret);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<SecretLookupModel> RequireSecretAsync(
        string name, string errorMessage, CancellationToken cancellationToken)
    {
        SecretLookupModel? lookup = await LookupSecretAsync(name, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrSecret> ResolveSecretAsync(
        SecretLookupModel lookup,
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
}
