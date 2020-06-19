using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauPermission
    {
        [JsonProperty("project")]
        public TableauProject Project { get; set; }

        [JsonProperty("workbook")]
        public TableauWorkbook Workbook { get; set; }

        [JsonProperty("datasource")]
        public TableauDatasource Datasource { get; set; }


        [JsonProperty("granteeCapabilities")]
        public TableauGranteeCapabilities[] GranteeCapabilities { get; set; }

        public string ToRequestString()
        {
            var json = new JObject();

            if (Datasource != null)
            {
                json["datasource"] = new JObject();
                json["datasource"]["id"] = Datasource.Id;
            }
            else if (Workbook != null)
            {
                json["workbook"] = new JObject();
                json["workbook"]["id"] = Datasource.Id;
            }
            
            var granteeCapabilitiesJson = new JArray();
            foreach (var granteeCapabilities in GranteeCapabilities)
            {
                granteeCapabilitiesJson.Add(JObject.Parse(granteeCapabilities.ToRequestString()));
            }
            if (!granteeCapabilitiesJson.Any())
            {
                throw new Exception("Grantee capabilities must be specified for permission");
            }

            json["granteeCapabilities"] = granteeCapabilitiesJson;

            return json.ToString();
        }
    }
}
