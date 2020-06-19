using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauPagination
    {
        [JsonProperty("pageNumber")]
        public int PageNumber { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
        
        [JsonProperty("totalAvailable")]
        public int TotalAvailable { get; set; }
    }
}
