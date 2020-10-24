using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KamihamaWeb.Models
{
    public class ScenarioGeneral
    {

            [JsonProperty("story")]
            public Dictionary<string, dynamic> story { get; set; }

            [JsonProperty("version")]
            public int version { get; set; }
        
    }
}