using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauParent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
