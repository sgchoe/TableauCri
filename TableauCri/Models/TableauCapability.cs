using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauCapability
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }
    }
}
