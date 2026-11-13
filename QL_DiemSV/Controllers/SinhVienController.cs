using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien,KhaoThi,GiaoVuKhoa")]
public class SinhVienController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SinhVienController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maLop, int page = 1, int pageSize = 20,
        string? search = null, string? maNganh = null, string? maKhoa = null, string? status = null, string? cohort = null, string? sort = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = await StudentLookupScope.StudentsAsync(_db, User);
        var canViewIdentity = User.IsInRole("Admin");
        if (!string.IsNullOrWhiteSpace(maLop)) query = query.Where(s => s.MaLop == maLop);
        if (!string.IsNullOrWhiteSpace(maNganh)) query = query.Where(s => s.LopHanhChinh!.MaNganh == maNganh);
        if (!string.IsNullOrWhiteSpace(maKhoa)) query = query.Where(s => s.LopHanhChinh!.Nganh!.MaKhoa == maKhoa);
        if (!string.IsNullOrWhiteSpace(cohort)) query = query.Where(s => s.LopHanhChinh!.NienKhoa == cohort);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(s => s.MaSV.Contains(search) || s.HoTen.Contains(search)
                || (canViewIdentity && s.Email != null && s.Email.Contains(search)));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "newest" => query.OrderByDescending(s => s.UserID).ThenByDescending(s => s.MaSV),
            "oldest" => query.OrderBy(s => s.UserID).ThenBy(s => s.MaSV),
            "masv_desc" => query.OrderByDescending(s => s.MaSV),
            "hoten_asc" => query.OrderBy(s => s.HoTen).ThenBy(s => s.MaSV),
            "hoten_desc" => query.OrderByDescending(s => s.HoTen).ThenByDescending(s => s.MaSV),
            "gioitinh_asc" => query.OrderBy(s => s.GioiTinh).ThenBy(s => s.MaSV),
            "gioitinh_desc" => query.OrderByDescending(s => s.GioiTinh).ThenByDescending(s => s.MaSV),
            "ngaysinh_asc" => query.OrderBy(s => s.NgaySinh).ThenBy(s => s.MaSV),
            "ngaysinh_desc" => query.OrderByDescending(s => s.NgaySinh).ThenByDescending(s => s.MaSV),
            "malop_asc" => query.OrderBy(s => s.MaLop).ThenBy(s => s.MaSV),
            "malop_desc" => query.OrderByDescending(s => s.MaLop).ThenByDescending(s => s.MaSV),
            "nganh_asc" => query.OrderBy(s => s.LopHanhChinh!.Nganh!.TenNganh).ThenBy(s => s.MaSV),
            "nganh_desc" => query.OrderByDescending(s => s.LopHanhChinh!.Nganh!.TenNganh).ThenByDescending(s => s.MaSV),
            "trangthai_asc" => query.OrderBy(s => s.TrangThai).ThenBy(s => s.MaSV),
            "trangthai_desc" => query.OrderByDescending(s => s.TrangThai).ThenByDescending(s => s.MaSV),
            _ => query.OrderBy(s => s.MaSV)
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.MaSV, s.HoTen, s.NgaySinh, s.GioiTinh, s.MaLop, s.TrangThai,
                Email = canViewIdentity ? s.Email : null, SDT = canViewIdentity ? s.SDT : null,
                CCCD = canViewIdentity ? s.CCCD : null, DiaChi = canViewIdentity ? s.DiaChi : null,
                LopHanhChinh = s.LopHanhChinh == null ? null : new { s.LopHanhChinh.MaLop, s.LopHanhChinh.TenLop, s.LopHanhChinh.MaNganh, s.LopHanhChinh.NienKhoa }
            }).ToListAsync();
        return Ok(new { status = "success", data, total, page, pageSize, totalPages });
    }

    [Authorize(Roles = "Admin"), HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var students = await StudentLookupScope.StudentsAsync(_db, User);
        var item = await students
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.Khoa)
            .FirstOrDefaultAsync(s => s.MaSV == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
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

    [HttpPost("bulk/existing")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetExistingImportCodes([FromBody] List<string> codes, CancellationToken cancellationToken)
    {
        if (codes.Count > 1000)
            return BadRequest(new { message = "Tối đa 1.000 mã sinh viên mỗi lần kiểm tra." });

        var data = await _db.SinhViens.AsNoTracking()
            .Where(s => codes.Contains(s.MaSV))
            .Select(s => s.MaSV).ToListAsync(cancellationToken);
        return Ok(new { data });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<SinhVien> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách sinh viên trống" });

        var svRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "SinhVien");
        int roleId = svRole?.RoleID ?? 3;
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var existingSvIds = (await _db.SinhViens.AsNoTracking().Select(s => s.MaSV).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAccounts = await _db.UserAccounts
            .ToDictionaryAsync(u => u.Username, StringComparer.OrdinalIgnoreCase);

        var newAccounts = new List<UserAccount>();
        var pendingStudents = new List<(SinhVien sv, UserAccount acc)>();

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaSV)) continue;
            item.MaSV = item.MaSV.Trim();
            if (existingSvIds.Contains(item.MaSV)) continue;

            existingSvIds.Add(item.MaSV);
            if (!existingAccounts.TryGetValue(item.MaSV, out var ua))
            {
                ua = new UserAccount
                {
                    Username = item.MaSV,
                    PasswordHash = defaultHash,
                    RoleID = roleId,
                    IsActive = true,
                    NgayTao = DateTime.Now
                };
                newAccounts.Add(ua);
                existingAccounts[item.MaSV] = ua;
            }

            if (string.IsNullOrWhiteSpace(item.MaLop)) item.MaLop = null;
            if (string.IsNullOrWhiteSpace(item.TrangThai)) item.TrangThai = "Đang học";
            pendingStudents.Add((item, ua));
        }

        if (newAccounts.Count > 0)
        {
            _db.UserAccounts.AddRange(newAccounts);
            await _db.SaveChangesAsync();
        }

        foreach (var (sv, ua) in pendingStudents)
        {
            sv.UserID = ua.UserID;
            _db.SinhViens.Add(sv);
        }

        int count = pendingStudents.Count;
        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Sinh viên", $"Đã import {count} sinh viên từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} sinh viên" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.SinhViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });

        if (await _db.KetQuaHocTaps.AnyAsync(k => k.MaSV == id && (k.DiemTongKet != null || k.DiemCK != null || k.DiemThiLan1 != null)))
        {
            return BadRequest(new { status = "error", message = $"Không thể xóa sinh viên {id} vì đã phát sinh kết quả điểm học tập trên hệ thống! Vui lòng chuyển trạng thái sang 'Thôi học' hoặc 'Bảo lưu'." });
        }

        var emptyEnrollments = await _db.KetQuaHocTaps.Where(k => k.MaSV == id).ToListAsync();
        if (emptyEnrollments.Count > 0)
        {
            _db.KetQuaHocTaps.RemoveRange(emptyEnrollments);
        }

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
