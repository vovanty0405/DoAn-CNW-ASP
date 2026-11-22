using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class TaiKhoanController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TaiKhoanController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.UserAccounts.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.GiangVien)
            .Include(u => u.SinhVien)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role) && int.TryParse(role, out int rId))
        {
            query = query.Where(u => u.RoleID == rId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "active") query = query.Where(u => u.IsActive);
            else if (status == "inactive") query = query.Where(u => !u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(u => u.Username.Contains(kw)
                || (u.MaGV != null && u.MaGV.Contains(kw))
                || (u.GiangVien != null && (u.GiangVien.MaGV.Contains(kw) || u.GiangVien.HoTen.Contains(kw)))
                || (u.SinhVien != null && (u.SinhVien.MaSV.Contains(kw) || u.SinhVien.HoTen.Contains(kw))));
        }

        var ordered = query.OrderBy(u => u.UserID);

        int total = 0;
        int totalPages = 1;
        int currentPage = 1;
        List<UserAccount> list;

        if (page.HasValue)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            total = await ordered.CountAsync();
            totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
            currentPage = Math.Clamp(page.Value, 1, totalPages);
            list = await ordered.Skip((currentPage - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        else
        {
            list = await ordered.ToListAsync();
            total = list.Count;
        }

        var data = list.Select(u =>
        {
            string refObj = "Quản trị hệ thống";
            string rawRefType = "";
            string rawRefId = "";
            if (u.GiangVien != null)
            {
                refObj = $"GV: {u.GiangVien.MaGV} - {u.GiangVien.HoTen}";
                rawRefType = "Giảng Viên";
                rawRefId = u.GiangVien.MaGV;
            }
            else if (u.SinhVien != null)
            {
                refObj = $"SV: {u.SinhVien.MaSV} - {u.SinhVien.HoTen}";
                rawRefType = "Sinh Viên";
                rawRefId = u.SinhVien.MaSV;
            }
            else if (!string.IsNullOrEmpty(u.MaGV))
            {
                refObj = $"GV: {u.MaGV}";
                rawRefType = "Giảng Viên";
                rawRefId = u.MaGV;
            }

            return new
            {
                id = u.UserID,
                UserID = u.UserID,
                username = u.Username,
                roleId = u.RoleID,
                maKhoa = u.MaKhoa,
                role = u.Role?.RoleName ?? "Chưa phân quyền",
                refId = refObj,
                rawRefType,
                rawRefId,
                status = u.IsActive,
                IsActive = u.IsActive,
                NgayTao = u.NgayTao
            };
        });

        return Ok(new { status = "success", data, total, page = currentPage, pageSize, totalPages });
    }

    public class TaiKhoanDto
    {
        public string? username { get; set; }
        public string? password { get; set; }
        public int? roleId { get; set; }
        public string? maKhoa { get; set; }
        public string? refType { get; set; }
        public string? refId { get; set; }
        public bool isActive { get; set; } = true;
    }

    private async Task<string?> ValidateAssignment(int? roleId, string? faculty)
    {
        var role = await _db.Roles.FindAsync(roleId ?? 0);
        if (role == null) return "Nhóm quyền không tồn tại.";
        if (!string.IsNullOrWhiteSpace(faculty) && !await _db.Khoas.AnyAsync(k => k.MaKhoa == faculty))
            return "Khoa được chọn không tồn tại.";
        return null;
    }

    private async Task<string?> ValidateAccountInputAsync(TaiKhoanDto req, int? accountId = null)
    {
        req.username = ProfileValidation.Optional(req.username);
        req.refId = ProfileValidation.Optional(req.refId);
        if (req.username is { Length: > 50 }) return "Tên đăng nhập tối đa 50 ký tự.";
        if (!string.IsNullOrEmpty(req.password) && (string.IsNullOrWhiteSpace(req.password)
            || System.Text.Encoding.UTF8.GetByteCount(req.password) > 72))
            return "Mật khẩu không được chỉ chứa khoảng trắng hoặc vượt quá 72 byte UTF-8.";
        if (req.username != null && await _db.UserAccounts.AnyAsync(u => u.Username == req.username && u.UserID != accountId))
            return "Tên đăng nhập đã tồn tại.";
        if (req.refType is not (null or "" or "Sinh Viên" or "Giảng Viên"))
            return "Loại hồ sơ liên kết không hợp lệ.";
        if (req.refId == null)
            return req.refType is "Sinh Viên" or "Giảng Viên" ? "Cần chọn mã hồ sơ liên kết." : null;
        if (req.refType == "Sinh Viên" && await _db.SinhViens.AnyAsync(s => s.MaSV == req.refId && (s.UserID == null || s.UserID == accountId)))
            return null;
        if (req.refType == "Giảng Viên" && await _db.GiangViens.AnyAsync(g => g.MaGV == req.refId && (g.UserID == null || g.UserID == accountId)))
            return null;
        return "Hồ sơ không tồn tại hoặc đã liên kết với tài khoản khác.";
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaiKhoanDto req)
    {
        if (string.IsNullOrWhiteSpace(req.username) || string.IsNullOrWhiteSpace(req.password))
            return BadRequest(new { status = "error", message = "Tên đăng nhập và mật khẩu không được để trống!" });

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var inputError = await ValidateAccountInputAsync(req);
        if (inputError != null) return BadRequest(new { message = inputError });
        var uname = req.username!;

        var roleId = req.roleId ?? await _db.Roles.Where(r => r.RoleName == "SinhVien").Select(r => (int?)r.RoleID).SingleOrDefaultAsync();
        var faculty = string.IsNullOrWhiteSpace(req.maKhoa) ? null : req.maKhoa.Trim();
        var error = await ValidateAssignment(roleId, faculty);
        if (error != null) return BadRequest(new { message = error });

        var ua = new UserAccount
        {
            Username = uname,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.password),
            RoleID = roleId,
            MaKhoa = faculty,
            IsActive = req.isActive,
            MaGV = req.refType == "Giảng Viên" ? req.refId : null,
            NgayTao = DateTime.Now
        };

        _db.UserAccounts.Add(ua);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(req.refId))
        {
            if (req.refType == "Giảng Viên")
            {
                var gv = await _db.GiangViens.FindAsync(req.refId.Trim());
                if (gv != null) gv.UserID = ua.UserID;
            }
            else if (req.refType == "Sinh Viên")
            {
                var sv = await _db.SinhViens.FindAsync(req.refId.Trim());
                if (sv != null) sv.UserID = ua.UserID;
            }
            await _db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Tạo tài khoản", $"Đã tạo tài khoản {uname}");
        return Ok(new { status = "success", message = "Thêm tài khoản thành công!" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaiKhoanDto req)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var ua = await _db.UserAccounts.FindAsync(id);
        if (ua == null) return NotFound(new { status = "error", message = "Không tìm thấy tài khoản" });

        var inputError = await ValidateAccountInputAsync(req, id);
        if (inputError != null) return BadRequest(new { message = inputError });

        var faculty = req.maKhoa == null ? ua.MaKhoa : (string.IsNullOrWhiteSpace(req.maKhoa) ? null : req.maKhoa.Trim());
        var error = await ValidateAssignment(req.roleId ?? ua.RoleID, faculty);
        if (error != null) return BadRequest(new { message = error });
        var adminRole = await _db.Roles.AsNoTracking().Where(r => r.RoleName == "Admin")
            .Select(r => (int?)r.RoleID).SingleOrDefaultAsync();
        if (adminRole != null && ua.IsActive && ua.RoleID == adminRole
            && (!req.isActive || (req.roleId.HasValue && req.roleId != adminRole))
            && !await _db.UserAccounts.AnyAsync(u => u.UserID != id && u.RoleID == adminRole && u.IsActive))
            return Conflict(new { message = "Phải giữ ít nhất một tài khoản quản trị đang hoạt động." });

        ua.MaKhoa = faculty;
        if (!string.IsNullOrWhiteSpace(req.username)) ua.Username = req.username.Trim();
        if (!string.IsNullOrWhiteSpace(req.password)) ua.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.password);
        if (req.roleId != null) ua.RoleID = req.roleId;
        ua.IsActive = req.isActive;

        if (req.refType != null)
        {
            var oldGv = await _db.GiangViens.FirstOrDefaultAsync(g => g.UserID == id);
            if (oldGv != null) oldGv.UserID = null;

            var oldSv = await _db.SinhViens.FirstOrDefaultAsync(s => s.UserID == id);
            if (oldSv != null) oldSv.UserID = null;

            ua.MaGV = null;
            if (!string.IsNullOrWhiteSpace(req.refId))
            {
                var targetCode = req.refId.Trim();
                if (req.refType == "Giảng Viên")
                {
                    ua.MaGV = targetCode;
                    var gv = await _db.GiangViens.FindAsync(targetCode);
                    if (gv != null) gv.UserID = ua.UserID;
                }
                else if (req.refType == "Sinh Viên")
                {
                    var sv = await _db.SinhViens.FindAsync(targetCode);
                    if (sv != null) sv.UserID = ua.UserID;
                }
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật tài khoản", $"Đã cập nhật tài khoản #{id} ({ua.Username})");
        return Ok(new { status = "success", message = "Cập nhật tài khoản thành công!" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var ua = await _db.UserAccounts.FindAsync(id);
        if (ua == null) return NotFound(new { status = "error", message = "Không tìm thấy tài khoản" });

        if (ua.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { status = "error", message = "Không thể xóa tài khoản Quản trị viên gốc!" });

        var adminRole = await _db.Roles.AsNoTracking().Where(r => r.RoleName == "Admin")
            .Select(r => (int?)r.RoleID).SingleOrDefaultAsync();
        if (adminRole != null && ua.IsActive && ua.RoleID == adminRole
            && !await _db.UserAccounts.AnyAsync(u => u.UserID != id && u.RoleID == adminRole && u.IsActive))
            return Conflict(new { message = "Phải giữ ít nhất một tài khoản quản trị đang hoạt động." });

        var gv = await _db.GiangViens.FirstOrDefaultAsync(g => g.UserID == id);
        if (gv != null) gv.UserID = null;

        var sv = await _db.SinhViens.FirstOrDefaultAsync(s => s.UserID == id);
        if (sv != null) sv.UserID = null;

        _db.UserAccounts.Remove(ua);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa tài khoản", $"Đã xóa tài khoản {ua.Username}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa tài khoản thành công!" });
    }
}

