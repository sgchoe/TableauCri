using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauGroup
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("domain")]
        public TableauDomain Domain { get; set; }

        public string SiteId { get; set; }
        public string SiteName { get; set; }
        public string ApiVersion { get; set; }

        public string Url => $"api/{ApiVersion}/sites/{SiteId}/groups/{Id}";

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Name;
            return json.ToString();
        }
    }
}
