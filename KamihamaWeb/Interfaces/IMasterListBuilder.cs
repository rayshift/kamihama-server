using System.Collections.Generic;
using System.Threading.Tasks;
using KamihamaWeb.Models;

namespace KamihamaWeb.Interfaces
{
    public interface IMasterListBuilder
    {
        Task<Dictionary<string, GamedataAsset>> GenerateEnglishAssetList();

        public GamedataAsset GetFileInformation(string file);

        public Task<GamedataAsset> BuildScenarioGeneralJson(GamedataAsset generalAsset,
            Dictionary<string, GamedataAsset> englishAssets);
    }
}