using Newtonsoft.Json;
using TableauCri.Models.Configuration;

namespace TableauCri.Models
{
    public class VizDatasource
    {
        [JsonProperty("name")]
        public string Name{ get; set; }

        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; }

        [JsonProperty("connectionDetails")]
        public VizConnectionDetail VizConnectionDetail { get; set; }
    }
}
