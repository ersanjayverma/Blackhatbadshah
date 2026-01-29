namespace backend.Services;

public interface IEmailService
{
    Task<EmailSendResult> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendEmailAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default);
    bool IsConfigured();
}

public class EmailMessage
{
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = new();
}

public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }

    public static EmailSendResult Succeeded(string? messageId = null) => new() { Success = true, MessageId = messageId };
    public static EmailSendResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
