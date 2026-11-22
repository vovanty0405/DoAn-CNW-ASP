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
public class LopHanhChinhController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LopHanhChinhController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? maNganh,
        [FromQuery] string? maGVCN = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? page = null,
        [FromQuery] int pageSize = 10)
    {
        var query = await StudentLookupScope.ClassesAsync(_db, User);
        if (!string.IsNullOrWhiteSpace(maNganh)) query = query.Where(l => l.MaNganh == maNganh);
        if (!string.IsNullOrWhiteSpace(maGVCN)) query = query.Where(l => l.MaGVCN == maGVCN);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(l => l.MaLop.Contains(kw) || l.TenLop.Contains(kw));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "malop_desc" => query.OrderByDescending(l => l.MaLop),
            "tenlop_asc" => query.OrderBy(l => l.TenLop).ThenBy(l => l.MaLop),
            "tenlop_desc" => query.OrderByDescending(l => l.TenLop).ThenByDescending(l => l.MaLop),
            "nienkhoa_desc" or "newest" => query.OrderByDescending(l => l.NienKhoa).ThenByDescending(l => l.MaLop),
            "nienkhoa_asc" or "oldest" => query.OrderBy(l => l.NienKhoa).ThenBy(l => l.MaLop),
            "nganh_asc" => query.OrderBy(l => l.Nganh!.TenNganh).ThenBy(l => l.MaLop),
            "nganh_desc" => query.OrderByDescending(l => l.Nganh!.TenNganh).ThenByDescending(l => l.MaLop),
            "gvcn_asc" => query.OrderBy(l => l.GiangVien!.HoTen).ThenBy(l => l.MaLop),
            "gvcn_desc" => query.OrderByDescending(l => l.GiangVien!.HoTen).ThenByDescending(l => l.MaLop),
            "siso_desc" => query.OrderByDescending(l => l.SinhViens.Count()).ThenBy(l => l.MaLop),
            "siso_asc" => query.OrderBy(l => l.SinhViens.Count()).ThenBy(l => l.MaLop),
            _ => query.OrderBy(l => l.MaLop)
        };

        if (page.HasValue)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            int total = await query.CountAsync();
            int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
            int currentPage = Math.Clamp(page.Value, 1, totalPages);

            var pagedData = await query.Skip((currentPage - 1) * pageSize).Take(pageSize)
                .Select(l => new
                {
                    l.MaLop, l.TenLop, l.NienKhoa, l.MaNganh, l.MaGVCN,
                    Nganh = l.Nganh == null ? null : new { l.Nganh.MaNganh, l.Nganh.TenNganh, l.Nganh.MaKhoa,
                        Khoa = l.Nganh.Khoa == null ? null : new { l.Nganh.Khoa.MaKhoa, l.Nganh.Khoa.TenKhoa } },
                    GiangVien = l.GiangVien == null ? null : new { l.GiangVien.MaGV, l.GiangVien.HoTen },
                    SiSo = l.SinhViens.Count()
                }).ToListAsync();

            return Ok(new { status = "success", data = pagedData, total, page = currentPage, pageSize, totalPages });
        }

        var data = await query.Select(l => new
        {
            l.MaLop, l.TenLop, l.NienKhoa, l.MaNganh, l.MaGVCN,
            Nganh = l.Nganh == null ? null : new { l.Nganh.MaNganh, l.Nganh.TenNganh, l.Nganh.MaKhoa,
                Khoa = l.Nganh.Khoa == null ? null : new { l.Nganh.Khoa.MaKhoa, l.Nganh.Khoa.TenKhoa } },
            GiangVien = l.GiangVien == null ? null : new { l.GiangVien.MaGV, l.GiangVien.HoTen },
            SiSo = l.SinhViens.Count()
        }).ToListAsync();

        return Ok(new { status = "success", data });
    }

    [Authorize(Roles = "Admin"), HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var classes = await StudentLookupScope.ClassesAsync(_db, User);
        var item = await classes.AsSplitQuery()
            .Include(l => l.Nganh)
                .ThenInclude(n => n!.Khoa)
            .Include(l => l.GiangVien)
            .Include(l => l.SinhViens)
            .FirstOrDefaultAsync(l => l.MaLop == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp hành chính" });

        return Ok(new
        {
            status = "success",
            data = new
            {
                item.MaLop,
                item.TenLop,
                item.NienKhoa,
                item.MaNganh,
                item.MaGVCN,
                item.Nganh,
                item.GiangVien,
                SiSo = item.SinhViens.Count,
                SinhViens = item.SinhViens.OrderBy(s => s.MaSV).ToList()
            }
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LopHanhChinh model)
    {
        if (string.IsNullOrWhiteSpace(model.MaLop) || string.IsNullOrWhiteSpace(model.TenLop))
            return BadRequest(new { status = "error", message = "Mã lớp và Tên lớp không được để trống" });

        model.MaLop = model.MaLop.Trim();
        if (!ProfileValidation.Code().IsMatch(model.MaLop)) return BadRequest(new { message = "Mã lớp không hợp lệ." });
        var error = await ValidateReferencesAsync(model);
        if (error != null) return BadRequest(new { message = error });
        model.Nganh = null;
        model.GiangVien = null;
        if (await _db.LopHanhChinhs.AnyAsync(l => l.MaLop == model.MaLop))
            return BadRequest(new { status = "error", message = $"Mã lớp {model.MaLop} đã tồn tại!" });

        if (string.IsNullOrWhiteSpace(model.MaNganh)) model.MaNganh = null;
        if (string.IsNullOrWhiteSpace(model.MaGVCN)) model.MaGVCN = null;

        _db.LopHanhChinhs.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Lớp hành chính", $"Đã thêm lớp hành chính {model.MaLop} - {model.TenLop}");
        return Ok(new { status = "success", message = "Thêm lớp hành chính thành công", data = model });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<LopHanhChinh> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách lớp hành chính trống" });

        var existingCodes = (await _db.LopHanhChinhs.AsNoTracking().Select(l => l.MaLop).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var majors = (await _db.Nganhs.Select(x => x.MaNganh).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lecturers = (await _db.GiangViens.Select(x => x.MaGV).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        int row = 1;
        foreach (var item in list)
        {
            row++;
            if (item == null || !ProfileValidation.Code().IsMatch(item.MaLop.Trim()))
                return BadRequest(new { message = $"Dòng {row}: Mã lớp không hợp lệ." });
            item.MaLop = item.MaLop.Trim();
            if (existingCodes.Contains(item.MaLop))
                continue;

            existingCodes.Add(item.MaLop);
            NormalizeClass(item);
            if ((item.MaNganh != null && !majors.Contains(item.MaNganh))
                || (item.MaGVCN != null && !lecturers.Contains(item.MaGVCN)))
                return BadRequest(new { message = $"Dòng {row}: Ngành hoặc giảng viên chủ nhiệm không tồn tại." });
            item.Nganh = null;
            item.GiangVien = null;
            _db.LopHanhChinhs.Add(item);
            count++;
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Lớp hành chính", $"Đã import {count} lớp hành chính từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} lớp hành chính" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] LopHanhChinh model)
    {
        var existing = await _db.LopHanhChinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp hành chính" });

        var error = await ValidateReferencesAsync(model);
        if (error != null) return BadRequest(new { message = error });

        existing.TenLop = model.TenLop;
        existing.NienKhoa = model.NienKhoa;
        existing.MaNganh = string.IsNullOrWhiteSpace(model.MaNganh) ? null : model.MaNganh;
        existing.MaGVCN = string.IsNullOrWhiteSpace(model.MaGVCN) ? null : model.MaGVCN;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Lớp hành chính", $"Đã cập nhật lớp hành chính {id}");
        return Ok(new { status = "success", message = "Cập nhật lớp hành chính thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.LopHanhChinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp hành chính" });

        if (await _db.SinhViens.AnyAsync(s => s.MaLop == id))
            return BadRequest(new { status = "error", message = "Không thể xóa Lớp hành chính đang có Sinh viên theo học!" });

        if (await _db.LopHocPhans.AnyAsync(l => l.MaLop == id))
            return BadRequest(new { message = "Không thể xóa lớp hành chính đang được dùng trong lớp học phần." });

        _db.LopHanhChinhs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Lớp hành chính", $"Đã xóa lớp hành chính {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa lớp hành chính thành công" });
    }

    private static void NormalizeClass(LopHanhChinh model)
    {
        model.TenLop = model.TenLop.Trim();
        model.NienKhoa = ProfileValidation.Optional(model.NienKhoa);
        model.MaNganh = ProfileValidation.Optional(model.MaNganh);
        model.MaGVCN = ProfileValidation.Optional(model.MaGVCN);
    }

    private async Task<string?> ValidateReferencesAsync(LopHanhChinh model)
    {
        NormalizeClass(model);
        if (model.MaNganh != null && !await _db.Nganhs.AnyAsync(x => x.MaNganh == model.MaNganh))
            return "Ngành không tồn tại.";
        if (model.MaGVCN != null && !await _db.GiangViens.AnyAsync(x => x.MaGV == model.MaGVCN))
            return "Giảng viên chủ nhiệm không tồn tại.";
        return null;
    }
}
