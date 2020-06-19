using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauConnectionCredentials
    {
        [JsonProperty("name")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("embed")]
        public bool? Embed { get; set; }

        [JsonProperty("oAuth")]
        public bool? Oauth { get; set; }

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Username;
            json["password"] = Password;
            json["embed"] = Embed ?? false;
            return json.ToString();
        }
    }
}
