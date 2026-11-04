using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SinhVienController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SinhVienController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maLop, int page = 1, int pageSize = 20,
        string? search = null, string? maNganh = null, string? maKhoa = null, string? status = null, string? cohort = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.SinhViens.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(maLop)) query = query.Where(s => s.MaLop == maLop);
        if (!string.IsNullOrWhiteSpace(maNganh)) query = query.Where(s => s.LopHanhChinh!.MaNganh == maNganh);
        if (!string.IsNullOrWhiteSpace(maKhoa)) query = query.Where(s => s.LopHanhChinh!.Nganh!.MaKhoa == maKhoa);
        if (!string.IsNullOrWhiteSpace(cohort)) query = query.Where(s => s.LopHanhChinh!.NienKhoa == cohort);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(s => s.MaSV.Contains(search) || s.HoTen.Contains(search) || (s.Email != null && s.Email.Contains(search)));
        }
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var data = await query.OrderBy(s => s.MaSV).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.MaSV, s.HoTen, s.NgaySinh, s.GioiTinh, s.MaLop, s.Email, s.SDT, s.CCCD, s.DiaChi, s.TrangThai,
                LopHanhChinh = s.LopHanhChinh == null ? null : new { s.LopHanhChinh.MaLop, s.LopHanhChinh.TenLop, s.LopHanhChinh.MaNganh, s.LopHanhChinh.NienKhoa }
            }).ToListAsync();
        return Ok(new { status = "success", data, total, page, pageSize, totalPages });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.SinhViens
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.Khoa)
            .FirstOrDefaultAsync(s => s.MaSV == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SinhVien model)
    {
        if (string.IsNullOrWhiteSpace(model.MaSV) || string.IsNullOrWhiteSpace(model.HoTen))
            return BadRequest(new { status = "error", message = "Mã sinh viên và Họ tên không được để trống" });

        model.MaSV = model.MaSV.Trim();
        if (await _db.SinhViens.AnyAsync(s => s.MaSV == model.MaSV))
            return BadRequest(new { status = "error", message = $"Mã sinh viên {model.MaSV} đã tồn tại!" });

        var svRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "SinhVien");
        var existingAcc = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == model.MaSV);
        if (existingAcc == null)
        {
            existingAcc = new UserAccount
            {
                Username = model.MaSV,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                RoleID = svRole?.RoleID ?? 3,
                IsActive = true,
                NgayTao = DateTime.Now
            };
            _db.UserAccounts.Add(existingAcc);
            await _db.SaveChangesAsync();
        }

        model.UserID = existingAcc.UserID;
        if (string.IsNullOrWhiteSpace(model.MaLop)) model.MaLop = null;
        if (string.IsNullOrWhiteSpace(model.TrangThai)) model.TrangThai = "Đang học";

        _db.SinhViens.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Sinh viên", $"Đã thêm sinh viên {model.MaSV} - {model.HoTen}");
        return Ok(new { status = "success", message = "Thêm sinh viên và tạo tài khoản thành công", data = model });
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] List<SinhVien> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách sinh viên trống" });

        var svRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "SinhVien");
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");
        int count = 0;

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaSV) || await _db.SinhViens.AnyAsync(s => s.MaSV == item.MaSV))
                continue;

            var ua = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == item.MaSV);
            if (ua == null)
            {
                ua = new UserAccount
                {
                    Username = item.MaSV.Trim(),
                    PasswordHash = defaultHash,
                    RoleID = svRole?.RoleID ?? 3,
                    IsActive = true,
                    NgayTao = DateTime.Now
                };
                _db.UserAccounts.Add(ua);
                await _db.SaveChangesAsync();
            }

            item.UserID = ua.UserID;
            if (string.IsNullOrWhiteSpace(item.MaLop)) item.MaLop = null;
            if (string.IsNullOrWhiteSpace(item.TrangThai)) item.TrangThai = "Đang học";
            _db.SinhViens.Add(item);
            count++;
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Sinh viên", $"Đã import {count} sinh viên từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} sinh viên" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SinhVien model)
    {
        var existing = await _db.SinhViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });

        existing.HoTen = model.HoTen;
        existing.NgaySinh = model.NgaySinh;
        existing.GioiTinh = model.GioiTinh;
        existing.DiaChi = model.DiaChi;
        existing.CCCD = model.CCCD;
        existing.Email = model.Email;
        existing.SDT = model.SDT;
        existing.MaLop = string.IsNullOrWhiteSpace(model.MaLop) ? null : model.MaLop;
        existing.TrangThai = model.TrangThai;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Sinh viên", $"Đã cập nhật sinh viên {id}");
        return Ok(new { status = "success", message = "Cập nhật sinh viên thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.SinhViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });

        int? userId = existing.UserID;
        _db.SinhViens.Remove(existing);
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

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Sinh viên", $"Đã xóa sinh viên {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa sinh viên thành công" });
    }
}
