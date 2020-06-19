using TableauCri.Models.Configuration;

namespace TableauCri.Models.Configuration
{
    public class TableauApiSettings
    {
        public string BaseUrl { get; set; }
        public string ApiVersion { get; set; }
        public string SiteContentUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int TimeoutSeconds { get; set; }
    }
}
