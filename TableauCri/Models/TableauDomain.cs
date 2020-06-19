using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauDomain
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
