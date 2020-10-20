using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamihamaWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
// ReSharper disable InconsistentNaming

namespace KamihamaWeb.Controllers
{
    [Route("api/v1/")]
    [ApiController]
    public class APIController : ControllerBase
    {
        [Route("endpoint")]
        public IActionResult GetEndpoint()
        {
            var response = new APIResult(200, "ok");
            response.Add("endpoint", "https://dev01-ma84hvas.kamihama.io/game");
            //response.Add("master", "http://local-mBha9xzf.kamihama.io:64571/game/");
            response.Add("version", 100);
            response.Add("max_threads", 40);
            return response;
        }

    }
}
