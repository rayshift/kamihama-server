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

       // private IDatabase _cache { get; set; }
        private IConfiguration _config { get; set; }
        private IRestSharpTransient _rest { get; set; }
        private IMasterListBuilder _builder { get; set; }

        public MasterService(
            //IDistributedCache cache, 
            IConfiguration config,
            IRestSharpTransient rest,
            IMasterListBuilder builder
            ) : this(Guid.NewGuid(), 
            //cache, 
            config, rest, builder)
        {
        }

        public MasterService(
            Guid guid, 
            //IDistributedCache cache, 
            IConfiguration config,
            IRestSharpTransient rest,
            IMasterListBuilder builder
            )
        {
            Guid = guid;
            _config = config;
            //_cache = ((RedisCache) cache).GetConnection().GetDatabase();
            _rest = rest;
            _builder = builder;

            if (_config["MagiRecoServer:Type"] == "master")
            {
                Log.Information("This is a master server, populating endpoints.");

                foreach (var item in _config.GetSection("MagiRecoNodes").Get<Dictionary<string, string>>())
                {
                    Log.Information($"{item.Key} -> {item.Value}");
                    foreach (var endpoint in item.Key.Split(","))
                    {
                        Endpoints.Add(endpoint, item.Value);
                    }
                }

                if (!Endpoints.ContainsKey("*"))
                {
                    Log.Fatal("Missing * endpoint in config! Please add one!");
                    throw new Exception("Missing * endpoint in config! Please add one!");
                }
            }
            Task.Run(Initialize);
        }
        public Guid Guid { get; set; }

        public Dictionary<string, string> Endpoints { get; set; } = new Dictionary<string, string>();

        public List<string> ModdedAssetLists = new List<string>()
        {
            "asset_main",
            "asset_prologue_main",
            "asset_prologue_voice",
            "asset_voice",
            "asset_fullvoice",
            "asset_char_list"
        };

        /// <summary>
        /// Update master lists
        /// </summary>
        /// <returns>Success</returns>
        public async Task<bool> UpdateMasterLists()
        {
            Log.Information("Updating master lists.");
            UpdateIsRunning = true;


            Log.Information("Fetching current asset version...");
            var masterJson = await _rest.GetMasterJson<MasterJsonConfig>(_config["MagiRecoServer:AssetVersion"]);
            if (masterJson != null)
            {
                Log.Information($"Asset version: {masterJson.version}");
                AssetsCurrentVersion = masterJson.version;
            }


            var workGamedataAssets = new Dictionary<string, List<GamedataAsset>>();
            foreach (var assetToMod in ModdedAssetLists)
            {
                Log.Information($"Updating master entry {assetToMod}.json...");
                var json = await _rest.GetMasterJson<List<GamedataAsset>>($"{assetToMod}.json.gz");

                if (json == null)
                {
                    Log.Fatal($"Failed to parse JSON for {assetToMod}, quitting.");
                    return false;
                }

                workGamedataAssets.Add(assetToMod, json);
            }

            Log.Information("Configuring master list...");

            var postProcessingGeneralScenario = new Dictionary<string, GamedataAsset>();
            var newGamedataAssets = new Dictionary<string, Dictionary<string, GamedataAsset>>();

            long counterReplace = 0;
            long counterSkip = 0;
            long counterNew = 0;
            long counterPost = 0;

            foreach (var assetType in workGamedataAssets)
            {
                var readyAssets = new Dictionary<string, GamedataAsset>();
                foreach (var asset in assetType.Value)
                {
                    // Replace with english assets as needed
                    if (EnglishMasterAssets.ContainsKey(asset.Path))
                    {
                        if (asset.Path.StartsWith("scenario/json/general/"))
                        {
                            postProcessingGeneralScenario.Add(asset.Path, asset);
                            counterPost++;
                        }
                        else if (EnglishMasterAssets[asset.Path].Md5 != asset.Md5)
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
                newGamedataAssets.Add(assetType.Key, readyAssets);
            }
            Log.Information($"Finished setting up. {counterReplace} replaced assets, {counterSkip} duplicate assets, {counterNew} new assets, {counterPost} assets for post processing.");

            // Add scripts
            foreach (var asset in EnglishMasterAssets)
            {
                if (asset.Key.StartsWith("scenario/json/adv/"))
                {
                    var split = asset.Key.Split("/").Last();
                    var scenario = split[0..^5]; // Trim .json from end
                    //Log.Debug($"Adding script {scenario}.");
                    newGamedataAssets.Add($"asset_scenario_{scenario}", new Dictionary<string, GamedataAsset>()
                    {
                        {scenario,asset.Value}
                    });
                }
            }

            // Post processing
            foreach (var asset in postProcessingGeneralScenario)
            {
                var builtJson = await _builder.BuildScenarioGeneralJson(asset.Value, EnglishMasterAssets);

                newGamedataAssets["asset_main"].Add(builtJson.Path, builtJson);
            }

            IsReady = false;
            GamedataAssets = newGamedataAssets;
            IsReady = true;
            UpdateIsRunning = false;
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

            var lists = await _builder.GenerateEnglishAssetList();

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
        public bool UpdateIsRunning { get; set; } = false;
        public long AssetsCurrentVersion { get; set; }
        public Dictionary<string, GamedataAsset> EnglishMasterAssets { get; set; }
        public Dictionary<string, Dictionary<string, GamedataAsset>> GamedataAssets { get; set; }

        /// <summary>
        /// Is a master data update needed?
        /// </summary>
        /// <returns>Boolean</returns>
        public async Task<bool> IsUpdateRequired()
        {
            var masterJson = await _rest.GetMasterJson<MasterJsonConfig>(_config["MagiRecoServer:AssetVersion"]);
            return masterJson != null && AssetsCurrentVersion < masterJson.version;
        }

        /// <summary>
        /// Run update on master data
        /// </summary>
        /// <returns>Successful</returns>
        public async Task<bool> RunUpdate()
        {
            if (await IsUpdateRequired())
            {
                if (!UpdateIsRunning)
                {
                    Log.Information($"Update to master data required. Current version: {AssetsCurrentVersion}.");
                    try
                    {
                        return await UpdateMasterLists();
                    }
                    finally
                    {
                        Log.Information($"Update finished. New version: {AssetsCurrentVersion}");
                    }
                }
                else
                {
                    Log.Information("Skipping update as an update is currently running.");
                }
            }
            else
            {
                Log.Information("No update needed.");
            }
            return false;
        }

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