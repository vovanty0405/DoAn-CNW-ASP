using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuthController(ApplicationDbContext db)
    {
        _db = db;
    }

    public class LoginDto
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
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

        var user = await _db.UserAccounts
            .Include(u => u.Role)
            .Include(u => u.GiangVien)
            .Include(u => u.SinhVien)
            .FirstOrDefaultAsync(u => u.Username == uname);

        if (user == null)
        {
            return Unauthorized(new { status = "error", message = "Tài khoản không tồn tại trong hệ thống!" });
        }

        if (!user.IsActive)
        {
            return StatusCode(403, new { status = "error", message = "Tài khoản của bạn đã bị khóa!" });
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
            return Unauthorized(new { status = "error", message = "Mật khẩu không chính xác!" });
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

