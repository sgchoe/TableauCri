using TableauCri.Models.Configuration;

namespace TableauCri.Models.Configuration
{
    public class TableauCriSettings
    {
        public SmtpSettings SmtpSettings { get; set; }
        public TableauApiSettings TableauApiSettings { get; set; }
        public bool DryRun { get; set; }
    }
}
