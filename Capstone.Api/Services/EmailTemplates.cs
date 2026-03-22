namespace Capstone.Api.Services;

/// <summary>
/// Branded HTML email templates for Tenurix notifications.
/// Uses Tenurix brand colors: Navy (#1e3a5f), Gold (#c4985a), White
/// </summary>
public static class EmailTemplates
{
    private const string LOGO_URL = "https://tenurix.net/logo.png";
    private const string LOGIN_URL = "https://tenurix.net/auth";
    private const string LANDLORD_LOGIN_URL = "https://tenurix.net/landlord/login";
    private const string SUPPORT_EMAIL = "support@tenurix.net";

    // Brand colors
    private const string NAVY = "#1e3a5f";
    private const string GOLD = "#c4985a";
    private const string LIGHT_NAVY = "#2a4f7a";
    private const string LIGHT_GOLD = "#dbb887";

    // ─── SHARED LAYOUT ───────────────────────────────────────────────
    private static string Wrap(string title, string bodyHtml, string loginUrl = LOGIN_URL)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{title}</title>
</head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif;-webkit-font-smoothing:antialiased;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f1f5f9;padding:40px 16px;"">
  <tr><td align=""center"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 32px rgba(0,0,0,0.08);"">

      <!-- ═══ HEADER with Logo ═══ -->
      <tr>
        <td style=""background:{NAVY};padding:0;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td style=""padding:24px 32px;"">
                <table cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td style=""vertical-align:middle;padding-right:14px;"">
                      <img src=""{LOGO_URL}"" alt=""Tenurix"" width=""44"" height=""44"" style=""display:block;border:0;border-radius:10px;"" />
                    </td>
                    <td style=""vertical-align:middle;"">
                      <h1 style=""margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:1.5px;"">TENURIX</h1>
                      <p style=""margin:2px 0 0;color:{LIGHT_GOLD};font-size:11px;letter-spacing:0.5px;text-transform:uppercase;"">Property Management</p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </td>
      </tr>

      <!-- ═══ GOLD ACCENT LINE ═══ -->
      <tr>
        <td style=""height:4px;background:linear-gradient(90deg,{GOLD},{LIGHT_GOLD},{GOLD});""></td>
      </tr>

      <!-- ═══ BODY ═══ -->
      <tr>
        <td style=""padding:36px 32px 24px;"">
          {bodyHtml}
        </td>
      </tr>

      <!-- ═══ CTA BUTTON ═══ -->
      <tr>
        <td style=""padding:0 32px 32px;"" align=""center"">
          <a href=""{loginUrl}"" style=""display:inline-block;padding:14px 36px;background:{NAVY};color:#ffffff;text-decoration:none;border-radius:10px;font-size:14px;font-weight:600;letter-spacing:0.3px;"">
            Log in to Tenurix
          </a>
        </td>
      </tr>

      <!-- ═══ DIVIDER ═══ -->
      <tr>
        <td style=""padding:0 32px;"">
          <div style=""height:1px;background:#e2e8f0;""></div>
        </td>
      </tr>

      <!-- ═══ FOOTER ═══ -->
      <tr>
        <td style=""padding:24px 32px;background:#fafbfc;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td align=""center"">
                <p style=""margin:0 0 8px;font-size:12px;color:#94a3b8;"">
                  This is an automated notification from Tenurix Property Management.
                </p>
                <p style=""margin:0 0 8px;font-size:12px;color:#94a3b8;"">
                  Need help? Contact us at
                  <a href=""mailto:{SUPPORT_EMAIL}"" style=""color:{NAVY};text-decoration:none;font-weight:600;"">{SUPPORT_EMAIL}</a>
                </p>
                <p style=""margin:0;font-size:11px;color:#cbd5e1;"">
                  &copy; {DateTime.UtcNow.Year} Tenurix &middot; All rights reserved
                </p>
              </td>
            </tr>
          </table>
        </td>
      </tr>

