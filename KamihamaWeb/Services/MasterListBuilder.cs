using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Models;
using KamihamaWeb.Util;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using StackExchange.Redis;

namespace KamihamaWeb.Services
{
    public class MasterListBuilder: IMasterListBuilder
    {
        private readonly Regex _multiPartRegex = new Regex(@"\.a[a-z]{2}"); // A bit crude

        private IDiskCacheSingleton _disk;
        private IDatabase _cache;

        public MasterListBuilder(IDiskCacheSingleton disk, IDistributedCache cache)
        {
            BasePathLength =
                @"MagiRecoStatic/magica/resource/download/asset/master/resource/".Length;

            _cache = ((RedisCache)cache).GetConnection().GetDatabase();
            _disk = disk;
        }

        private int BasePathLength { get; }

        private string StaticDirectory { get; set; } = "MagiRecoStatic/";

        public async Task<Dictionary<string, GamedataAsset>> GenerateEnglishAssetList()
        {
            if (File.Exists("en_cache.json"))
            {
                Log.Information("Loading cached English master list...");
                var cacheContents = await File.ReadAllTextAsync("en_cache.json");

                try
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, GamedataAsset>>(cacheContents);
                }
                catch (Exception ex)
                {
                    Log.Warning($"An exception was triggered while deserializing cached contents, regenerating it. {ex.ToString()}");
                    File.Delete("en_cache.json");
                }
            }

            Log.Information("First run detected, creating English master list from files on disk.");

            Dictionary<string, GamedataAsset> englishAssets = new Dictionary<string, GamedataAsset>();




            if (Directory.Exists(StaticDirectory))
            {
                List<string> files = Directory.GetFiles(Path.Combine(StaticDirectory, "magica/resource/download/asset/master"),
                    "*.*",
                    SearchOption.AllDirectories).ToList();

                foreach (var file in files)
                {

                    var fileInfo = GetFileInformation(file);
                    
                    englishAssets.Add(fileInfo.Path, fileInfo);

                }

                // Clean up multi-part files - not used
                /*foreach (var asset in englishAssets)
                {
                    if (multiPartRegex.IsMatch(asset.Path.Substring(asset.Path.Length - 4)))
                    {
                        var fileRemainder = asset.Path.Substring(0, asset.Path.Length - 4);
                        var existingGroupedAsset = englishAssets.FirstOrDefault(x => x.Path == fileRemainder);
                        if (existingGroupedAsset == null)
                        {
                            Log.Information($"Adding new asset {fileRemainder}.");
                            var newAsset = asset;
                            newAsset.Path = fileRemainder;
                            englishAssets.Add(newAsset);
                        }
                        else
                        {
                            Log.Information($"Appending to existing asset for {asset.Path}.");
                            existingGroupedAsset.FileList.Add(new GamedataFileList()
                            {
                                Url = asset.FileList[0].Url,
                                Size = asset.FileList[0].Size
                            });
                        }
                        Log.Information($"{asset.Path}");
                    }
                }*/

                // Remove multi-part files
                foreach (var asset in englishAssets)
                {
                    if (_multiPartRegex.IsMatch(asset.Value.Path.Substring(asset.Value.Path.Length - 4)))
                    {
                        Log.Debug($"Removing duplicate asset {asset.Key}");
                        englishAssets.Remove(asset.Value.Path);
                    }
                    else if (asset.Key.StartsWith("image_native/mini/") 
                             || asset.Key.StartsWith("image_native/live2d/")
                             //|| asset.Key.StartsWith("image_native/scene/gacha")
                             //|| asset.Key.StartsWith("scenario/json/general/")
                             || asset.Key.StartsWith("scenario/json/oneShot/")
                             || asset.Key.StartsWith("image_native/scene/event/")
                             || asset.Key.StartsWith("image_native/scene/emotion/")
                             || asset.Key.StartsWith("image_native/scene/event/")
                             || asset.Key.StartsWith("image_native/scene/web/")
                             )
                    {
                        Log.Debug($"Removing invalid asset {asset.Key}.");
                        englishAssets.Remove(asset.Value.Path);
                    }
                }

                Log.Information("Writing en_cache.json...");
                await File.WriteAllTextAsync("en_cache.json", JsonConvert.SerializeObject(englishAssets));
                Log.Information("Successfully wrote en_cache.json.");

                return englishAssets;
            }
            else
            {
                Log.Fatal("MagiRecoStatic does not exist! Please clone it to the root directory of the program.");
                return null;
            }
        }

