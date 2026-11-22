using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    private const int MaxLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OtpExpiration = TimeSpan.FromMinutes(5);
    private const string OtpTargetEmail = "voty365@gmail.com";

    public AuthController(
        ApplicationDbContext db,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _db = db;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public class LoginDto
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    private sealed class LoginLockState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    private sealed class OtpResetState
    {
        public string Username { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public int FailedVerifyCount { get; set; }
    }

    private string GetClientKey(string username)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "local";
        return $"LOGIN_LOCK:{ip}:{username.ToUpperInvariant()}";
    }

    private (bool IsLocked, int RemainingSeconds, LoginLockState State) CheckLockState(string cacheKey)
    {
        if (!_cache.TryGetValue(cacheKey, out LoginLockState? state) || state == null)
        {
            state = new LoginLockState();
            return (false, 0, state);
        }

        if (state.LockedUntil.HasValue)
        {
            var remaining = (int)Math.Ceiling((state.LockedUntil.Value - DateTimeOffset.UtcNow).TotalSeconds);
            if (remaining > 0)
            {
                return (true, remaining, state);
            }

            // Hết hạn khóa -> reset
            state.FailedAttempts = 0;
            state.LockedUntil = null;
            _cache.Remove(cacheKey);
        }

        return (false, 0, state);
    }

    private (bool NewlyLocked, int RemainingAttempts, int LockRemainingSeconds) RecordFailedAttempt(string cacheKey, LoginLockState state)
    {
        state.FailedAttempts++;
        if (state.FailedAttempts >= MaxLoginAttempts)
        {
            state.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            _cache.Set(cacheKey, state, LockoutDuration.Add(TimeSpan.FromMinutes(1)));
            var secs = (int)LockoutDuration.TotalSeconds;
            return (true, 0, secs);
        }

        _cache.Set(cacheKey, state, TimeSpan.FromMinutes(15));
        return (false, MaxLoginAttempts - state.FailedAttempts, 0);
    }

    [HttpPost("login"), EnableRateLimiting("SignIn")]
    public async Task<IActionResult> Login([FromBody] LoginDto req)
    {
        var uname = (req.Username ?? "").Trim();
        var pwd = req.Password ?? "";

        if (string.IsNullOrEmpty(uname) || string.IsNullOrEmpty(pwd))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập đầy đủ Tên đăng nhập và Mật khẩu!" });
        }

        var lockKey = GetClientKey(uname);
        var (isLocked, remainingSecs, lockState) = CheckLockState(lockKey);
        if (isLocked)
        {
            var mins = remainingSecs / 60;
            var secs = remainingSecs % 60;
            return StatusCode(429, new
            {
                status = "error",
                locked = true,
                retryAfterSeconds = remainingSecs,
                remainingAttempts = 0,
                message = $"Tài khoản tạm khóa đăng nhập do nhập sai quá {MaxLoginAttempts} lần. Vui lòng thử lại sau {(mins > 0 ? $"{mins} phút {secs} giây" : $"{secs} giây")}."
            });
        }

        var user = await _db.UserAccounts
            .Include(u => u.Role)
            .Include(u => u.GiangVien)
            .Include(u => u.SinhVien)
            .FirstOrDefaultAsync(u => u.Username == uname);

        if (user == null)
        {
            var (newlyLocked, remAttempts, lockSecs) = RecordFailedAttempt(lockKey, lockState);
            if (newlyLocked)
            {
                return StatusCode(429, new
                {
                    status = "error",
                    locked = true,
                    retryAfterSeconds = lockSecs,
                    remainingAttempts = 0,
                    message = $"Bạn đã nhập sai {MaxLoginAttempts}/{MaxLoginAttempts} lần liên tiếp. Hệ thống tạm khóa đăng nhập trong 5 phút để bảo vệ an toàn!"
                });
            }

            return Unauthorized(new
            {
                status = "error",
                locked = false,
                remainingAttempts = remAttempts,
                maxAttempts = MaxLoginAttempts,
                message = $"Tài khoản hoặc mật khẩu không chính xác! (Còn {remAttempts}/{MaxLoginAttempts} lần thử)"
            });
        }

        if (!user.IsActive)
        {
            return StatusCode(403, new { status = "error", message = "Tài khoản của bạn đã bị khóa bởi Quản trị viên!" });
        }

        bool validPassword = false;
        try
        {
            if (user.PasswordHash.StartsWith("$2"))
            {
                validPassword = BCrypt.Net.BCrypt.Verify(pwd, user.PasswordHash);
            }
            else
            {
                validPassword = user.PasswordHash == pwd;
            }
        }
        catch
        {
            validPassword = user.PasswordHash == pwd;
        }

        if (!validPassword)
        {
            var (newlyLocked, remAttempts, lockSecs) = RecordFailedAttempt(lockKey, lockState);
            if (newlyLocked)
            {
                GradeHelper.GhiNhatKy(_db, uname, "Khóa đăng nhập tạm thời", $"Tài khoản {uname} bị tạm khóa 5 phút do nhập sai mật khẩu {MaxLoginAttempts} lần liên tiếp.");
                return StatusCode(429, new
                {
                    status = "error",
                    locked = true,
                    retryAfterSeconds = lockSecs,
                    remainingAttempts = 0,
                    message = $"Bạn đã nhập sai mật khẩu {MaxLoginAttempts}/{MaxLoginAttempts} lần liên tiếp. Tài khoản tạm khóa trong 5 phút!"
                });
            }

            return Unauthorized(new
            {
                status = "error",
                locked = false,
                remainingAttempts = remAttempts,
                maxAttempts = MaxLoginAttempts,
                message = $"Tài khoản hoặc mật khẩu không chính xác! (Còn {remAttempts}/{MaxLoginAttempts} lần thử)"
            });
        }

        // Đăng nhập thành công -> xóa bộ đếm lỗi
        _cache.Remove(lockKey);

        string roleName = user.Role?.RoleName ?? "SinhVien";
        string? maGV = user.GiangVien?.MaGV ?? user.MaGV;
        string? maSV = user.SinhVien?.MaSV;
        if (roleName == "SinhVien" && string.IsNullOrEmpty(maSV)) maSV = user.Username;
        if (roleName == "GiangVien" && string.IsNullOrEmpty(maGV)) maGV = user.Username;

        string hoTen = user.GiangVien?.HoTen ?? user.SinhVien?.HoTen ?? (roleName == "Admin" ? "Quản trị viên Hệ thống" : user.Username);

        var permissions = await _db.PhanQuyens
            .Where(p => p.RoleID == user.RoleID)
            .Select(p => new { p.MaChucNang, p.MaHanhDong })
            .ToListAsync();

        var scopes = await _db.PhanQuyenPhamVis
            .Where(s => s.RoleID == user.RoleID && s.IsActive)
            .Select(s => s.MaPhamVi)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("RoleID", (user.RoleID ?? 0).ToString()),
            new Claim("HoTen", hoTen),
            new Claim("MaGV", maGV ?? ""),
            new Claim("MaSV", maSV ?? "")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

        // Tạo session tượng trưng
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.UserID}:{user.Username}:{roleName}:{DateTime.UtcNow.Ticks}"));
        _db.Sessions.Add(new Session
        {
            UserID = user.UserID,
            RefreshToken = token,
            ExpiresAt = DateTime.Now.AddDays(7),
            IsRevoked = false
        });
        await _db.SaveChangesAsync();

        GradeHelper.GhiNhatKy(_db, user.Username, "Đăng nhập", $"Người dùng {user.Username} ({roleName}) đã đăng nhập vào hệ thống.");

        var userPayload = new
        {
            userId = user.UserID,
            username = user.Username,
            role = roleName,
            roleId = user.RoleID,
            hoTen = hoTen,
            maGV = maGV,
            maSV = maSV,
            permissions,
            scopes
        };

        return Ok(new
        {
            status = "success",
            message = "Đăng nhập thành công!",
            data = new
            {
                accessToken = token,
                user = userPayload
            }
        });
    }

    public class ForgotPasswordOtpRequestDto
    {
        public string? Username { get; set; }
    }

    [HttpPost("forgot-password/send-otp")]
    public async Task<IActionResult> SendForgotPasswordOtp([FromBody] ForgotPasswordOtpRequestDto req)
    {
        var uname = (req?.Username ?? "").Trim();
        if (string.IsNullOrEmpty(uname))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập Tên đăng nhập / Mã SV / Mã GV cần khôi phục mật khẩu!" });
        }

        var user = await _db.UserAccounts
            .Include(u => u.Role)
            .Include(u => u.GiangVien)
            .Include(u => u.SinhVien)
            .FirstOrDefaultAsync(u => u.Username == uname);

        if (user == null)
        {
            return NotFound(new { status = "error", message = $"Không tìm thấy tài khoản \"{uname}\" trong hệ thống đào tạo!" });
        }

        if (!user.IsActive)
        {
            return StatusCode(403, new { status = "error", message = "Tài khoản này đang bị khóa, vui lòng liên hệ Phòng Đào tạo!" });
        }

        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var otpKey = $"FORGOT_PWD_OTP:{user.Username.ToUpperInvariant()}";

        var otpState = new OtpResetState
        {
            Username = user.Username,
            Code = otpCode,
            ExpiresAt = DateTimeOffset.UtcNow.Add(OtpExpiration),
            FailedVerifyCount = 0
        };
        _cache.Set(otpKey, otpState, OtpExpiration);

        var hoTen = user.GiangVien?.HoTen ?? user.SinhVien?.HoTen ?? user.Username;
        var roleName = user.Role?.RoleName ?? "SinhVien";

        var emailSection = _config.GetSection("AcademicEmail");
        var smtpUser = emailSection["Username"];
        if (string.IsNullOrWhiteSpace(smtpUser))
        {
            smtpUser = OtpTargetEmail;
        }
        var smtpPassword = emailSection["AppPassword"] ?? _config["AcademicEmail:AppPassword"] ?? _config["AppPassword"];
        var smtpHost = emailSection["Host"] ?? "smtp.gmail.com";
        var smtpPort = emailSection.GetValue("Port", 587);

        if (string.IsNullOrWhiteSpace(smtpPassword))
        {
            return StatusCode(500, new
            {
                status = "error",
                message = "Chưa cấu hình AppPassword cho dịch vụ gửi Email trong User Secrets (AcademicEmail:AppPassword)!"
            });
        }

        try
        {
            using var mail = new MailMessage(
                new MailAddress(smtpUser, "Cổng Học Vụ Số - Academic Ledger"),
                new MailAddress(OtpTargetEmail))
            {
                Subject = $"[Academic Ledger] Mã xác thực OTP khôi phục mật khẩu ({otpCode})",
                IsBodyHtml = true,
                Body = $@"
                <div style='font-family: Inter, Segoe UI, Arial, sans-serif; max-width: 560px; margin: 0 auto; border: 1px solid #cbd5e1; border-top: 4px solid #0f3d75; border-radius: 8px; background: #ffffff; color: #0f172a; padding: 28px;'>
                    <div style='font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; color: #0f3d75; margin-bottom: 6px;'>
                        HỆ THỐNG QUẢN LÝ ĐÀO TẠO &amp; ĐIỂM SỐ ACADEMIC LEDGER
                    </div>
                    <h2 style='margin: 0 0 16px 0; font-size: 20px; color: #0f172a;'>Yêu cầu Đặt lại Mật khẩu</h2>
                    <p style='font-size: 14px; line-height: 1.6; color: #334155; margin: 0 0 16px 0;'>
                        Hệ thống vừa nhận được yêu cầu khôi phục mật khẩu cho tài khoản đào tạo:
                    </p>
                    <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 13px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px;'>
                        <tr>
                            <td style='padding: 8px 12px; color: #64748b; width: 130px;'>Tài khoản / Mã số:</td>
                            <td style='padding: 8px 12px; font-weight: 700; color: #0f172a; font-family: monospace;'>{WebUtility.HtmlEncode(user.Username)}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px 12px; color: #64748b; border-top: 1px solid #e2e8f0;'>Họ và tên:</td>
                            <td style='padding: 8px 12px; font-weight: 600; color: #0f172a; border-top: 1px solid #e2e8f0;'>{WebUtility.HtmlEncode(hoTen)} ({WebUtility.HtmlEncode(roleName)})</td>
                        </tr>
                    </table>
                    <div style='text-align: center; margin: 24px 0;'>
                        <div style='font-size: 12px; color: #64748b; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.06em;'>Mã xác thực OTP của bạn</div>
                        <div style='display: inline-block; padding: 14px 28px; background: #0f3d75; color: #ffffff; font-size: 28px; font-weight: 800; letter-spacing: 8px; border-radius: 8px; font-family: monospace;'>
                            {otpCode}
                        </div>
                    </div>
                    <p style='font-size: 13px; color: #475569; line-height: 1.5; margin-bottom: 0;'>
                        Mã OTP có hiệu lực trong vòng <strong>05 phút</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.
                    </p>
                </div>"
            };

            using var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpUser, smtpPassword)
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await smtp.SendMailAsync(mail, cts.Token);

            GradeHelper.GhiNhatKy(_db, user.Username, "Yêu cầu OTP Quên mật khẩu", $"Đã gửi mã OTP đặt lại mật khẩu cho tài khoản {user.Username} tới {OtpTargetEmail}.");

            return Ok(new
            {
                status = "success",
                targetEmail = OtpTargetEmail,
                expiresInSeconds = (int)OtpExpiration.TotalSeconds,
                message = $"Mã xác thực OTP (6 chữ số) đã được gửi tới email {OtpTargetEmail}!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi email OTP quên mật khẩu cho {Username}", user.Username);
            return StatusCode(500, new
            {
                status = "error",
                message = "Không thể kết nối máy chủ SMTP để gửi mã OTP. Vui lòng kiểm tra kết nối mạng hoặc cấu hình AppPassword!"
            });
        }
    }

    public class ResetPasswordWithOtpDto
    {
        public string? Username { get; set; }
        public string? OtpCode { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    [HttpPost("forgot-password/reset")]
    public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpDto req)
    {
        var uname = (req?.Username ?? "").Trim();
        var otp = (req?.OtpCode ?? "").Trim();
        var newPwd = req?.NewPassword ?? "";
        var confirmPwd = req?.ConfirmPassword ?? "";

        if (string.IsNullOrEmpty(uname) || string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(newPwd))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập đầy đủ Tên đăng nhập, Mã OTP và Mật khẩu mới!" });
        }

        if (newPwd.Length < 6)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu mới phải có độ dài tối thiểu 6 ký tự!" });
        }

        if (!string.IsNullOrEmpty(confirmPwd) && newPwd != confirmPwd)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu xác nhận không khớp với mật khẩu mới!" });
        }

        var otpKey = $"FORGOT_PWD_OTP:{uname.ToUpperInvariant()}";
        if (!_cache.TryGetValue(otpKey, out OtpResetState? otpState) || otpState == null || otpState.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest(new { status = "error", message = "Mã OTP đã hết hạn hoặc chưa được khởi tạo. Vui lòng nhấn Gửi lại mã OTP!" });
        }

        if (!string.Equals(otpState.Code, otp, StringComparison.Ordinal))
        {
            otpState.FailedVerifyCount++;
            if (otpState.FailedVerifyCount >= 5)
            {
                _cache.Remove(otpKey);
                return BadRequest(new { status = "error", message = "Bạn đã nhập sai mã OTP quá 5 lần. Mã OTP đã bị hủy, vui lòng yêu cầu mã mới!" });
            }

            _cache.Set(otpKey, otpState, otpState.ExpiresAt - DateTimeOffset.UtcNow);
            return BadRequest(new
            {
                status = "error",
                message = $"Mã xác thực OTP không chính xác! (Còn {5 - otpState.FailedVerifyCount} lần thử)"
            });
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == uname);
        if (user == null)
        {
            return NotFound(new { status = "error", message = "Không tìm thấy tài khoản người dùng!" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPwd);
        await _db.SaveChangesAsync();

        // Xóa OTP & mở khóa đăng nhập nếu tài khoản đang bị tạm khóa
        _cache.Remove(otpKey);
        _cache.Remove(GetClientKey(user.Username));

        GradeHelper.GhiNhatKy(_db, user.Username, "Đặt lại mật khẩu qua OTP", $"Người dùng {user.Username} đã đặt lại mật khẩu thành công qua xác thực OTP email ({OtpTargetEmail}).");

        return Ok(new
        {
            status = "success",
            message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay bằng mật khẩu mới."
        });
    }


    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var uname = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!string.IsNullOrEmpty(uname))
        {
            GradeHelper.GhiNhatKy(_db, uname, "Đăng xuất", $"Người dùng {uname} đã đăng xuất khỏi hệ thống.");
        }
        return Ok(new { status = "success", message = "Đăng xuất thành công!" });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { status = "error", message = "Chưa đăng nhập" });
        }

        var uname = User.Identity.Name;
        var user = await _db.UserAccounts
            .Include(u => u.Role)
            .Include(u => u.GiangVien)
            .Include(u => u.SinhVien)
            .FirstOrDefaultAsync(u => u.Username == uname);

        if (user == null)
        {
            return Unauthorized(new { status = "error", message = "Không tìm thấy người dùng" });
        }

        string roleName = user.Role?.RoleName ?? "SinhVien";
        string? maGV = user.GiangVien?.MaGV ?? user.MaGV;
        string? maSV = user.SinhVien?.MaSV;
        if (roleName == "SinhVien" && string.IsNullOrEmpty(maSV)) maSV = user.Username;
        if (roleName == "GiangVien" && string.IsNullOrEmpty(maGV)) maGV = user.Username;
        string hoTen = user.GiangVien?.HoTen ?? user.SinhVien?.HoTen ?? (roleName == "Admin" ? "Quản trị viên Hệ thống" : user.Username);

        var permissions = await _db.PhanQuyens
            .Where(p => p.RoleID == user.RoleID)
            .Select(p => new { p.MaChucNang, p.MaHanhDong })
            .ToListAsync();

        var scopes = await _db.PhanQuyenPhamVis
            .Where(s => s.RoleID == user.RoleID && s.IsActive)
            .Select(s => s.MaPhamVi)
            .ToListAsync();

        return Ok(new
        {
            status = "success",
            data = new
            {
                userId = user.UserID,
                username = user.Username,
                role = roleName,
                roleId = user.RoleID,
                hoTen = hoTen,
                maGV = maGV,
                maSV = maSV,
                permissions,
                scopes
            }
        });
    }

    public class VerifyPasswordDto
    {
        public string? Password { get; set; }
    }

    [HttpPost("verify-password"), EnableRateLimiting("TranscriptVerification")]
    public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordDto req)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { status = "error", message = "Bạn chưa đăng nhập!" });
        }

        var uname = User.Identity.Name;
        var pwd = req?.Password ?? "";
        if (string.IsNullOrEmpty(pwd))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập mật khẩu xác thực!" });
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == uname);
        if (user == null)
        {
            return Unauthorized(new { status = "error", message = "Tài khoản không tồn tại!" });
        }

        bool validPassword = false;
        try
        {
            if (user.PasswordHash.StartsWith("$2"))
            {
                validPassword = BCrypt.Net.BCrypt.Verify(pwd, user.PasswordHash);
            }
            else
            {
                validPassword = user.PasswordHash == pwd;
            }
        }
        catch
        {
            validPassword = user.PasswordHash == pwd;
        }

        if (!validPassword)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu xác thực không chính xác!" });
        }

        return Ok(new { status = "success", message = "Mật khẩu chính xác" });
    }
}

