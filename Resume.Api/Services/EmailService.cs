using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Resume.Api.Configuration;

namespace Resume.Api.Services;

public class EmailService(
    IOptions<EmailOptions> emailOptions,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailOptions _opts = emailOptions.Value;

    public async Task SendVerificationEmailAsync(
        string toEmail,
        string verificationLink,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.SmtpHost))
        {
            // Development fallback: log the link so devs can verify without a mail server.
            logger.LogWarning(
                "SMTP is not configured — email not sent to {Email}. Paste this link to verify: {Link}",
                toEmail,
                verificationLink);
            return;
        }

        using var client = new SmtpClient(_opts.SmtpHost, _opts.SmtpPort)
        {
            EnableSsl = _opts.UseSsl,
            Credentials = new NetworkCredential(_opts.SmtpUser, _opts.SmtpPassword),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var from = new MailAddress(_opts.FromAddress, _opts.FromName);
        var to = new MailAddress(toEmail);

        using var message = new MailMessage(from, to)
        {
            Subject = "Verify your email — Resume Automation",
            IsBodyHtml = true,
            Body = BuildHtmlBody(verificationLink)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Verification email sent to {Email}.", toEmail);
    }

    private static string BuildHtmlBody(string link)
    {
        // In HTML attributes, '&' between query-string params must be '&amp;'.
        // Without this, some email clients strip '&token=...' as an unknown entity.
        var hrefSafeLink = link.Replace("&", "&amp;");
        return BuildHtml(hrefSafeLink, link);
    }

    private static string BuildHtml(string hrefSafeLink, string plainLink) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#f8fafc;font-family:Arial,Helvetica,sans-serif">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
            <tr>
              <td align="center" style="padding:40px 16px">
                <table role="presentation" width="480" cellspacing="0" cellpadding="0"
                       style="background:#0f172a;border-radius:16px;border:1px solid rgba(148,163,184,0.18);overflow:hidden">
                  <tr>
                    <td style="padding:36px 40px">
                      <p style="margin:0 0 4px;font-size:11px;text-transform:uppercase;letter-spacing:.14em;color:#818cf8;font-weight:600">
                        Resume Automation
                      </p>
                      <h1 style="margin:8px 0 16px;font-size:24px;font-weight:700;color:#f8fafc">
                        Verify your email
                      </h1>
                      <p style="margin:0 0 28px;font-size:15px;line-height:1.6;color:#94a3b8">
                        Thanks for signing up! Click the button below to verify your email address
                        and activate your account. This link expires in&nbsp;<strong style="color:#e2e8f0">24&nbsp;hours</strong>.
                      </p>
                      <a href="{hrefSafeLink}"
                         style="display:inline-block;padding:13px 28px;background:linear-gradient(135deg,#a5b4fc,#38bdf8);
                                color:#0f172a;text-decoration:none;border-radius:10px;font-weight:700;font-size:15px">
                        Verify email address
                      </a>
                      <p style="margin:32px 0 0;font-size:12px;color:#475569;line-height:1.6">
                        If you didn't create an account you can safely ignore this email.<br>
                        Can't click the button? Copy and paste this link:<br>
                        <a href="{hrefSafeLink}" style="color:#818cf8;word-break:break-all">{plainLink}</a>
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
