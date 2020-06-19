using Newtonsoft.Json;

namespace TableauCri.Models
{
    public class TableauCredentialRequest
    {
        private class TableauRequestSite
        {
            [JsonProperty("contentUrl")]
            public string ContentUrl { get; set; }
        }

        private class TableauRequestCredential
        {
            [JsonProperty("name")]
            public string Username { get; set; }

            [JsonProperty("password")]
            public string Password { get; set; }

            [JsonProperty("site")]
            public TableauRequestSite Site { get; set; }
        }

        [JsonProperty("credentials")]
        private TableauRequestCredential Credentials { get; set; }

        public TableauCredentialRequest(string site, string username, string password)
        {
            Credentials = new TableauRequestCredential
            {
                Site = new TableauRequestSite { ContentUrl = site },
                Username = username,
                Password = password
            };
        }

        [JsonIgnore]
        public string Username
        {
            get => Credentials.Username;
            set => Credentials.Username = value;
        }

        [JsonIgnore]
        public string Password
        {
            get =>  Credentials?.Password;
            set => Credentials.Password = value;
        }

        [JsonIgnore]
        public string Site
        {
            get => Credentials?.Site?.ContentUrl;
            set => Credentials.Site.ContentUrl = value;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }

}
