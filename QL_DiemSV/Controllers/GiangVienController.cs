using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GiangVienController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GiangVienController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maKhoa, [FromQuery] string? sort = null)
    {
        var query = _db.GiangViens.AsNoTracking()
            .Include(g => g.Khoa)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            query = query.Where(g => g.MaKhoa == maKhoa);
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "newest" => query.OrderByDescending(g => g.UserID).ThenByDescending(g => g.MaGV),
            "oldest" => query.OrderBy(g => g.UserID).ThenBy(g => g.MaGV),
            "magv_desc" => query.OrderByDescending(g => g.MaGV),
            "hoten_asc" => query.OrderBy(g => g.HoTen).ThenBy(g => g.MaGV),
            "hoten_desc" => query.OrderByDescending(g => g.HoTen).ThenByDescending(g => g.MaGV),
            "hocvi_asc" => query.OrderBy(g => g.HocVi).ThenBy(g => g.MaGV),
            "hocvi_desc" => query.OrderByDescending(g => g.HocVi).ThenByDescending(g => g.MaGV),
            "khoa_asc" => query.OrderBy(g => g.Khoa!.TenKhoa).ThenBy(g => g.MaGV),
            "khoa_desc" => query.OrderByDescending(g => g.Khoa!.TenKhoa).ThenByDescending(g => g.MaGV),
            "email_asc" => query.OrderBy(g => g.Email).ThenBy(g => g.MaGV),
            "email_desc" => query.OrderByDescending(g => g.Email).ThenByDescending(g => g.MaGV),
            _ => query.OrderBy(g => g.MaGV)
        };

        var data = await query.ToListAsync();
        return Ok(new { status = "success", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.GiangViens
            .Include(g => g.Khoa)
            .FirstOrDefaultAsync(g => g.MaGV == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] GiangVien model)
    {
        if (string.IsNullOrWhiteSpace(model.MaGV) || string.IsNullOrWhiteSpace(model.HoTen))
            return BadRequest(new { status = "error", message = "Mã giảng viên và Họ tên không được để trống" });

        model.MaGV = model.MaGV.Trim();
        if (await _db.GiangViens.AnyAsync(g => g.MaGV == model.MaGV))
            return BadRequest(new { status = "error", message = $"Mã giảng viên {model.MaGV} đã tồn tại!" });

        // Tự động tạo tài khoản đăng nhập cho Giảng viên (mật khẩu mặc định 123456)
        var gvRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "GiangVien");
        var existingAcc = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == model.MaGV);
        if (existingAcc == null)
        {
            existingAcc = new UserAccount
            {
                Username = model.MaGV,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                RoleID = gvRole?.RoleID ?? 2,
                IsActive = true,
                MaGV = model.MaGV,
                NgayTao = DateTime.Now
            };
            _db.UserAccounts.Add(existingAcc);
            await _db.SaveChangesAsync();
        }

        model.UserID = existingAcc.UserID;
        if (string.IsNullOrWhiteSpace(model.MaKhoa)) model.MaKhoa = null;

        _db.GiangViens.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Giảng viên", $"Đã thêm giảng viên {model.MaGV} - {model.HoTen}");
        return Ok(new { status = "success", message = "Thêm giảng viên và tạo tài khoản thành công", data = model });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<GiangVien> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách giảng viên trống" });

        var gvRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "GiangVien");
        int roleId = gvRole?.RoleID ?? 2;
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var existingGvIds = (await _db.GiangViens.AsNoTracking().Select(g => g.MaGV).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAccounts = await _db.UserAccounts
            .ToDictionaryAsync(u => u.Username, StringComparer.OrdinalIgnoreCase);

        var newAccounts = new List<UserAccount>();
        var pendingLecturers = new List<(GiangVien gv, UserAccount acc)>();

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaGV)) continue;
            item.MaGV = item.MaGV.Trim();
            if (existingGvIds.Contains(item.MaGV)) continue;

            existingGvIds.Add(item.MaGV);
            if (!existingAccounts.TryGetValue(item.MaGV, out var ua))
            {
                ua = new UserAccount
                {
                    Username = item.MaGV,
                    PasswordHash = defaultHash,
                    RoleID = roleId,
                    IsActive = true,
                    MaGV = item.MaGV,
                    NgayTao = DateTime.Now
                };
                newAccounts.Add(ua);
                existingAccounts[item.MaGV] = ua;
            }

            if (string.IsNullOrWhiteSpace(item.MaKhoa)) item.MaKhoa = null;
            pendingLecturers.Add((item, ua));
        }

        if (newAccounts.Count > 0)
        {
            _db.UserAccounts.AddRange(newAccounts);
            await _db.SaveChangesAsync();
        }

        foreach (var (gv, ua) in pendingLecturers)
        {
            gv.UserID = ua.UserID;
            _db.GiangViens.Add(gv);
        }

        int count = pendingLecturers.Count;
        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Giảng viên", $"Đã import {count} giảng viên từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} giảng viên" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] GiangVien model)
    {
        var existing = await _db.GiangViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });

        existing.HoTen = model.HoTen;
        existing.NgaySinh = model.NgaySinh;
        existing.GioiTinh = model.GioiTinh;
        existing.Email = model.Email;
        existing.SDT = model.SDT;
        existing.CCCD = model.CCCD;
        existing.HocVi = model.HocVi;
        existing.MaKhoa = string.IsNullOrWhiteSpace(model.MaKhoa) ? null : model.MaKhoa;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Giảng viên", $"Đã cập nhật giảng viên {id}");
        return Ok(new { status = "success", message = "Cập nhật giảng viên thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.GiangViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });

        if (await _db.ClassSchedules.AnyAsync(c => c.MaGV == id))
        {
            return BadRequest(new { status = "error", message = $"Không thể xóa giảng viên {id} vì đang có buổi học được phân công trong Thời khóa biểu! Vui lòng đổi giảng viên cho các buổi học đó trước." });
        }

        // Gỡ liên kết lớp học phần và lớp hành chính trước khi xóa
        var lhps = await _db.LopHocPhans.Where(l => l.MaGV == id).ToListAsync();
        foreach (var l in lhps) l.MaGV = null;

        var lhcs = await _db.LopHanhChinhs.Where(l => l.MaGVCN == id).ToListAsync();
        foreach (var l in lhcs) l.MaGVCN = null;

        int? userId = existing.UserID;
        _db.GiangViens.Remove(existing);
        await _db.SaveChangesAsync();

        if (userId != null)
        {
            var acc = await _db.UserAccounts.FindAsync(userId);
            if (acc != null)
            {
                _db.UserAccounts.Remove(acc);
                await _db.SaveChangesAsync();
            }
        }

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Giảng viên", $"Đã xóa giảng viên {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa giảng viên thành công" });
    }
}
