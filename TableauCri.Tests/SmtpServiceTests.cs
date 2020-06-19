using NUnit.Framework;
using TableauCri.Services;
using System;

namespace TableauCri.Tests
{
    [Ignore("SmtpServiceTests")]
    public class SmtpServiceTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Ignore("SendEmailAsyncTest")]
        [Test]
        public void SendEmailAsyncTest()
        {
            var svc = GetService();

            var to = "test@example.com";
            var cc = "";
            var bcc = "";
            var subject = $"test subject: {DateTime.Now}";
            var body = $"test body: {DateTime.Now}";

            svc.SendEmailAsync(to, cc, bcc, subject, body).Wait();
        }

        private ISmtpService GetService()
        {
            return ServiceFactory.Instance.GetService<ISmtpService>();
        }
    }
}
