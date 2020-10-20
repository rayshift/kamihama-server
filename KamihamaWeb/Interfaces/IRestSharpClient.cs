using System;
using System.IO;
using System.Threading.Tasks;
using KamihamaWeb.Models;

namespace KamihamaWeb.Interfaces
{
    public interface IRestSharpClient
    {
        Guid Guid { get; }
    }

    public interface IRestSharpTransient : IRestSharpClient
    {
        Task<T> GetMasterJson<T>(string masterJsonEndpoint);
        Task<Tuple<int, Stream>> FetchAsset(string item);
        Task<string> GetAdditionalJson(string item);
    }
}