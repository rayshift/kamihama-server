using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Models;
using KamihamaWeb.Util;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace KamihamaWeb.Services
{
    public class MasterService: IMasterSingleton
    {

        private IDatabase _cache { get; set; }
        private IConfiguration _config { get; set; }
        private IRestSharpTransient _rest { get; set; }

        public MasterService(
            IDistributedCache cache, 
            IConfiguration config,
            IRestSharpTransient rest
            ) : this(Guid.NewGuid(), cache, config, rest)
        {
        }

        public MasterService(
            Guid guid, 
            IDistributedCache cache, 
            IConfiguration config,
            IRestSharpTransient rest
            )
        {
            Guid = guid;
            _config = config;
            _cache = ((RedisCache) cache).GetConnection().GetDatabase();
            _rest = rest;
            Task.Run(Initialize);
        }
        public Guid Guid { get; set; }

        public List<string> ModdedAssetLists = new List<string>()
        {
            "asset_main",
            "asset_prologue_main",
            "asset_prologue_voice",
            "asset_voice",
            "asset_fullvoice",
            "asset_char_list"
        };


        public async Task<bool> UpdateMasterLists()
        {
            Log.Information("Updating master lists.");
            var workGamedataAssets = new Dictionary<string, List<GamedataAsset>>();
            foreach (var assetToMod in ModdedAssetLists)
            {
                Log.Information($"Updating master entry {assetToMod}.json...");
                var masterJson = await _rest.GetMasterJson<List<GamedataAsset>>($"{assetToMod}.json.gz");

                if (masterJson == null)
                {
                    Log.Fatal($"Failed to parse JSON for {assetToMod}, quitting.");
                    return false;
                }

                workGamedataAssets.Add(assetToMod, masterJson);
            }

            IsReady = false;
            GamedataAssets.Clear();

            Log.Information("Configuring master list...");

            long counterReplace = 0;
            long counterSkip = 0;
            long counterNew = 0;
            foreach (var assetType in workGamedataAssets)
            {
                var readyAssets = new Dictionary<string, GamedataAsset>();
                foreach (var asset in assetType.Value)
                {
                    // Replace with english assets as needed
                    if (EnglishMasterAssets.ContainsKey(asset.Path))
                    {
                        if (EnglishMasterAssets[asset.Path].Md5 != asset.Md5)
                        {
                            //Log.Debug($"Replacing Japanese asset with English asset for {asset.Path}.");
                            readyAssets.Add(asset.Path, EnglishMasterAssets[asset.Path]);
                            counterReplace++;
                        }
                        else
                        {
                            //Log.Debug($"Asset {asset.Path} has the same MD5, skipping.");
                            readyAssets.Add(asset.Path, asset);
                            counterSkip++;
                        }
                    }
                    else
                    {
                        //Log.Debug($"Asset {asset.Path} not found in EN, adding.");
                        readyAssets.Add(asset.Path, asset);
                        counterNew++;
                    }
                }
                GamedataAssets.Add(assetType.Key, readyAssets);
            }
            Log.Information($"Finished setting up. {counterReplace} replaced assets, {counterSkip} duplicate assets, {counterNew} new assets.");

            // Add scripts
            foreach (var asset in EnglishMasterAssets)
            {
                if (asset.Key.StartsWith("scenario/json/adv/"))
                {
                    var split = asset.Key.Split("/").Last();
                    var scenario = split[0..^5]; // Trim .json from end
                    //Log.Debug($"Adding script {scenario}.");
                    GamedataAssets.Add($"asset_scenario_{scenario}", new Dictionary<string, GamedataAsset>()
                    {
                        {scenario,asset.Value}
                    });
                }
            }
            IsReady = true;
            return true;
        }

        public async Task Initialize()
        {
            Log.Information("Initializing master service.");
            GamedataAssets = new Dictionary<string, Dictionary<string, GamedataAsset>>();
            int delay = 1000;
            while (true)
            {
                Log.Information("Fetching current asset version...");
                var masterJson = await _rest.GetMasterJson<MasterJsonConfig>(_config["MagiRecoServer:AssetVersion"]);
                if (masterJson != null)
                {
                    Log.Information($"Asset version: {masterJson.version}");
                    AssetsCurrentVersion = masterJson.version;
                    break;
                }

                if (delay > 5 * 60 * 1000) // Reset delay after 5 minute delay is undertaken (eg. maintenance)
                {
                    delay = 1000;
                }
                await Task.Delay(delay);
                delay *= 2;
            }
            var builder= new MasterListBuilder();
            var lists = await builder.GenerateEnglishAssetList();

            if (lists != null)
            {
                EnglishMasterAssets = lists;
            }
            else
            {
                Log.Fatal("Creating English language master list failed.");
                return;
            }

            await UpdateMasterLists();
        }

        public bool IsReady { get; set; } = false;
        public long AssetsCurrentVersion { get; set; }
        public Dictionary<string, GamedataAsset> EnglishMasterAssets { get; set; }
        public Dictionary<string, Dictionary<string, GamedataAsset>> GamedataAssets { get; set; }

        public async Task<bool> IsUpdateRequired()
        {
            var masterJson = await _rest.GetMasterJson<MasterJsonConfig>(_config["MagiRecoServer:AssetVersion"]);
            if (masterJson != null && AssetsCurrentVersion < masterJson.version)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //public async Task<FileStream> ProvideFile(string filePath)
        //{

//        }

        public async Task<string> ProvideJson(string which)
        {
            if (which == "asset_config")
            {
                return JsonConvert.SerializeObject(new MasterJsonConfig()
                {
                    asset_optimize = 1,
                    version = AssetsCurrentVersion
                });
            }
            if (GamedataAssets.ContainsKey(which))
            {
                return JsonConvert.SerializeObject(GamedataAssets[which].Values);
            }

            return await _rest.GetAdditionalJson(which + ".json.gz");
        }
    }
}