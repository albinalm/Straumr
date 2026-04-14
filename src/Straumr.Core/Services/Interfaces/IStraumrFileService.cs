using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrFileService
{
    Task WriteStraumrModelAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task<T> ReadStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task<T> PeekStraumrModelAsync<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task StampAccessAsync<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task WriteGenericAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo);
    Task<T> ReadGenericAsyncAsync<T>(string path, JsonTypeInfo<T> typeInfo);
    T ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo);
}