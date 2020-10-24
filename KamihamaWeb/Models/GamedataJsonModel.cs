using System.Collections.Generic;
using Newtonsoft.Json;

namespace KamihamaWeb.Models
{

    public class GamedataFileList
    {

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class GamedataAsset
    {

        [JsonProperty("file_list")]
        public IList<GamedataFileList> FileList { get; set; }

        [JsonProperty("md5")]
        public string Md5 { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonIgnore]
        public AssetDownloadType Type { get; set; } = AssetDownloadType.JPServer;

        [JsonIgnore] 
        public AssetSourceType AssetSource { get; set; } = AssetSourceType.Remote;
    }

    public class MasterJsonConfig
    {
        [JsonProperty("asset_optimize")]
        public int asset_optimize { get; set; }

        [JsonProperty("version")]
        public long version { get; set; }
    }

    public enum AssetDownloadType
    {
        ENServer,
        JPServer
    }

    public enum AssetSourceType
    {
        Local,
        Remote,
        GeneralScript
    }

}