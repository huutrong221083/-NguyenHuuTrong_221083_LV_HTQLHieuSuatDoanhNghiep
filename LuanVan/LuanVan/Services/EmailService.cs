using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace LuanVan.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default);
    Task SendSystemNotificationEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "LuanVan KPI";
}

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default)
    {
        EnsureSmtpConfigured();
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new ArgumentException("Email người nhận không hợp lệ.", nameof(recipientEmail));
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "[LuanVan KPI] Dat lai mat khau",
            IsBodyHtml = true,
            Body = BuildResetPasswordBody(recipientName, resetLink)
        };

        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendSystemNotificationEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        EnsureSmtpConfigured();

        if (string.IsNullOrWhiteSpace(recipientEmail) || string.IsNullOrWhiteSpace(subject))
        {
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            IsBodyHtml = true,
            Body = BuildSystemNotificationBody(recipientName, htmlBody)
        };

        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private void EnsureSmtpConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.FromEmail)
            || string.IsNullOrWhiteSpace(_options.UserName)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogError("SMTP chưa được cấu hình đầy đủ. Thiếu Host/UserName/Password/FromEmail.");
            throw new InvalidOperationException("SMTP chưa được cấu hình đầy đủ.");
        }
    }

    private static string BuildResetPasswordBody(string recipientName, string resetLink)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "người dùng" : recipientName);
        var safeLink = WebUtility.HtmlEncode(resetLink);
        var requestedAt = WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

        return $"""
            <div style=\"font-family:Arial,sans-serif;color:#0f172a;line-height:1.55;max-width:680px;margin:0 auto;\">
                <div style=\"border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;\">
                    <div style=\"background:#0d6efd;color:#ffffff;padding:16px 20px;\">
                        <h2 style=\"margin:0;font-size:20px;\">Đặt lại mật khẩu</h2>
                        <p style=\"margin:6px 0 0 0;font-size:13px;opacity:.92;\">Hệ thống LuanVan KPI</p>
                    </div>

                    <div style=\"padding:18px 20px;background:#ffffff;\">
                        <p>Xin chào <strong>{safeName}</strong>,</p>
                        <p>Hệ thống vừa nhận yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>

                        <div style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 12px;margin:12px 0;\">
                            <div><strong>Thời gian yêu cầu:</strong> {requestedAt}</div>
                        </div>

                        <p style=\"margin:14px 0;\">
                            <a href=\"{safeLink}\" style=\"display:inline-block;padding:10px 14px;background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;\">Đặt lại mật khẩu</a>
                        </p>

                        <p>Nếu nút không bấm được, bạn có thể sao chép liên kết sau vào trình duyệt:</p>
                        <p style=\"word-break:break-all;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:8px;padding:10px 12px;\">{safeLink}</p>

                        <p style=\"margin-top:14px;\">Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email. Mật khẩu hiện tại vẫn giữ nguyên.</p>
                        <p style=\"color:#475569;font-size:12px;margin-bottom:0;\">Liên kết đặt lại mật khẩu có thời hạn và chỉ dùng được một lần.</p>
                    </div>
                </div>
            </div>
            """;
    }

    private static string BuildSystemNotificationBody(string recipientName, string htmlBody)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "người dùng" : recipientName);
        var body = string.IsNullOrWhiteSpace(htmlBody) ? "<p>Không có nội dung.</p>" : htmlBody;

        return $"""
            <div style=\"font-family:Arial,sans-serif;color:#0f172a;line-height:1.5\">
                <h2 style=\"margin-bottom:8px;\">Thông báo hệ thống</h2>
                <p>Xin chào {safeName},</p>
                {body}
                <p style=\"margin-top:12px;color:#475569;font-size:12px;\">Email này được gửi tự động từ hệ thống LuanVan KPI.</p>
            </div>
            """;
    }
}
