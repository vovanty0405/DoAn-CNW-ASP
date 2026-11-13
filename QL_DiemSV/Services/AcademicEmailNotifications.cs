using System.Net;
using System.Net.Mail;
using System.Threading.Channels;

namespace QL_DiemSV.Services;

public sealed class AcademicEmailNotifications(
    IConfiguration configuration,
    ILogger<AcademicEmailNotifications> logger) : BackgroundService
{
    private readonly Channel<string> _pending = Channel.CreateBounded<string>(100);

    public void Enqueue(string subject)
    {
        if (bool.TryParse(configuration["AcademicEmail:Enabled"], out var enabled) && enabled
            && !_pending.Writer.TryWrite(subject))
            logger.LogWarning("Academic email queue is full. The web notification remains available.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var subject in _pending.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var settings = configuration.GetSection("AcademicEmail");
                var username = settings["Username"];
                var password = settings["AppPassword"];
                var recipient = settings["Recipient"];
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                    || string.IsNullOrWhiteSpace(recipient))
                {
                    logger.LogWarning("Academic email requires Username, AppPassword and Recipient.");
                    continue;
                }

                using var message = new MailMessage(new MailAddress(username), new MailAddress(recipient))
                {
                    Subject = subject,
                    Body = "Academic Ledger vừa ghi nhận một cập nhật học vụ. Hãy đăng nhập vào hệ thống để xem thông báo và chi tiết theo quyền truy cập."
                };
                using var smtp = new SmtpClient(settings["Host"] ?? "smtp.gmail.com", settings.GetValue("Port", 587))
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(username, password)
                };
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                await smtp.SendMailAsync(message, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SmtpException ex)
            {
                logger.LogWarning("Academic email could not be sent. SMTP status: {Status}.", ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Academic email could not be sent. Error type: {Type}.", ex.GetType().Name);
            }
        }
    }
}
