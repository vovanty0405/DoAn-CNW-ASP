using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LopHanhChinhController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LopHanhChinhController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maNganh, [FromQuery] string? sort = null)
    {
        var query = _db.LopHanhChinhs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(maNganh)) query = query.Where(l => l.MaNganh == maNganh);

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.LopHanhChinhs.AsNoTracking().AsSplitQuery()
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
    public async Task<IActionResult> Create([FromBody] LopHanhChinh model)
    {
        if (string.IsNullOrWhiteSpace(model.MaLop) || string.IsNullOrWhiteSpace(model.TenLop))
            return BadRequest(new { status = "error", message = "Mã lớp và Tên lớp không được để trống" });

        model.MaLop = model.MaLop.Trim();
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
    public async Task<IActionResult> BulkCreate([FromBody] List<LopHanhChinh> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách lớp hành chính trống" });

        int count = 0;
        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaLop) || await _db.LopHanhChinhs.AnyAsync(l => l.MaLop == item.MaLop))
                continue;

            if (string.IsNullOrWhiteSpace(item.MaNganh)) item.MaNganh = null;
            if (string.IsNullOrWhiteSpace(item.MaGVCN)) item.MaGVCN = null;
            _db.LopHanhChinhs.Add(item);
            count++;
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Lớp hành chính", $"Đã import {count} lớp hành chính từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} lớp hành chính" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LopHanhChinh model)
    {
        var existing = await _db.LopHanhChinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp hành chính" });

        existing.TenLop = model.TenLop;
        existing.NienKhoa = model.NienKhoa;
        existing.MaNganh = string.IsNullOrWhiteSpace(model.MaNganh) ? null : model.MaNganh;
        existing.MaGVCN = string.IsNullOrWhiteSpace(model.MaGVCN) ? null : model.MaGVCN;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Lớp hành chính", $"Đã cập nhật lớp hành chính {id}");
        return Ok(new { status = "success", message = "Cập nhật lớp hành chính thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.LopHanhChinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp hành chính" });

        if (await _db.SinhViens.AnyAsync(s => s.MaLop == id))
            return BadRequest(new { status = "error", message = "Không thể xóa Lớp hành chính đang có Sinh viên theo học!" });

        _db.LopHanhChinhs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Lớp hành chính", $"Đã xóa lớp hành chính {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa lớp hành chính thành công" });
    }
}
