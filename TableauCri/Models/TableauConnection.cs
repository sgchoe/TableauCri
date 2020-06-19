using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauConnection
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("serverAddress")]
        public string ServerAddress { get; set; }

        [JsonProperty("serverPort")]
        public string ServerPort { get; set; }

        [JsonProperty("userName")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("embedPassword")]
        public bool EmbedPassword { get; set; }

        [JsonProperty("connectionCredentials")]
        public TableauConnectionCredentials ConnectionCredentials { get; set; }

        [JsonProperty("datasource")]
        public TableauDatasource Datasource { get; set; }

        public string ToRequestString()
        {
            var json = new JObject();
            json["serverAddress"] = ServerAddress;
            json["serverPort"] = ServerPort;
            json["connectionCredentials"] = ConnectionCredentials != null
                ? JToken.Parse(ConnectionCredentials?.ToRequestString())
                : null;
            if (Datasource != null)
            {
                json["datasource"] = new JObject();
                json["datasource"]["id"] = Datasource.Id;
                json["datasource"]["name"] = Datasource.Name;
            }
            return json.ToString();
        }
    }
}
