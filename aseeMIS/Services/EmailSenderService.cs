using System.Diagnostics;
using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using aseeMIS.Services.Interfaces;


namespace aseeMIS.Services
{
    public class EmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _configuration;

        public EmailSenderService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Recipient email is required.", nameof(email));

            var smtpSection = _configuration.GetSection("Smtp");
            if (!smtpSection.Exists())
                throw new InvalidOperationException("Missing Smtp configuration section.");

            // Required settings
            var smtpHost = smtpSection["Host"] ?? throw new InvalidOperationException("SMTP host is missing.");
            var smtpUsername = smtpSection["Username"] ?? throw new InvalidOperationException("SMTP username is missing.");
            var smtpPassword = smtpSection["Password"] ?? throw new InvalidOperationException("SMTP password is missing.");

            if (!int.TryParse(smtpSection["Port"], out var port))
                throw new InvalidOperationException("Invalid SMTP port configuration.");

            if (!bool.TryParse(smtpSection["EnableSsl"], out var enableSsl))
                throw new InvalidOperationException("Invalid SMTP SSL configuration.");

            // Optional settings
            var fromEmail = smtpSection["FromEmail"];   // default to Username if not set
            var fromName = smtpSection["FromName"];    // optional display name
            var replyTo = smtpSection["ReplyTo"];     // optional
            var timeoutStr = smtpSection["TimeoutMs"];   // optional
            var timeoutMs = 0;
            int.TryParse(timeoutStr, out timeoutMs);
            if (timeoutMs <= 0) timeoutMs = 30000; // 30s default

            // Prepare sender
            var senderEmail = string.IsNullOrWhiteSpace(fromEmail) ? smtpUsername : fromEmail;
            var fromAddress = string.IsNullOrWhiteSpace(fromName)
                ? new MailAddress(senderEmail)
                : new MailAddress(senderEmail, fromName, Encoding.UTF8);

            // Build client
            using var smtpClient = new SmtpClient(smtpHost)
            {
                Port = port,
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = timeoutMs
            };

            // Build message
            using var message = new MailMessage
            {
                From = fromAddress,
                Subject = subject ?? string.Empty,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                HeadersEncoding = Encoding.UTF8,
                IsBodyHtml = true // we also add a text alternative below
            };

            // Support multiple recipients separated by comma/semicolon/whitespace
            var recipients = email
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
                throw new ArgumentException("No valid recipient emails were provided.", nameof(email));

            foreach (var to in recipients)
                message.To.Add(new MailAddress(to));

            if (!string.IsNullOrWhiteSpace(replyTo))
                message.ReplyToList.Add(new MailAddress(replyTo));

            // HTML body + plain text alternative
            var htmlBody = htmlMessage ?? string.Empty;
            var textBody = HtmlToPlainText(htmlBody);

            var altHtml = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
            var altText = AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain");
            message.AlternateViews.Add(altText);
            message.AlternateViews.Add(altHtml);

            // Also set Body for clients that ignore AlternateViews
            message.Body = htmlBody;

            // A little helpful header
            message.Headers.Add("X-Mailer", "aseeMIS EmailSenderService");

            try
            {
                Debug.WriteLine($"[Email] Sending to: {string.Join(", ", recipients)} | Subject: {subject}");
                await smtpClient.SendMailAsync(message);
                Debug.WriteLine("[Email] Sent successfully.");
            }
            catch (SmtpException smtpEx)
            {
                Debug.WriteLine($"[Email][SMTP ERROR] {smtpEx.StatusCode}: {smtpEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Email][ERROR] {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        private static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // Remove scripts/styles
            html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // Replace <br> and </p> with newlines
            html = Regex.Replace(html, @"<(br|/p)\s*/?>", "\n", RegexOptions.IgnoreCase);
            // Strip all tags
            var text = Regex.Replace(html, "<.*?>", string.Empty);
            // Decode HTML entities
            text = WebUtility.HtmlDecode(text);
            // Normalize whitespace
            text = Regex.Replace(text, @"\s+\n", "\n").Trim();
            return text;
        }
    }
}
