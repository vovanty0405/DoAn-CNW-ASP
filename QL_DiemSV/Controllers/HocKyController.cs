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
public class HocKyController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public HocKyController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.HocKys
            .OrderByDescending(h => h.NamHocBatDau)
            .ThenBy(h => h.MaHK)
            .ToListAsync();
        return Ok(new { status = "success", data });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] HocKy model)
    {
        if (string.IsNullOrWhiteSpace(model.MaHK) || string.IsNullOrWhiteSpace(model.TenHK))
            return BadRequest(new { status = "error", message = "Mã học kỳ và Tên học kỳ không được để trống" });

        model.MaHK = model.MaHK.Trim();
        if (!ProfileValidation.Code().IsMatch(model.MaHK))
            return BadRequest(new { message = "Mã học kỳ không hợp lệ." });
        if (model.NamHocKetThuc != model.NamHocBatDau + 1)
            return BadRequest(new { message = "Năm kết thúc phải liền sau năm bắt đầu." });
        if (model.NgayBatDauTuan1 is { DayOfWeek: not DayOfWeek.Monday }
            || model.BatDauDangKy.HasValue != model.KetThucDangKy.HasValue
            || model.BatDauDangKy >= model.KetThucDangKy)
            return BadRequest(new { message = "Tuần 1 phải bắt đầu vào Thứ Hai và khoảng đăng ký phải hợp lệ." });
        if (await _db.HocKys.AnyAsync(h => h.MaHK == model.MaHK))
            return BadRequest(new { status = "error", message = $"Mã học kỳ {model.MaHK} đã tồn tại!" });

        _db.HocKys.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Học kỳ", $"Đã thêm học kỳ {model.MaHK} ({model.TenHK})");
        return Ok(new { status = "success", message = "Thêm học kỳ thành công", data = model });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] HocKy model)
    {
        var existing = await _db.HocKys.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Học kỳ" });

        if (model.NamHocKetThuc != model.NamHocBatDau + 1)
            return BadRequest(new { message = "Năm kết thúc phải liền sau năm bắt đầu." });

        existing.TenHK = model.TenHK.Trim();
        existing.NamHocBatDau = model.NamHocBatDau;
        existing.NamHocKetThuc = model.NamHocKetThuc;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Học kỳ", $"Đã cập nhật học kỳ {id}");
        return Ok(new { status = "success", message = "Cập nhật học kỳ thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.HocKys.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Học kỳ" });

        if (await _db.LopHocPhans.AnyAsync(l => l.MaHK == id))
            return BadRequest(new { status = "error", message = "Không thể xóa Học kỳ đã có Lớp học phần!" });

        _db.HocKys.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Học kỳ", $"Đã xóa học kỳ {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa học kỳ thành công" });
    }
}
