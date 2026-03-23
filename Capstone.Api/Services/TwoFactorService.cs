using System.Collections.Concurrent;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Capstone.Api.Services;

public sealed class TwoFactorService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<TwoFactorService> _logger;

    // In-memory store: key = email (lowercase), value = (code, expiry)
    private static readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresUtc)> _codes = new();

    public TwoFactorService(IConfiguration config, ILogger<TwoFactorService> logger)
    {
        _smtp = new SmtpSettings();
        config.GetSection("Smtp2FA").Bind(_smtp);

        // Fallback to main Smtp if Smtp2FA not configured
        if (string.IsNullOrWhiteSpace(_smtp.Host))
            config.GetSection("Smtp").Bind(_smtp);

        _logger = logger;
    }

    /// <summary>
    /// Generate a 6-digit code, store it for 5 minutes, and email it.
    /// </summary>
    public async Task SendCodeAsync(string email)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        var key = email.Trim().ToLowerInvariant();

        _codes[key] = (code, DateTime.UtcNow.AddMinutes(5));

        _logger.LogInformation("2FA code generated for {Email}, expires in 5 min", email);

        await SendEmailAsync(email, code);
    }

    /// <summary>
    /// Verify the code. Returns true if valid. Removes it on success.
    /// </summary>
    public bool VerifyCode(string email, string code)
    {
        var key = email.Trim().ToLowerInvariant();

        if (!_codes.TryGetValue(key, out var entry))
            return false;

        if (DateTime.UtcNow > entry.ExpiresUtc)
        {
            _codes.TryRemove(key, out _);
            return false;
        }

        if (entry.Code != code.Trim())
            return false;

        // Valid — remove so it can't be reused
        _codes.TryRemove(key, out _);
        return true;
    }

    private async Task SendEmailAsync(string toEmail, string code)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.Username))
        {
            _logger.LogWarning("2FA SMTP not configured. Code for {Email}: {Code}", toEmail, code);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = $"Tenurix Verification Code: {code}";

        var html = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;padding:32px;'>
    <div style='text-align:center;margin-bottom:24px;'>
        <h2 style='color:#1e3a5f;margin:0;'>Tenurix Management</h2>
        <p style='color:#64748b;font-size:14px;margin:4px 0 0 0;'>Two-Factor Authentication</p>
    </div>
    <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;padding:28px;text-align:center;'>
        <p style='color:#374151;font-size:15px;margin:0 0 16px 0;'>Your verification code is:</p>
        <div style='font-size:36px;font-weight:700;letter-spacing:8px;color:#3B82F6;
                    background:#fff;border:2px solid #3B82F6;border-radius:10px;
                    padding:16px 24px;display:inline-block;'>{code}</div>
        <p style='color:#64748b;font-size:13px;margin:20px 0 0 0;'>
            This code expires in <strong>5 minutes</strong>.<br/>
            If you did not request this, please ignore this email.
        </p>
    </div>
    <p style='color:#94a3b8;font-size:11px;text-align:center;margin-top:20px;'>
        &copy; Tenurix Property Management
    </p>
</div>";

        var bodyBuilder = new BodyBuilder { HtmlBody = html };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        var sslMode = _smtp.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_smtp.Host, _smtp.Port, sslMode);
        await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("2FA email sent to {Email}", toEmail);
    }
}
