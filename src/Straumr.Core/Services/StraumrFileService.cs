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

    public async Task WriteGeneric<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        EnsureDirectoryExists(path);

        string json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<T> ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        string json = await File.ReadAllTextAsync(path);
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
        await File.WriteAllTextAsync(path, json);
    }

    private void EnsureDirectoryExists(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }
    }
}