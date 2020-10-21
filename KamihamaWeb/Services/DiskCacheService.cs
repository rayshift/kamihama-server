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
            Directory.CreateDirectory(CacheDirectory);
        }

        public Guid Guid { get; }
        private IRestSharpTransient Rest { get; set; }
        private string CacheDirectory { get; set; } = "";

        public async Task<Tuple<int, Stream>> Get(string cacheItem, string versionMd5)
        {
            var filename = CryptUtil.CalculateSha256(cacheItem + "?" + versionMd5);

            var filePath = Path.Combine(CacheDirectory, filename);
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
                            return new Tuple<int, Stream>(0, stream);
                        }
                    }
                    catch (IOException) // File in use, wait
                    {
                        Log.Debug("Failed, file is already being downloaded, retrying in 1000ms.");
                        await Task.Delay(1000);
                    }
                }
                Log.Warning($"Max loops exceeded in DiskCacheService.Get() for {cacheItem}.");
                return new Tuple<int, Stream>(500, null);
            }
            Log.Debug($"Fetching {cacheItem}.");
            
            return await FastFetch(cacheItem, filePath, versionMd5);
        }

        private async Task<Tuple<int, Stream>> FastFetch(string item, string path, string md5)
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
                            return new Tuple<int, Stream>(0, file);
                        }

                        var stream = await Rest.FetchAsset(item + $"?{md5}");

                        if (stream.Item2 == null)
                        {
                            deleteFlag = true;
                            return stream;
                        }

                        ((MemoryStream) stream.Item2).WriteTo(file);
                        stream.Item2.Seek(0, SeekOrigin.Begin);
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
            return new Tuple<int, Stream>(500, null);
        }


    }
}