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
    public async Task<IActionResult> GetAll([FromQuery] string? role, [FromQuery] string? status)
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

        var list = await query.OrderBy(u => u.UserID).ToListAsync();
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

        return Ok(new { status = "success", data });
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaiKhoanDto req)
    {
        if (string.IsNullOrWhiteSpace(req.username) || string.IsNullOrWhiteSpace(req.password))
            return BadRequest(new { status = "error", message = "Tên đăng nhập và mật khẩu không được để trống!" });

        var uname = req.username.Trim();
        if (await _db.UserAccounts.AnyAsync(u => u.Username == uname))
            return BadRequest(new { status = "error", message = $"Tài khoản {uname} đã tồn tại!" });

        var roleId = req.roleId ?? await _db.Roles.Where(r => r.RoleName == "SinhVien").Select(r => r.RoleID).SingleAsync();
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

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Tạo tài khoản", $"Đã tạo tài khoản {uname}");
        return Ok(new { status = "success", message = "Thêm tài khoản thành công!" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaiKhoanDto req)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var ua = await _db.UserAccounts.FindAsync(id);
        if (ua == null) return NotFound(new { status = "error", message = "Không tìm thấy tài khoản" });

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

