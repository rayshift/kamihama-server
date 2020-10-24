using System;
using System.IO;
using System.Threading.Tasks;
using KamihamaWeb.Models;
using KamihamaWeb.Services;

namespace KamihamaWeb.Interfaces
{
    public interface IRestSharpClient
    {
        Guid Guid { get; }
    }

    public interface IRestSharpTransient : IRestSharpClient
    {
        Task<T> GetMasterJson<T>(string masterJsonEndpoint);
        Task<DiskCacheItem> FetchAsset(string item);
        Task<string> GetAdditionalJson(string item);
    }
}