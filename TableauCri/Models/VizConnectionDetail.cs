using Newtonsoft.Json;
using TableauCri.Models.Configuration;

namespace TableauCri.Models
{
    public class VizConnectionDetail
    {
        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        [JsonProperty("serverPort")]
        public string ServerPort { get; set; }

        [JsonProperty("databaseUsername")]
        public string Username { get; set; }

        [JsonProperty("databasePassword")]
        public string Password { get; set; }

        [JsonProperty("hasEmbeddedPassword")]
        public bool HasEmbeddedPassword { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
