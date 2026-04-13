using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrFileService : IStraumrFileService
{
    public async Task WriteStraumrModel<T>(string path, T value, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase
    {
        await WriteInternal(path, value, typeInfo, true);
    }

    public async Task<T> ReadStraumrModel<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase
    {
        string json = await File.ReadAllTextAsync(path);
        T deserialized = JsonSerializer.Deserialize(json, typeInfo) ??
                         throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
        deserialized.LastAccessed = DateTimeOffset.UtcNow;
        await WriteInternal(path, deserialized, typeInfo, false);
        return deserialized;
    }

    public async Task<T> PeekStraumrModel<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }

    public async Task StampAccessAsync<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase
    {
        if (!File.Exists(path))
        {
            return;
        }

        string json = await File.ReadAllTextAsync(path);
        T? deserialized = JsonSerializer.Deserialize(json, typeInfo);
        if (deserialized is null)
        {
            return;
        }

        deserialized.LastAccessed = DateTimeOffset.UtcNow;
        await WriteInternal(path, deserialized, typeInfo, false);
    }

    public async Task WriteGeneric<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        EnsureDirectoryExists(path);

        string json = JsonSerializer.Serialize(value, typeInfo);
        await WriteTextAtomicAsync(path, json);
    }

    public async Task<T> ReadGenericAsync<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }
    
    public T ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }

    private async Task WriteInternal<T>(string path, T value, JsonTypeInfo<T> typeInfo, bool updateModify)
        where T : StraumrModelBase
    {
        EnsureDirectoryExists(path);

        if (updateModify)
        {
            value.Modified = DateTimeOffset.UtcNow;
        }

        string json = JsonSerializer.Serialize(value, typeInfo);
        await WriteTextAtomicAsync(path, json);
    }

    private void EnsureDirectoryExists(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static async Task WriteTextAtomicAsync(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        string tempPath = Path.Combine(directory, Path.GetRandomFileName());

        try
        {
            await File.WriteAllTextAsync(tempPath, content);
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
