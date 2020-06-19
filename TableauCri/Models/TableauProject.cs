using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauProject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("contentPermissions")]
        public string ContentPermissions { get; set; }

        [JsonProperty("parentProjectId")]
        public string ParentProjectId { get; set; }

        public string SiteId { get; set; }
        public string SiteName { get; set; }
        public string ApiVersion { get; set; }

        public string Url => $"api/{ApiVersion}/sites/{SiteId}/projects/{Id}";

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Name;
            json["description"] = Description;
            json["contentPermissions"] = ContentPermissions;
            json["parentProjectId"] = ParentProjectId;
            return json.ToString();
        }
    }
}
