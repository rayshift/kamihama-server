using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamihamaWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

// ReSharper disable InconsistentNaming

namespace KamihamaWeb.Controllers
{
    [Route("api/v1/")]
    [ApiController]
    public class APIController : ControllerBase
    {
        public APIController(IConfiguration config)
        {
            _config = config;
        }

        private IConfiguration _config { get; set; }
        [Route("endpoint")]
        public IActionResult GetEndpoint()
        {
            var response = new APIResult(200, "ok");
            response.Add("endpoint", _config["MagiRecoServer:Endpoint"]);
            response.Add("version", int.Parse(_config["MagiRecoServer:Version"]));
            response.Add("max_threads", int.Parse(_config["MagiRecoServer:MaxThreads"]));
            return response;
        }

    }
}
