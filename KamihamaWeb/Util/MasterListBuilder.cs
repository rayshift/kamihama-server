using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KamihamaWeb.Models;
using Newtonsoft.Json;
using Serilog;

namespace KamihamaWeb.Util
{
    public class MasterListBuilder
    {
        private Regex multiPartRegex = new Regex(@"\.a[a-z]{2}"); // A bit crude
        public MasterListBuilder()
        {
            BasePathLength =
                @"MagiRecoStatic/magica/resource/download/asset/master/resource/".Length;
        }

        private int BasePathLength { get; }

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




            if (Directory.Exists("MagiRecoStatic/"))
            {
                List<string> files = Directory.GetFiles("MagiRecoStatic/magica/resource/download/asset/master",
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
                    if (multiPartRegex.IsMatch(asset.Value.Path.Substring(asset.Value.Path.Length - 4)))
                    {
                        Log.Debug($"Removing duplicate asset {asset.Key}");
                        englishAssets.Remove(asset.Value.Path);
                    }
                    else if (asset.Key.StartsWith("image_native/mini/") || asset.Key.StartsWith("image_native/live2d/"))
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
    }
}