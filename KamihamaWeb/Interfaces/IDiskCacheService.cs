using System;
using System.IO;
using System.Threading.Tasks;

namespace KamihamaWeb.Interfaces
{
    public interface IDiskCacheService
    {
        Guid Guid { get; }
    }

    public interface IDiskCacheSingleton: IDiskCacheService
    {
        public Task<Tuple<int, Stream>> Get(string cacheItem, string versionMd5);
    }
}