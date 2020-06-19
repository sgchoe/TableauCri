namespace TableauCri.Models.Configuration
{
    public class SmtpSettings
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string From { get; set; }
        public string Admin { get; set; }
        public string DevTest { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
