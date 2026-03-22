using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Capstone.Api.Services;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 465;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Tenurix";
    public bool EnableSsl { get; set; } = true;
}

public sealed class EmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _settings = new SmtpSettings();
        config.GetSection("Smtp").Bind(_settings);
        _logger = logger;

        // Log config on startup (mask password)
        _logger.LogInformation(
            "EmailService initialized — Host={Host}, Port={Port}, From={From}, SSL={Ssl}",
            _settings.Host, _settings.Port, _settings.FromEmail, _settings.EnableSsl);
    }

    /// <summary>
    /// Send an email in the background. Never blocks the API response.
    /// </summary>
    public void SendInBackground(string toEmail, string subject, string htmlBody)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(toEmail, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EMAIL FAILED — To={To}, Subject='{Subject}', Host={Host}:{Port}",
                    toEmail, subject, _settings.Host, _settings.Port);
            }
        });
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Username))
        {
            _logger.LogWarning("SMTP is not configured. Skipping email to {To}", toEmail);
            return;
        }

        // Build the MIME message
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.ReplyTo.Add(new MailboxAddress("Tenurix Support", "support@tenurix.net"));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        // Send via MailKit (supports implicit SSL on port 465)
        using var client = new SmtpClient();

        // Determine SSL mode based on port
        var sslMode = _settings.Port == 465
            ? SecureSocketOptions.SslOnConnect    // Port 465 = implicit SSL
            : SecureSocketOptions.StartTls;        // Port 587 = STARTTLS

        _logger.LogInformation("Connecting to {Host}:{Port} with {Ssl}...", _settings.Host, _settings.Port, sslMode);

        await client.ConnectAsync(_settings.Host, _settings.Port, sslMode);

        _logger.LogInformation("Authenticating as {Username}...", _settings.Username);

        await client.AuthenticateAsync(_settings.Username, _settings.Password);

        _logger.LogInformation("Sending email to {To}: {Subject}", toEmail, subject);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("EMAIL SENT — To={To}, Subject='{Subject}'", toEmail, subject);
    }
}
