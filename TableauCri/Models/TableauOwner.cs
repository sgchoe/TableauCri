using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauOwner
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
