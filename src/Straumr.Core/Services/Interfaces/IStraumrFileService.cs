using System.Text.Json.Serialization.Metadata;
using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrFileService
{
    Task WriteStraumrModel<T>(string path, T value, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task<T> ReadStraumrModel<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task<T> PeekStraumrModel<T>(string path, JsonTypeInfo<T> typeInfo) where T : StraumrModelBase;
    Task WriteGeneric<T>(string path, T value, JsonTypeInfo<T> typeInfo);
    Task<T> ReadGeneric<T>(string path, JsonTypeInfo<T> typeInfo);
}
