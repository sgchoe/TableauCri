using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Serilog;

namespace TableauCri.Services
{
    public interface ISmtpService : IDisposable
    {
        /// <summary>
        /// Send email with specified recipients and content
        /// </summary>
        /// <param name="to"></param>
        /// <param name="cc"></param>
        /// <param name="bcc"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="isBodyHtml"></param>
        /// <param name="attachmentPaths"></param>
        Task SendEmailAsync(
            string to,
            string cc,
            string bcc,
            string subject,
            string body,
            bool isBodyHtml = true,
            IEnumerable<string> attachmentPaths = null
        );

        /// <summary>
        /// Send email with specified content to configured admin
        /// </summary>
        /// <param name="to"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="isBodyHtml"></param>
        /// <param name="attachmentPaths"></param>
        Task SendAdminEmailAsync(
            string subject,
            string body,
            bool isBodyHtml = true,
            IEnumerable<string> attachmentPaths = null
        );
    }

    public class SmtpService : ISmtpService
    {
        private ILogger _logger;
        private IOptionsMonitor<SmtpSettings> _settingsMonitor;
        private SmtpClient _smtpClient;

        private bool _disposed = false;

        public SmtpService(IOptionsMonitor<SmtpSettings> settingsMonitor, ILogger logger)
        {
            _logger = logger;
            _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException("Missing mail settings");

            if (String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.Server) ||
                String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.From))
            {
                throw new ArgumentException("Server and from address must be specified in config");
            }

            _smtpClient = new SmtpClient();

            // ignore cert errors
            _smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// <see cref="ISmtpService.SendAdminEmailAsync"/>
        /// </summary>
        public async Task SendAdminEmailAsync(
            string subject,
            string body,
            bool isBodyHtml = true,
            IEnumerable<string> attachmentPaths = null
        )
        {
            _logger?.Debug($"SendAdminMailAsync: admin {_settingsMonitor.CurrentValue.Admin}");
            await SendEmailAsync(
                _settingsMonitor.CurrentValue.Admin, "", "", subject, body, isBodyHtml, attachmentPaths
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ISmtpService.SendEmailAsync"/>
        /// </summary>
        public async Task SendEmailAsync(
            string to,
            string cc,
            string bcc,
            string subject,
            string body,
            bool isBodyHtml = true,
            IEnumerable<string> attachmentPaths = null
        )
        {
            _logger?.Debug(
                "SendMailAsync: to {0}, server {1}, port {2} ",
                to,
                _settingsMonitor.CurrentValue.Server,
                _settingsMonitor.CurrentValue.Port
            );
            bool hasCredentials = !String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.Username) &&
                !String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.Password);
            var msg = GetMimeMessage(to, cc, bcc, subject, body, isBodyHtml, attachmentPaths);
            await _smtpClient.ConnectAsync(
                _settingsMonitor.CurrentValue.Server,
                _settingsMonitor.CurrentValue.Port == default ? 25 : _settingsMonitor.CurrentValue.Port,
                SecureSocketOptions.StartTlsWhenAvailable
            ).ConfigureAwait(false);

            if (hasCredentials)
            {
                await _smtpClient.AuthenticateAsync(
                    _settingsMonitor.CurrentValue.Username, _settingsMonitor.CurrentValue.Password
                ).ConfigureAwait(false);
            }
            await _smtpClient.SendAsync(msg).ConfigureAwait(false);
            await _smtpClient.DisconnectAsync(true).ConfigureAwait(false);
        }

        private MimeMessage GetMimeMessage(
            string to,
            string cc,
            string bcc,
            string subject,
            string body,
            bool isBodyHtml = true,
            IEnumerable<string> attachmentPaths = null
        )
        {
            var msg = new MimeMessage { Subject = subject };

            msg.From.Add(new MailboxAddress(_settingsMonitor.CurrentValue.From));

            // parse recipients if specified as comma or semi-colon separated strings
            if (String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.DevTest))
            {
                var addressLists = new List<KeyValuePair<InternetAddressList, string>>()
                {
                    new KeyValuePair<InternetAddressList, string>(msg.To, to),
                    new KeyValuePair<InternetAddressList, string>(msg.Cc, cc),
                    new KeyValuePair<InternetAddressList, string>(msg.Bcc, bcc)
                };
                foreach (var kvp in addressLists)
                {
                    (kvp.Value ?? "")
                        .Split(',', ';')
                        .Select(a => a.Trim())
                        .Where(a => !String.IsNullOrWhiteSpace(a))
                        .ToList()
                        .ForEach(a => kvp.Key.Add(new MailboxAddress(a)));
                }
            }
            else
            {
                // dev/test mode, funnel all emails to DevTest address, note actual intended recipients in body
                _logger?.Debug($"SMTP dev/test mode: all emails will go to {_settingsMonitor.CurrentValue.DevTest}");
                msg.To.Clear();
                msg.Cc.Clear();
                msg.Bcc.Clear();
                msg.To.Add(new MailboxAddress(_settingsMonitor.CurrentValue.DevTest));
                body = String.Format(
                    "To: {1}{0}Cc: {2}{0}Bcc: {3}{0}{0}{4}",
                    "<br />" + Environment.NewLine,
                    to,
                    cc,
                    bcc,
                    body
                );
            }
            
            if (!(msg.To.Any() || msg.Cc.Any() || msg.Bcc.Any()))
            {
                _logger?.Error("Error sending mail, no recipients (to/cc/bcc) specified");
                throw new ArgumentException("Error sending mail, no recipients (to/cc/bcc) specified");
            }

            var bodyBuilder = new BodyBuilder();
            if (isBodyHtml)
            {
                bodyBuilder.HtmlBody = body;
            }
            bodyBuilder.TextBody = body;

            var bodyMultiPart = new Multipart("mixed");
            bodyMultiPart.Add(new TextPart("plain") { Text = body });

            foreach (var attachmentPath in attachmentPaths ?? Enumerable.Empty<string>())
            {
                try
                {
                    bodyBuilder.Attachments.Add(attachmentPath);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error adding mail attachment {attachmentPath}: {ex.ToString()} ");
                }

            }

            msg.Body = bodyBuilder.ToMessageBody();

            return msg;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _smtpClient.Dispose();
            }

            _disposed = true;
        }
    }
}
