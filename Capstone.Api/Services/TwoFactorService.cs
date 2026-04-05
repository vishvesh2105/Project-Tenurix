using Capstone.Api.Data;
using Dapper;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Capstone.Api.Services;

public sealed class TwoFactorService
{
    private readonly SmtpSettings _smtp;
    private readonly SqlConnectionFactory _db;
    private readonly ILogger<TwoFactorService> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    public TwoFactorService(IConfiguration config, SqlConnectionFactory db, ILogger<TwoFactorService> logger)
    {
        _smtp = new SmtpSettings();
        config.GetSection("Smtp2FA").Bind(_smtp);

        // Fallback to main Smtp if Smtp2FA not configured
        if (string.IsNullOrWhiteSpace(_smtp.Host))
            config.GetSection("Smtp").Bind(_smtp);

        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Generate a 6-digit code, persist it in the database for 5 minutes, and email it.
    /// Resets failed attempts so the user gets a fresh window after requesting a new code.
    /// </summary>
    /// <param name="purpose">Ties the code to a specific flow: "login", "register", "reset". Prevents cross-flow reuse.</param>
    public async Task SendCodeAsync(string email, string purpose = "login")
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        var key = email.Trim().ToLowerInvariant();
        var purposeKey = purpose.Trim().ToLowerInvariant();

        await EnsureTableAsync();

        await using var conn = _db.Create();

        // Clean up expired codes opportunistically on each send (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await using var cleanConn = _db.Create();
                await cleanConn.ExecuteAsync(
                    "DELETE FROM dbo.TwoFactorCodes WHERE ExpiresUtc < @Now;",
                    new { Now = DateTime.UtcNow });
            }
            catch { /* Non-critical */ }
        });

        // Upsert — reset failed attempts and lockout when a new code is issued
        await conn.ExecuteAsync(@"
            MERGE dbo.TwoFactorCodes AS target
            USING (SELECT @Email AS Email, @Purpose AS Purpose) AS source
                ON target.Email = source.Email AND target.Purpose = source.Purpose
            WHEN MATCHED THEN
                UPDATE SET Code = @Code,
                           ExpiresUtc = @ExpiresUtc,
                           FailedAttempts = 0,
                           LockedUntil = NULL
            WHEN NOT MATCHED THEN
                INSERT (Email, Code, ExpiresUtc, FailedAttempts, LockedUntil, Purpose)
                VALUES (@Email, @Code, @ExpiresUtc, 0, NULL, @Purpose);",
            new { Email = key, Code = code, ExpiresUtc = DateTime.UtcNow.AddMinutes(5), Purpose = purposeKey });

        _logger.LogInformation("2FA code generated for {Email}, expires in 5 min", email);

        await SendEmailAsync(email, code);
    }

    /// <summary>
    /// Verify the 6-digit code.
    /// Returns (true, null) on success.
    /// Returns (false, errorMessage) on failure — includes lockout and expiry reasons.
    /// Increments failed attempts and locks the account after MaxFailedAttempts.
    /// </summary>
    /// <param name="purpose">Must match the purpose used when sending the code. Prevents cross-flow code reuse.</param>
    public async Task<(bool Success, string? Error)> VerifyCodeAsync(string email, string code, string purpose = "login")
    {
        var key = email.Trim().ToLowerInvariant();
        var purposeKey = purpose.Trim().ToLowerInvariant();

        await EnsureTableAsync();

        await using var conn = _db.Create();

        var row = await conn.QuerySingleOrDefaultAsync(@"
            SELECT Code, ExpiresUtc, FailedAttempts, LockedUntil
            FROM dbo.TwoFactorCodes
            WHERE Email = @Email AND Purpose = @Purpose;",
            new { Email = key, Purpose = purposeKey });

        if (row == null)
            return (false, "Invalid or expired verification code.");

        // Check lockout
        DateTime? lockedUntil = row.LockedUntil;
        if (lockedUntil.HasValue && DateTime.UtcNow < lockedUntil.Value)
        {
            var remaining = (int)Math.Ceiling((lockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            return (false, $"Too many failed attempts. Try again in {remaining} minute{(remaining == 1 ? "" : "s")}.");
        }

        // Check expiry
        DateTime expiresUtc = row.ExpiresUtc;
        if (DateTime.UtcNow > expiresUtc)
        {
            await conn.ExecuteAsync("DELETE FROM dbo.TwoFactorCodes WHERE Email = @Email AND Purpose = @Purpose;", new { Email = key, Purpose = purposeKey });
            return (false, "Verification code has expired. Please request a new one.");
        }

        // Check code
        string storedCode = row.Code?.ToString() ?? "";
        if (storedCode != code.Trim())
        {
            int failed = (int)row.FailedAttempts + 1;

            if (failed >= MaxFailedAttempts)
            {
                // Lock the account
                var lockUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                await conn.ExecuteAsync(@"
                    UPDATE dbo.TwoFactorCodes
                    SET FailedAttempts = @Failed, LockedUntil = @LockUntil
                    WHERE Email = @Email AND Purpose = @Purpose;",
                    new { Failed = failed, LockUntil = lockUntil, Email = key, Purpose = purposeKey });

                _logger.LogWarning("2FA account locked for {Email} after {Max} failed attempts", email, MaxFailedAttempts);
                return (false, $"Too many failed attempts. Your account is locked for {LockoutMinutes} minutes.");
            }

            await conn.ExecuteAsync(@"
                UPDATE dbo.TwoFactorCodes
                SET FailedAttempts = @Failed
                WHERE Email = @Email AND Purpose = @Purpose;",
                new { Failed = failed, Email = key, Purpose = purposeKey });

            int remaining = MaxFailedAttempts - failed;
            return (false, $"Invalid verification code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        }

        // Valid — atomic delete so concurrent requests with same code cannot both succeed
        var deleted = await conn.ExecuteAsync(
            "DELETE FROM dbo.TwoFactorCodes WHERE Email = @Email AND Purpose = @Purpose AND Code = @Code;",
            new { Email = key, Purpose = purposeKey, Code = storedCode });
        if (deleted == 0)
            return (false, "Invalid or expired verification code.");
        return (true, null);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static int _tableEnsured = 0;

    private async Task EnsureTableAsync()
    {
        // Only run once per process lifetime
        if (Interlocked.CompareExchange(ref _tableEnsured, 1, 0) != 0) return;

        await using var conn = _db.Create();
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.objects
                WHERE object_id = OBJECT_ID(N'dbo.TwoFactorCodes') AND type = 'U'
            )
            BEGIN
                CREATE TABLE dbo.TwoFactorCodes (
                    Email          NVARCHAR(256) NOT NULL,
                    Purpose        NVARCHAR(20)  NOT NULL DEFAULT 'login',
                    Code           NVARCHAR(10)  NOT NULL,
                    ExpiresUtc     DATETIME2     NOT NULL,
                    FailedAttempts INT           NOT NULL DEFAULT 0,
                    LockedUntil    DATETIME2     NULL,
                    CONSTRAINT PK_TwoFactorCodes PRIMARY KEY (Email, Purpose)
                )
            END
            ELSE
            BEGIN
                -- Add Purpose column if it doesn't exist yet (migration)
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.TwoFactorCodes') AND name = 'Purpose'
                )
                BEGIN
                    ALTER TABLE dbo.TwoFactorCodes ADD Purpose NVARCHAR(20) NOT NULL DEFAULT 'login';
                END
            END");
    }

    private async Task SendEmailAsync(string toEmail, string code)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.Username))
        {
            // Do NOT log the code — just warn that email was not sent
            _logger.LogWarning("2FA SMTP not configured. Verification code was not sent to {Email}.", toEmail);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "Your Tenurix Verification Code";

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
