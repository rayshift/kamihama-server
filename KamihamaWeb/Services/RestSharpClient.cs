using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Models;
using Microsoft.Extensions.Configuration;
using RestSharp;
using Serilog;
using MemoryStream = System.IO.MemoryStream;

namespace KamihamaWeb.Services
{
    public class RestSharpClient: IRestSharpTransient
    {
        public RestSharpClient(IConfiguration config) : this(Guid.NewGuid(), config)
        {
        }

        public RestSharpClient(Guid guid, IConfiguration config)
        {
            Guid = guid;
            _client = new RestClient();
            _config = config;

            if (!string.IsNullOrEmpty(_config["MagiRecoServer:AssetBase"]))
            {
                SetBaseUrl(_config["MagiRecoServer:AssetBase"]);
            }
            else
            {
                throw new Exception("MagiRecoServer:AssetBase config entry missing!");
            }

            if (!string.IsNullOrEmpty(_config["MagiRecoServer:Proxy"]))
            {
                this._client.Proxy = new WebProxy(_config["MagiRecoServer:Proxy"]);
            }
        }

        public Guid Guid { get; }
        private IConfiguration _config;

        protected void SetBaseUrl(string baseUrl)
        {
            _client.BaseUrl = new Uri(baseUrl);
            _client.UserAgent = "";
        }

        private IRestClient _client { get; set; }

        public async Task<T> GetMasterJson<T>(string masterJsonEndpoint)
        {
            var request = new RestRequest(masterJsonEndpoint + $"?{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", Method.GET);

            var result = await _client.ExecuteAsync<T>(request);
            if (result.IsSuccessful)
            {
                return result.Data;
            }
            else
            {
                Log.Warning($"Unable to get Master JSON config!\nError code: {result.ResponseStatus}\nError message: {result.ErrorMessage}\nContent: {result.Content}");
                return default(T);
            }
        }

        public async Task<string> GetAdditionalJson(string item)
        {
            var request = new RestRequest(item + $"?{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", Method.GET);
            var result = await _client.ExecuteAsync(request);
            return result.Content;
        }

        public async Task<Tuple<int, Stream>> FetchAsset(string item)
        {
            var request = new RestRequest("resource/" + item, Method.GET);
            
            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.StatusCode == HttpStatusCode.NotFound || response.ContentType == "text/html")
                {
                    return new Tuple<int, Stream>(404, null);
                }

                var stream = new MemoryStream(response.RawBytes);
                return new Tuple<int, Stream>(0, stream);
            }
            catch (WebException ex)
            {
                Log.Warning($"Web exception thrown: Status code {ex.Status}, {ex.ToString()}");
            }
            return new Tuple<int, Stream>(500, null);
        }
    }
}