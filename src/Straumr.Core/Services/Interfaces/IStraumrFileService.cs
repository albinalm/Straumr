using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrFileService
{
    Task WriteStraumrModelAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase;

    Task<T> ReadStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase;

    Task<T> PeekStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase;

    Task StampAccessAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase;

    Task StampAccessAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) where T : StraumrModelBase;

    Task WriteGenericAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    Task<T> ReadGenericAsync<T>(string path, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    T ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo);
}
