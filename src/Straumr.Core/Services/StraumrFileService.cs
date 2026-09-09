using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrFileService : IStraumrFileService
{
    public async Task WriteStraumrModelAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase
    {
        await WriteInternal(path, value, typeInfo, true, cancellationToken);
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

        string json = JsonSerializer.Serialize(value, typeInfo);
        await WriteTextAtomicAsync(path, json, cancellationToken);
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
