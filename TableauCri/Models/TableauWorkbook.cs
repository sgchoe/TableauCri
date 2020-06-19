using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauWorkbook
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("contentUrl")]
        public string ContentUrl { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("project")]
        public TableauProject Project { get; set; }

        [JsonProperty("owner")]
        public TableauOwner Owner { get; set; }
        
        public TableauConnection[] Connections{ get; set; }
        
        public string SiteId { get; set; }
        public string SiteName { get; set; }
        public string ApiVersion { get; set; }
        public string SavePath { get; set; }
        
        public string Url => $"api/{ApiVersion}/sites/{SiteId}/workbooks/{Id}";

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Name;
            json["project"] = new JObject();
            json["project"]["id"] = Project?.Id;
            var connectionsJson = new JArray();
            foreach (var connection in Connections)
            {
                connectionsJson.Add(JToken.Parse(connection.ToRequestString()));
            }
            json["connections"] = new JObject();
            json["connections"]["connection"] = connectionsJson;
            return json.ToString();
        }
    }
}
