using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static TableauCri.Services.TableauPermissionService;

namespace TableauCri.Models
{
    public class TableauGranteeCapabilities
    {
        [JsonProperty("user")]
        public TableauUser User { get; set; }

        [JsonProperty("group")]
        public TableauGroup Group { get; set; }

        [JsonProperty("capabilities")]
        public TableauCapabilities Capabilities { get; set; }

        public GranteeType GranteeType =>
            User != null
                ? GranteeType.User
                : (Group != null ? GranteeType.Group : throw new Exception("Grantee not specified"));

        public string GranteeId => User?.Id ?? Group?.Id;
        
        public string ToRequestString()
        {
            var json = new JObject();

            if (User != null)
            {
                json["user"] = new JObject();
                json["user"]["id"] = User.Id;
            }
            else if (Group != null)
            {
                json["group"] = new JObject();
                json["group"]["id"] = Group.Id;
            }
            else
            {
                throw new Exception("User or group must be specified for GranteeCapabilities");
            }

            json["capabilities"] = JObject.Parse(JsonConvert.SerializeObject(Capabilities));
            
            if (json["capabilities"]["capability"] == null || !json["capabilities"]["capability"].Any())
            {
                throw new Exception("One or more capabilities must be specified for GranteeCapabilities");
            }

            return json.ToString();
        }
    }
}