    </table>

    <!-- ═══ BELOW-CARD LINKS ═══ -->
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;"">
      <tr>
        <td align=""center"" style=""padding:20px 0;"">
          <a href=""https://tenurix.net"" style=""color:#94a3b8;font-size:12px;text-decoration:none;margin:0 8px;"">tenurix.net</a>
          <span style=""color:#cbd5e1;"">|</span>
          <a href=""mailto:{SUPPORT_EMAIL}"" style=""color:#94a3b8;font-size:12px;text-decoration:none;margin:0 8px;"">Support</a>
        </td>
      </tr>
    </table>

  </td></tr>
</table>

</body>
</html>";
    }

    private static string StatusPill(string status, string bgColor)
    {
        return $@"<span style=""display:inline-block;padding:5px 16px;border-radius:20px;font-size:12px;font-weight:700;background:{bgColor};color:#ffffff;letter-spacing:0.3px;text-transform:uppercase;"">{status}</span>";
    }

    private static string InfoCard(string content, string borderColor = "#e2e8f0", string bgColor = "#f8fafc")
    {
        return $@"<div style=""margin:20px 0;padding:20px;background:{bgColor};border-radius:12px;border:1px solid {borderColor};"">
            {content}
        </div>";
    }

    private static string FieldRow(string label, string value)
    {
        return $@"<tr>
            <td style=""padding:6px 0;font-size:12px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;vertical-align:top;width:120px;"">{label}</td>
            <td style=""padding:6px 0;font-size:15px;color:#1e293b;font-weight:500;"">{value}</td>
        </tr>";
    }

    private static string NoteBlock(string? note, string label, string bgColor, string borderColor, string textColor)
    {
        if (string.IsNullOrWhiteSpace(note)) return "";
        return $@"<div style=""margin-top:16px;padding:14px 18px;background:{bgColor};border-left:4px solid {borderColor};border-radius:0 8px 8px 0;"">
            <p style=""margin:0;font-size:13px;color:{textColor};""><strong>{label}:</strong> {note}</p>
        </div>";
    }

    // ─── PROPERTY SUBMISSION ─────────────────────────────────────────

    public static (string subject, string html) PropertySubmitted(string landlordName, string address)
    {
        return (
            $"Submission Received — {address}",
            Wrap("Submission Received", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Submission Received</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Your property submission has been received and is now <strong>pending review</strong> by our management team.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("Pending Review", NAVY))}
                    </table>
                ")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;line-height:1.5;"">
                    You'll receive an email once your property has been reviewed. This usually takes 1-2 business days.
                </p>
            ", LANDLORD_LOGIN_URL)
        );
    }

    public static (string subject, string html) PropertyApproved(string landlordName, string address, string? note)
    {
        return (
            $"Property Approved — {address}",
            Wrap("Property Approved", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Property Approved</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Great news! Your property submission has been <strong style=""color:#16a34a;"">approved</strong> and is now live on Tenurix.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("Approved", "#16a34a"))}
                    </table>
                ", "#bbf7d0", "#f0fdf4")}
                {NoteBlock(note, "Note", "#f0fdf4", "#22c55e", "#15803d")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    Tenants can now view and apply for your listing.
                </p>
            ", LANDLORD_LOGIN_URL)
        );
    }

    public static (string subject, string html) PropertyRejected(string landlordName, string address, string? note)
    {
        return (
            $"Property Update — {address}",
            Wrap("Property Rejected", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Submission Update</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Unfortunately, your property submission has been <strong style=""color:#dc2626;"">rejected</strong>. Please review the details below.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("Rejected", "#ef4444"))}
                    </table>
                ", "#fecaca", "#fef2f2")}
                {NoteBlock(note, "Reason", "#fef2f2", "#ef4444", "#dc2626")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    You may resubmit with the required corrections from your dashboard.
                </p>
            ", LANDLORD_LOGIN_URL)
        );
    }

    public static (string subject, string html) PropertyOnHold(string landlordName, string address, string? note)
    {
        return (
            $"Property On Hold — {address}",
            Wrap("Property On Hold", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Property On Hold</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Your property submission has been placed <strong style=""color:#d97706;"">on hold</strong> for further review.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("On Hold", "#f59e0b"))}
                    </table>
                ", "#fde68a", "#fffbeb")}
                {NoteBlock(note, "Note", "#fffbeb", "#f59e0b", "#d97706")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    Our team may reach out for additional information.
                </p>
            ", LANDLORD_LOGIN_URL)
        );
    }

    // ─── LEASE APPLICATIONS ──────────────────────────────────────────

    public static (string subject, string html) LeaseApplicationSubmitted(string tenantName, string address, DateTime startDate, DateTime endDate)
    {
        return (
            $"Application Submitted — {address}",
            Wrap("Application Received", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Application Received</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {tenantName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Your lease application has been submitted and is now <strong>under review</strong>.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Lease Period", $"{startDate:MMMM d, yyyy} — {endDate:MMMM d, yyyy}")}
                        {FieldRow("Status", StatusPill("Pending Review", NAVY))}
                    </table>
                ")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    You'll be notified once management has reviewed your application.
                </p>
            ")
        );
    }

    public static (string subject, string html) LeaseApplicationApproved(string tenantName, string address, DateTime startDate, DateTime endDate)
    {
        return (
            $"Lease Approved — {address}",
            Wrap("Lease Approved", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Lease Application Approved!</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {tenantName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Congratulations! Your lease application has been <strong style=""color:#16a34a;"">approved</strong>.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Lease Period", $"{startDate:MMMM d, yyyy} — {endDate:MMMM d, yyyy}")}
                        {FieldRow("Status", StatusPill("Approved", "#16a34a"))}
                    </table>
                ", "#bbf7d0", "#f0fdf4")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    Please log in to your Tenurix account to review your lease details and next steps.
                </p>
            ")
        );
    }

    public static (string subject, string html) LeaseApplicationRejected(string tenantName, string address, string? note)
    {
        return (
            $"Application Update — {address}",
            Wrap("Application Update", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Application Update</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {tenantName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    We regret to inform you that your lease application for <strong>{address}</strong> has been <strong style=""color:#dc2626;"">rejected</strong>.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("Rejected", "#ef4444"))}
                    </table>
                ", "#fecaca", "#fef2f2")}
                {NoteBlock(note, "Reason", "#fef2f2", "#ef4444", "#dc2626")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    You can browse other available listings and submit a new application.
                </p>
            ")
        );
    }

    // Notify landlord about new application on their property
    public static (string subject, string html) NewLeaseApplication(string landlordName, string tenantName, string tenantEmail, string address)
    {
        return (
            $"New Application — {address}",
            Wrap("New Application", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">New Lease Application</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    A new lease application has been submitted for your property.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Property", address)}
                        {FieldRow("Applicant", $"{tenantName}")}
                        {FieldRow("Email", $"<a href='mailto:{tenantEmail}' style='color:{NAVY};'>{tenantEmail}</a>")}
                    </table>
                ")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    Management will review and process this application.
                </p>
            ", LANDLORD_LOGIN_URL)
        );
    }

    // ─── ISSUES ──────────────────────────────────────────────────────

    public static (string subject, string html) NewIssueReported(string recipientName, string reporterName, string issueType, string address, string description)
    {
        return (
            $"New Issue — {issueType} at {address}",
            Wrap("New Issue Reported", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">New Issue Reported</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {recipientName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    A new maintenance issue has been reported.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Issue Type", $"<strong>{issueType}</strong>")}
                        {FieldRow("Property", address)}
                        {FieldRow("Reported By", reporterName)}
                        {FieldRow("Status", StatusPill("Submitted", "#f59e0b"))}
                    </table>
                ", "#fecaca", "#fef2f2")}
                <div style=""margin-top:12px;padding:14px 18px;background:#f8fafc;border-left:4px solid {NAVY};border-radius:0 8px 8px 0;"">
                    <p style=""margin:0 0 4px;font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;"">Description</p>
                    <p style=""margin:0;font-size:14px;color:#475569;line-height:1.5;"">{description}</p>
                </div>
            ", LANDLORD_LOGIN_URL)
        );
    }

    // Confirm issue submitted to the reporter
    public static (string subject, string html) IssueSubmittedConfirmation(string reporterName, string issueType, string address)
    {
        return (
            $"Issue Submitted — {issueType}",
            Wrap("Issue Submitted", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Issue Submitted</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {reporterName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Your maintenance issue has been submitted and will be reviewed by the management team.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Issue Type", $"<strong>{issueType}</strong>")}
                        {FieldRow("Property", address)}
                        {FieldRow("Status", StatusPill("Submitted", "#f59e0b"))}
                    </table>
                ")}
                <p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                    You'll receive updates as the issue is being addressed.
                </p>
            ")
        );
    }

    public static (string subject, string html) IssueStatusUpdated(string recipientName, string issueType, string address, string oldStatus, string newStatus)
    {
        var newColor = newStatus switch
        {
            "Resolved" => "#16a34a",
            "InProgress" => "#2563eb",
            _ => "#f59e0b"
        };

        var statusLabel = newStatus switch
        {
            "InProgress" => "In Progress",
            _ => newStatus
        };

        return (
            $"Issue {statusLabel} — {issueType}",
            Wrap("Issue Updated", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">Issue Status Updated</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {recipientName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    A maintenance issue has been updated.
                </p>
                {InfoCard($@"
                    <table cellpadding='0' cellspacing='0' width='100%'>
                        {FieldRow("Issue Type", $"<strong>{issueType}</strong>")}
                        {FieldRow("Property", address)}
                        {FieldRow("Status", $"{StatusPill(oldStatus, "#94a3b8")} <span style='margin:0 6px;color:#94a3b8;'>&rarr;</span> {StatusPill(statusLabel, newColor)}")}
                    </table>
                ")}
                {(newStatus == "Resolved" ?
                    @"<p style=""margin:16px 0 0;font-size:14px;color:#16a34a;font-weight:600;"">
                        This issue has been resolved. If the problem persists, please submit a new report.
                    </p>" :
                    @"<p style=""margin:16px 0 0;font-size:14px;color:#64748b;"">
                        You'll receive further updates as the issue progresses.
                    </p>"
                )}
            ")
        );
    }

    // ─── ID REQUEST ──────────────────────────────────────────────────

    public static (string subject, string html) NewIdRequested(string landlordName, string? message)
    {
        return (
            "Action Required — New ID Document Requested",
            Wrap("ID Requested", $@"
                <h2 style=""margin:0 0 6px;font-size:22px;color:{NAVY};font-weight:700;"">ID Document Requested</h2>
                <p style=""margin:0 0 20px;font-size:15px;color:#64748b;"">Hi {landlordName},</p>
                <p style=""margin:0 0 16px;font-size:15px;color:#475569;line-height:1.6;"">
                    Management has requested a new identification document from you.
                    Please log in and upload your updated ID from the <strong>Properties</strong> page.
                </p>
                {NoteBlock(message, "Message from Management", "#eff6ff", "#3b82f6", "#1d4ed8")}
                <div style=""margin:20px 0;padding:16px;background:#fffbeb;border-radius:12px;border:1px solid #fde68a;text-align:center;"">
                    <p style=""margin:0;font-size:14px;color:#92400e;font-weight:600;"">
                        This is required to keep your account and listings in good standing.
                    </p>
                </div>
            ", LANDLORD_LOGIN_URL)
        );
    }
}
