using System;
using System.IO;
using System.Threading.Tasks;
using KamihamaWeb.Services;

namespace KamihamaWeb.Interfaces
{
    public interface IDiskCacheService
    {
        Guid Guid { get; }
    }

    public interface IDiskCacheSingleton: IDiskCacheService
    {
        public Task<DiskCacheItem> Get(string cacheItem, string versionMd5, bool forceOrigin = false);
        public Task<string> Store(string filepath, byte[] storeContents, DiskCacheService.StoreType type);
    }
}