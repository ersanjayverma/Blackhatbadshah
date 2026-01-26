using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using backend.Configuration;

namespace backend.Services;

public class EmailService : IEmailService
{
    private readonly EmailConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailConfiguration> config, ILogger<EmailService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public bool IsConfigured()
    {
        return _config.Enabled
            && !string.IsNullOrEmpty(_config.Host)
            && !string.IsNullOrEmpty(_config.Username)
            && !string.IsNullOrEmpty(_config.Password);
    }

    public Task<EmailSendResult> SendEmailAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        return SendEmailAsync(new EmailMessage
        {
            ToAddress = toAddress,
            Subject = subject,
            HtmlBody = htmlBody
        }, cancellationToken);
    }

    public async Task<EmailSendResult> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Email service is not configured. Email not sent to {ToAddress}", message.ToAddress);
            return EmailSendResult.Failed("Email service is not configured");
        }

        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetries)
        {
            attempt++;
            try
            {
                using var smtpClient = new SmtpClient(_config.Host, _config.Port)
                {
                    EnableSsl = _config.UseSsl,
                    Credentials = new NetworkCredential(_config.Username, _config.Password),
                    Timeout = _config.TimeoutMs
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_config.FromAddress, _config.FromName),
                    Subject = message.Subject,
                    Body = message.HtmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(message.ToAddress, message.ToName));

                if (!string.IsNullOrEmpty(message.PlainTextBody))
                {
                    var plainView = AlternateView.CreateAlternateViewFromString(message.PlainTextBody, null, "text/plain");
                    var htmlView = AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html");
                    mailMessage.AlternateViews.Add(plainView);
                    mailMessage.AlternateViews.Add(htmlView);
                }

                foreach (var attachment in message.Attachments)
                {
                    var stream = new MemoryStream(attachment.Content);
                    mailMessage.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
                }

                await smtpClient.SendMailAsync(mailMessage, cancellationToken);

                _logger.LogInformation("Email sent successfully to {ToAddress}, Subject: {Subject}", message.ToAddress, message.Subject);
                return EmailSendResult.Succeeded(Guid.NewGuid().ToString());
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Email send attempt {Attempt} failed to {ToAddress}", attempt, message.ToAddress);

                if (attempt < _config.MaxRetries)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }
        }

        _logger.LogError(lastException, "Failed to send email to {ToAddress} after {Attempts} attempts", message.ToAddress, attempt);
        return EmailSendResult.Failed(lastException?.Message ?? "Unknown error");
    }
}
