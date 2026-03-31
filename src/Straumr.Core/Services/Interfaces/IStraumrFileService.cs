using System.Text.Json.Serialization.Metadata;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrFileService
{
    Task Write<T>(string path, T value, JsonTypeInfo<T> typeInfo);
    Task<T> Read<T>(string path, JsonTypeInfo<T> typeInfo);
}
