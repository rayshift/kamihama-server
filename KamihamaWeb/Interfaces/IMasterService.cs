using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamihamaWeb.Models;

namespace KamihamaWeb.Interfaces
{
    public interface IMasterService
    {
        Guid Guid { get; }
    }

    public interface IMasterSingleton : IMasterService
    {
        public Task<bool> UpdateMasterLists();
        public bool IsReady { get; set; }
        public long AssetsCurrentVersion { get; set; }
        public Dictionary<string, GamedataAsset> EnglishMasterAssets { get; set; }
        public Dictionary<string, Dictionary<string, GamedataAsset>> GamedataAssets { get; set; }
        public Task<string> ProvideJson(string which);
    }
}