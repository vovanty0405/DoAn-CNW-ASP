using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class KhoaController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public KhoaController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.Khoas.OrderBy(k => k.MaKhoa).ToListAsync();
        return Ok(new { status = "success", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.Khoas.FindAsync(id);
        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Khoa" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Khoa model)
    {
        if (string.IsNullOrWhiteSpace(model.MaKhoa) || string.IsNullOrWhiteSpace(model.TenKhoa))
            return BadRequest(new { status = "error", message = "Mã khoa và Tên khoa không được để trống" });

        if (await _db.Khoas.AnyAsync(k => k.MaKhoa == model.MaKhoa))
            return BadRequest(new { status = "error", message = $"Mã khoa {model.MaKhoa} đã tồn tại!" });

        _db.Khoas.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Khoa", $"Đã thêm khoa {model.MaKhoa} - {model.TenKhoa}");
        return Ok(new { status = "success", message = "Thêm khoa thành công", data = model });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Khoa model)
    {
        var existing = await _db.Khoas.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Khoa" });

        existing.TenKhoa = model.TenKhoa;
        existing.NgayThanhLap = model.NgayThanhLap;
        existing.TruongKhoa = model.TruongKhoa;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Khoa", $"Đã cập nhật khoa {id}");
        return Ok(new { status = "success", message = "Cập nhật khoa thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.Khoas.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Khoa" });

        if (await _db.GiangViens.AnyAsync(g => g.MaKhoa == id) ||
            await _db.MonHocs.AnyAsync(m => m.MaKhoa == id) ||
            await _db.Nganhs.AnyAsync(n => n.MaKhoa == id))
        {
            return BadRequest(new { status = "error", message = "Không thể xóa Khoa đang có Giảng viên, Môn học hoặc Ngành học trực thuộc!" });
        }

        _db.Khoas.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Khoa", $"Đã xóa khoa {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa khoa thành công" });
    }
}