        public GamedataAsset MergeAssets(GamedataAsset baseAsset, GamedataAsset newAsset)
        {
            Log.Information($"Merging {baseAsset.Path} and {newAsset.Path}");

            baseAsset.FileList.Add(newAsset.FileList[0]);
            baseAsset.FileList = baseAsset.FileList.OrderBy(x => x.Url).ToList();

            return baseAsset;
        }

        public GamedataAsset GetFileInformation(string file)
        {
            var fileInfo = new FileInfo(file);
            var filePath = file.Replace(@"\", "/").Replace("//", "/").Substring(BasePathLength);
            // Clean up path
            List<GamedataFileList> fileList = new List<GamedataFileList>();

            fileList.Add(new GamedataFileList()
            {
                Size = fileInfo.Length,
                Url = filePath
            });

            
            var fileMd5 = CryptUtil.CalculateMd5File(fileInfo.FullName);
            GamedataAsset asset = new GamedataAsset()
            {
                FileList = fileList,
                Md5 = fileMd5,
                Path = filePath,
                Type = AssetDownloadType.ENServer

            };

            return asset;
        }

        public async Task<GamedataAsset> BuildScenarioGeneralJson(GamedataAsset generalAsset, Dictionary<string, GamedataAsset> englishAssets)
        {
            //Log.Information($"Building scenario JSON for {generalAsset.Path}.");
            if (!englishAssets.ContainsKey(generalAsset.Path)) // No english to replace with (yet)
            {
                Log.Warning($"No english asset for {generalAsset.Path}! This should be caught earlier!");
                return generalAsset;
            }

            // Fetch JP asset
            DiskCacheItem jpAsset = await _disk.Get(generalAsset.Path, generalAsset.Md5, true);

            if (jpAsset.Result != DiskCacheResultType.Success)
            {
                Log.Warning("An error has occurred fetching a general scenario asset.");
                return generalAsset;
            }

            // Merge assets
            var enPath = Path.Combine(StaticDirectory, "magica/resource/download/asset/master/resource",
                generalAsset.Path);

            if (!File.Exists(enPath))
            {
                Log.Warning($"File {enPath} does not exist!");
                return generalAsset;
            }
            var mergedAsset = MergeScenarioGeneral(
                Encoding.UTF8.GetString(
                    CryptUtil.ReadFully(jpAsset.Data)
                    ),
                await File.ReadAllTextAsync(enPath)
                );

            var storeFilePath = await _disk.Store(generalAsset.Path, Encoding.UTF8.GetBytes(mergedAsset),
                DiskCacheService.StoreType.ScenarioGeneral);

            generalAsset.AssetSource = AssetSourceType.GeneralScript;
            generalAsset.Md5 = CryptUtil.CalculateMd5File(storeFilePath);
            generalAsset.FileList[0].Size = new FileInfo(storeFilePath).Length;
            return generalAsset;
        }

        private string MergeScenarioGeneral(string jp, string en)
        {
            if (jp == en)
            {
                return jp;
            }

            var jp_json = JsonConvert.DeserializeObject<ScenarioGeneral>(jp);
            var en_json = JsonConvert.DeserializeObject<ScenarioGeneral>(en);

            foreach (var item in jp_json.story)
            {
                if (!en_json.story.ContainsKey(item.Key))
                {
                    Log.Debug($"Adding key {item.Key}.");
                    en_json.story[item.Key] = item.Value;
                }
            }

            /*jp_json.Merge(en_json, new JsonMergeSettings()
            {
                MergeArrayHandling = MergeArrayHandling.Union,
                MergeNullValueHandling = MergeNullValueHandling.Ignore,
            });*/

            return JsonConvert.SerializeObject(en_json);
        }
    }
}