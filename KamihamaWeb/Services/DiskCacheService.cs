using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Util;
using Microsoft.Extensions.Configuration;
using RestSharp;
using Serilog;

namespace KamihamaWeb.Services
{
    public class DiskCacheService: IDiskCacheSingleton
    {
        public DiskCacheService(IConfiguration config, IRestSharpTransient rest) : this(Guid.NewGuid(), config, rest)
        {
        }

        public DiskCacheService(Guid guid, IConfiguration config, IRestSharpTransient rest)
        {
            Guid = guid;
            Rest = rest;
            if (string.IsNullOrEmpty(config["MagiRecoServer:CacheDirectory"]))
            {
                throw new Exception("CacheDirectory is not set in the configuration! Exiting.");
            }

            CacheDirectory = config["MagiRecoServer:CacheDirectory"];
            ScenarioCacheDirectory = $"{config["MagiRecoServer:CacheDirectory"]}/scenario/json/general/";
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(ScenarioCacheDirectory);
        }

        public Guid Guid { get; }
        private IRestSharpTransient Rest { get; set; }
        private string CacheDirectory { get; set; } = "";
        private string ScenarioCacheDirectory { get; set; } = "";

        public async Task<DiskCacheItem> Get(string cacheItem, string versionMd5, bool forceOrigin = false)
        {
            // Remember: don't allow directory traversal attacks...
            var filename = CryptUtil.CalculateSha256(cacheItem + "?" + versionMd5);
            var filePath = Path.Combine(CacheDirectory, filename);

            if (!forceOrigin && cacheItem.StartsWith("scenario/json/general"))
            {
                var generalJson = Path.Combine(ScenarioCacheDirectory, filename);
                if (File.Exists(generalJson))
                {
                    return new DiskCacheItem()
                    {
                        Data = File.Open(generalJson, FileMode.Open, FileAccess.Read, FileShare.Read),
                        Result = DiskCacheResultType.Success
                    };
                }
                else
                {
                    Log.Information($"Cache item {generalJson} not found! Falling back to origin.");
                }
            }
            if (File.Exists(filePath))
            {
                Log.Debug($"Loading {cacheItem} from disk ({filePath}).");
                var maxLoops = 5;
                while (maxLoops-- > 0)
                {
                    try
                    {
                        var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                        if (stream.Length == 0)
                        {
                            Log.Information($"Empty file found, deleting {filePath}.");
                            File.Delete(filePath);
                            return await FastFetch(cacheItem, filePath, versionMd5);
                        }
                        else
                        {
                            return new DiskCacheItem()
                            {
                                Data = stream
                            };
                        }
                    }
                    catch (IOException) // File in use, wait
                    {
                        Log.Information("Failed, file is already being downloaded, retrying in 1000ms.");
                        await Task.Delay(1000);
                    }
                }
                Log.Warning($"Max loops exceeded in DiskCacheService.Get() for {cacheItem}.");
                return new DiskCacheItem()
                {
                    Result = DiskCacheResultType.Failed
                };
            }
            Log.Information($"Fetching {cacheItem}.");
            
            return await FastFetch(cacheItem, filePath, versionMd5);
        }

        public async Task<string> Store(string filepath, byte[] storeContents, StoreType type)
        {
            string storePath;
            switch (type)
            {
                case StoreType.ScenarioGeneral:
                    var md5 = CryptUtil.CalculateMd5Bytes(storeContents);
                    var filename = CryptUtil.CalculateSha256(filepath + "?" + md5);
                    storePath = Path.Combine(ScenarioCacheDirectory, filename);
                    await File.WriteAllBytesAsync(storePath, storeContents);
                    break;
                default:
                    throw new Exception("Invalid StoreType.");
            }

            return storePath;
        }

        public enum StoreType
        {
            ScenarioGeneral
        }

        private async Task<DiskCacheItem> FastFetch(string item, string path, string md5)
        {
            var maxLoops = 5;
            var deleteFlag = false;
            while (maxLoops-- > 0)
            {
                try
                {
                    FileStream file = File.Open(path, FileMode.Create, FileAccess.ReadWrite,
                        FileShare.None); // Open file

                    try
                    {

                        if (file.Length > 0)
                        {
                            return new DiskCacheItem()
                            {
                                Data = file,
                                Result = DiskCacheResultType.Success
                            };
                        }

                        var stream = await Rest.FetchAsset(item + $"?{md5}");

                        if (stream.Data == null)
                        {
                            deleteFlag = true;
                            return stream;
                        }

                        ((MemoryStream) stream.Data).WriteTo(file);
                        stream.Data.Seek(0, SeekOrigin.Begin);
                        return stream;
                    }
                    catch (Exception)
                    {
                        deleteFlag = true;
                    }
                    finally
                    {
                        file.Close();
                        await file.DisposeAsync();
                        if (deleteFlag)
                        {
                            File.Delete(path);
                        }
                    }
                    
                }
                catch (IOException)
                {
                   await Task.Delay(500);
                }
            }
            Log.Warning($"Max loops exceeded in DiskCacheService.FastFetch() for {item}.");
            File.Delete(path);
            return new DiskCacheItem()
            {
                Result = DiskCacheResultType.Failed
            };
        }


    }

    public class DiskCacheItem
    {
        public Stream Data { get; set; } = null;
        public DiskCacheResultType Result { get; set; } = DiskCacheResultType.Success;
    }

    public enum DiskCacheResultType
    {
        Success = 0,
        Failed = 500,
        NotFound = 404
    }
}