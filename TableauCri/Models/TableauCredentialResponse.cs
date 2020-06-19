using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauCredentialResponse
    {
        private class TableauResponseSite
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("contentUrl")]
            public string ContentUrl { get; set; }
        }

        private class TableauResponseUser
        {
            [JsonProperty("id")]
            public string Id { get; set; }
        }

        private class TableauResponseCredential
        {
            [JsonProperty("site")]
            public TableauResponseSite Site { get; set; }

            [JsonProperty("user")]
            public TableauResponseUser User { get; set; }

            [JsonProperty("token")]
            public string Token { get; set; }
        }

        [JsonProperty("credentials")]
        private TableauResponseCredential Credentials { get; set; }

        [JsonIgnore]
        public string UserId
        {
            get => Credentials?.User?.Id;
            set => Credentials.User.Id = value;
        }

        [JsonIgnore]
        public string Token
        {
            get => Credentials?.Token;
            set => Credentials.Token = value;
        }

        [JsonIgnore]
        public string SiteId
        {
            get => Credentials?.Site?.Id;
            set => Credentials.Site.Id = value;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }

}
