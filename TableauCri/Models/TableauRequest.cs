using System.Collections.Generic;
using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauRequest
    {
        public TableauRequest()
        {
            Parameters = new Dictionary<string, string>();
        }

        public Dictionary<string, string> Parameters { get; set; }

        public string ToJsonString()
        {
            var request = new Dictionary<string, Dictionary<string, string>>();
            return JsonConvert.SerializeObject(request);
        }

        public override string ToString() => ToJsonString();
    }

}
