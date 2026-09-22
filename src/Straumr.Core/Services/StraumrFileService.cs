using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrFileService : IStraumrFileService
{
    private (string Path, string Jsonc)? _handoff;

    public void CarryCommentsFrom(string path, string jsonc)
    {
        _handoff = (Path.GetFullPath(path), jsonc);
    }

    public async Task WriteStraumrModelAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        await WriteInternal(path, value, typeInfo, true, cancellationToken);
    }

    public async Task WriteStraumrModelAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        bool updateModified, CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        await WriteInternal(path, value, typeInfo, updateModified, cancellationToken);
    }

    public async Task<T> ReadStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken);
        T deserialized = JsonSerializer.Deserialize(json, typeInfo) ??
                         throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
        await StampAccessAsync(path, deserialized, typeInfo, cancellationToken);
        return deserialized;
    }

    public async Task<T> PeekStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }

    public async Task StampAccessAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        if (!File.Exists(path))
        {
            return;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        T? deserialized = JsonSerializer.Deserialize(json, typeInfo);
        if (deserialized is null)
        {
            return;
        }

        await StampAccessAsync(path, deserialized, typeInfo, cancellationToken);
    }

    public async Task StampAccessAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        value.LastAccessed = DateTimeOffset.UtcNow;
        await WriteInternal(path, value, typeInfo, false, cancellationToken);
    }

    public async Task WriteGenericAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(path);

        string json = JsonSerializer.Serialize(value, typeInfo);
        await WriteTextAtomicAsync(path, json, cancellationToken);
    }

    public async Task<T> ReadGenericAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }

    public T ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }

    private async Task WriteInternal<T>(string path, T value, JsonTypeInfo<T> typeInfo, bool updateModify,
        CancellationToken cancellationToken)
        where T : StraumrModelBase
    {
        EnsureDirectoryExists(path);

        if (updateModify)
        {
            value.Modified = DateTimeOffset.UtcNow;
        }

        string previous = await CommentSourceAsync(path, cancellationToken);
        string json = JsonSerializer.Serialize(value, typeInfo);
        await WriteTextAtomicAsync(path, JsoncCommentHelpers.Carry(previous, json), cancellationToken);
    }

    private async Task<string> CommentSourceAsync(string path, CancellationToken cancellationToken)
    {
        if (_handoff is { } handoff &&
            string.Equals(handoff.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            _handoff = null;
            return handoff.Jsonc;
        }

        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static async Task WriteTextAtomicAsync(
        string path, string content, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        string tempPath = Path.Combine(directory, Path.GetRandomFileName());

        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
