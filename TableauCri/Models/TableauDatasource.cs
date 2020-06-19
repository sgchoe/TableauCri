using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauDatasource
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("contentUrl")]
        public string ContentUrl { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("project")]
        public TableauProject Project { get; set; }

        [JsonProperty("owner")]
        public TableauOwner Owner { get; set; }

        public TableauConnection[] Connections { get; set; }
        public TableauConnectionCredentials ConnectionCredentials { get; set; }

        public string SiteId { get; set; }
        public string SiteName { get; set; }
        public string ApiVersion { get; set; }

        public string Url => $"api/{ApiVersion}/sites/{SiteId}/datasources/{Id}";

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Name;
            json["connectionCredentials"] = JToken.Parse(JsonConvert.SerializeObject(ConnectionCredentials));
            json["project"] = new JObject();
            json["project"]["id"] = Project?.Id;
            return json.ToString();
        }
    }
}
