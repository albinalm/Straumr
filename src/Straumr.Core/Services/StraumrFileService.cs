using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrFileService : IStraumrFileService
{
    public async Task Write<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<T> Read<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize(json, typeInfo) ??
               throw new StraumrException("Failed to deserialize file", StraumrError.CorruptEntry);
    }
}