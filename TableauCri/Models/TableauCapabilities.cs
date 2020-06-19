using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauCapabilities
    {
        [JsonProperty("capability")]
        public TableauCapability[] Capability { get; set; }
    }
}
