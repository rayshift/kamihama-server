using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Serilog;

// ReSharper disable InconsistentNaming

namespace KamihamaWeb.Controllers
{
    [Route("api/v1/")]
    [ApiController]
    public class APIController : ControllerBase
    {
        public APIController(IConfiguration config, IMasterSingleton master)
        {
            _config = config;
            _master = master;
        }

        private IConfiguration _config { get; set; }
        private IMasterSingleton _master { get; set; }
        [Route("endpoint")]
        public IActionResult GetEndpoint()
        {
            var response = new APIResult(200, "ok");
            if (_master.Endpoints.Count == 0) // Endpoint server
            {
                response.Add("endpoint", _config["MagiRecoServer:Endpoint"]);
            }
            else // Master server
            {
                if (Request.Headers.ContainsKey("CF-IPCountry"))
                {
                    if (_master.Endpoints.ContainsKey(Request.Headers["CF-IPCountry"]))
                    {
                        response.Add("endpoint", _master.Endpoints[Request.Headers["CF-IPCountry"]]);
                    }
                    else
                    {
                        response.Add("endpoint", _master.Endpoints["*"]);
                    }
                }
                else  {
                    Log.Warning("No Cloudflare IP header found.");
                    response.Add("endpoint", _master.Endpoints["*"]);
                }
            }

            response.Add("version", int.Parse(_config["MagiRecoServer:Version"]));
            response.Add("max_threads", int.Parse(_config["MagiRecoServer:MaxThreads"]));
            return response;
        }

    }
}
