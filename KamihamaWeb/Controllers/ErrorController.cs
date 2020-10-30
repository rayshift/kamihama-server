using KamihamaWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KamihamaWeb.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        [AllowAnonymous]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var path = feature?.OriginalPath;
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith("/api/v1"))
                {
                    switch (statusCode)
                    {
                        case 404:
                            return new APIResult(404, "not found");
                        case 503:
                            return new APIResult(503, "service temporarily unavailable");
                        default:
                            return new APIResult(500, "an error has occurred");
                    }

                }
            }
            switch (statusCode)
            {
                case 404:
                    ViewBag.Title = "404 Not Found";
                    ViewData["ErrorMessage"] = "The resource you requested could not be found.";
                    break;
                case 503:
                    ViewBag.Title = "503 Service Unavailable";
                    ViewData["ErrorMessage"] = "The requested resource is currently unavailable.";
                    break;
                default:
                    ViewBag.Title = "Unknown Error";
                    ViewData["ErrorMessage"] = "An unknown error occurred.";
                    break;
            }

            return View("HttpError");
        }
    }
}
}