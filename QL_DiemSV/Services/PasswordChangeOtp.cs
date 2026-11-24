using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace QL_DiemSV.Services;

public sealed class PasswordChangeOtp(IMemoryCache cache, IConfiguration configuration)
{
    private readonly object gate = new();
    private sealed class State(string code, string hash)
    {
        public string Code = code;
        public string Hash = hash;
        public DateTimeOffset Expires = DateTimeOffset.UtcNow.AddMinutes(5);
        public int Attempts;
    }
    private static string Key(int id) => $"CHANGE_PASSWORD_OTP:{id}";
    public async Task SendAsync(int id, string passwordHash, CancellationToken ct)
    {
        var settings = configuration.GetSection("AcademicEmail");
        var sender = settings["Username"];
        var password = settings["AppPassword"];
        var recipient = settings["Recipient"];
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("Chưa cấu hình email gửi/nhận mã xác thực.");
        lock (gate)
        {
            if (cache.TryGetValue(Key(id)+":cooldown", out _))
                throw new InvalidOperationException("Vui lòng chờ 60 giây trước khi gửi lại mã.");
            cache.Set(Key(id)+":cooldown", true, TimeSpan.FromSeconds(60));
            cache.Remove(Key(id));
        }
        var state = new State(RandomNumberGenerator.GetInt32(100000, 1000000).ToString(), passwordHash);
        using var message = new MailMessage(sender, recipient) {
            Subject = "[Academic Ledger] Xác thực đổi mật khẩu",
            Body = $"Mã xác thực đổi mật khẩu của bạn: {state.Code}. Mã có hiệu lực 5 phút, chỉ sử dụng một lần. Không chia sẻ mã cho người khác."
        };
        using var smtp = new SmtpClient(settings["Host"] ?? "smtp.gmail.com", settings.GetValue("Port", 587)) {
            EnableSsl = true, UseDefaultCredentials = false, Credentials = new NetworkCredential(sender, password)
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await smtp.SendMailAsync(message, timeout.Token);
        lock (gate) cache.Set(Key(id), state, state.Expires);
    }
    public bool Consume(int id, string hash, string? code)
    {
        lock (gate)
        {
            if (!cache.TryGetValue(Key(id), out State? state) || state == null) return false;
            if (state.Expires <= DateTimeOffset.UtcNow || state.Hash != hash) { cache.Remove(Key(id)); return false; }
            if (string.IsNullOrWhiteSpace(code) || state.Code != code.Trim())
            {
                if (++state.Attempts >= 5) cache.Remove(Key(id));
                return false;
            }
            cache.Remove(Key(id));
            return true;
        }
    }
}
