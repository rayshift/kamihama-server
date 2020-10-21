using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Models;
using Marvin.Cache.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace KamihamaWeb.Controllers
{
    [Route("game/magica/resource/download/asset/master/")]
    [ApiController]
    public class ResourceController : ControllerBase
    {
        private readonly IMasterSingleton _masterService;
        private IDiskCacheSingleton _diskCache;
        public ResourceController(IMasterSingleton masterService, IDiskCacheSingleton diskCache)
        {
            _masterService = masterService;
            _diskCache = diskCache;
        }
        /*[Route("{*url}")]
        public async Task<IActionResult> GetResource()
        {
            var x = Request.Path.Value;
            return NotFound();
        }*/
        [HttpGet]
        [Route("{url}.json.gz")]
        [HttpCacheValidation(NoCache = true)]
        public async Task<IActionResult> GetMasterJson(string url)
        {
            if (!_masterService.IsReady)
            {
                return new APIResult(503, "Master data still loading, try again later.");
            }

            /*WebClient wc = new WebClient();
            var queryString = Request.QueryString;
            var remoteItem =
                $"https://android.magi-reco.com/magica/resource/download/asset/master/{url}.json.gz{queryString.Value}";
            MemoryStream stream = new MemoryStream(wc.DownloadData(remoteItem));

            stream.Seek(0, SeekOrigin.Begin);

            if (!string.IsNullOrEmpty(wc.ResponseHeaders["Content-Encoding"]))
            {
                HttpContext.Response.Headers.Add("Content-Encoding", wc.ResponseHeaders["Content-Encoding"]);
            }

            return new FileStreamResult(stream, new MediaTypeHeaderValue("application/json"));*/

            var stream = new MemoryStream();
            await stream.WriteAsync(Encoding.UTF8.GetBytes(await _masterService.ProvideJson(url)));
            stream.Seek(0, SeekOrigin.Begin);
            return new FileStreamResult(stream, new MediaTypeHeaderValue("application/json"));
           

            //return Redirect("https://android.magi-reco.com/" + url);
        }


        [Route("resource/{*url}")]
        [HttpGet]
        public async Task<IActionResult> GetAsset(string url)
        {
            /*WebClient wc = new WebClient();
            MemoryStream stream = new MemoryStream(wc.DownloadData("https://android.magi-reco.com/" + url));

            stream.Seek(0, SeekOrigin.Begin);
            return new FileStreamResult(stream, new MediaTypeHeaderValue(wc.ResponseHeaders["Content-Type"]));

            //return Redirect("https://android.magi-reco.com/" + url);*/

            if (!_masterService.IsReady)
            {
                return new APIResult(503, "Master data still loading, try again later.");
            }

            var qs = Request.QueryString;
            if (qs.HasValue && qs.Value.Length == 33)
            {
                var md5 = qs.Value.Substring(1);

                if (_masterService.EnglishMasterAssets.ContainsKey(url))
                {
                    if (_masterService.EnglishMasterAssets[url].Md5 == md5)
                    {
                        var diskUrl = Path.Combine("MagiRecoStatic", "magica/resource/download/asset/master/resource/", url);
                        if (System.IO.File.Exists(diskUrl))
                        {
                            var diskItem = System.IO.File.Open(diskUrl, FileMode.Open, FileAccess.Read);
                            return new FileStreamResult(diskItem, "binary/octet-stream");
                        }
                        else
                        {
                            return new APIResult(404, "not found (disk error)");
                        }
                    }
                    else
                    {
                        Log.Debug($"Md5 mismatch on {url}, found {md5}, expected {_masterService.EnglishMasterAssets[url].Md5}.");
                    }
                }
                var asset = await _diskCache.Get(url, md5);

                if (asset.Item1 == 404)
                {
                    return new APIResult(404, "asset not found");
                }
                else if (asset.Item1 == 500)
                {
                    return new APIResult(503, "internal error fetching asset");
                }
                else
                {
                    return new FileStreamResult(asset.Item2, "binary/octet-stream");
                }
            }
            else
            {
                return new APIResult(403, "forbidden");
            }
        }
    }
}