namespace backend.Configuration;

public class EmailConfiguration
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@blackhatbadshah.com";
    public string FromName { get; set; } = "BlackHatBadshah";
    public int TimeoutMs { get; set; } = 30000;
    public int MaxRetries { get; set; } = 3;
}
