using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace KamihamaWeb.Models
{
    public class APIResult : IActionResult
    {
        public int? StatusCode { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> ResponseObjects = new Dictionary<string, object>();
        public APIResult(int statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }

        /// <summary>
        /// Add to the response dictionary
        /// </summary>
        /// <param name="key">String key</param>
        /// <param name="value">Value to be added</param>
        public void Add(string key, object value)
        {
            ResponseObjects.Add(key, value);
        }

        public override string ToString()
        {
            var finalObject = new Dictionary<string, object>
            {
                { "status", StatusCode ?? 200 },
                { "response", ResponseObjects },
                { "message", Message }
            };
            return JsonConvert.SerializeObject(finalObject);
        }

        /// <summary>
        /// Gets or set the content representing the body of the response.
        /// </summary>
        public string Content => ToString();

        /// <summary>
        /// Gets or sets the Content-Type header for the response.
        /// </summary>
        public const string ContentType = "application/json";

        public Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }


            var result = new ContentResult
            {
                StatusCode = StatusCode,
                Content = Content,
                ContentType = ContentType
            };

            return result.ExecuteResultAsync(context);
        }
    }
    
}