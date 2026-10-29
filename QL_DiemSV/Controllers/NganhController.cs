using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NganhController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NganhController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maKhoa)
    {
        var query = _db.Nganhs
            .Include(n => n.Khoa)
            .Include(n => n.QuyDinhTotNghiep)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            query = query.Where(n => n.MaKhoa == maKhoa);
        }

        var data = await query.OrderBy(n => n.MaNganh).ToListAsync();
        return Ok(new { status = "success", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.Nganhs
            .Include(n => n.Khoa)
            .Include(n => n.QuyDinhTotNghiep)
            .FirstOrDefaultAsync(n => n.MaNganh == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Ngành" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Nganh model)
    {
        if (string.IsNullOrWhiteSpace(model.MaNganh) || string.IsNullOrWhiteSpace(model.TenNganh))
            return BadRequest(new { status = "error", message = "Mã ngành và Tên ngành không được để trống" });

        if (await _db.Nganhs.AnyAsync(n => n.MaNganh == model.MaNganh))
            return BadRequest(new { status = "error", message = $"Mã ngành {model.MaNganh} đã tồn tại!" });

        _db.Nganhs.Add(model);
        await _db.SaveChangesAsync();

        if (!await _db.QuyDinhTotNghieps.AnyAsync(q => q.MaNganh == model.MaNganh))
        {
            _db.QuyDinhTotNghieps.Add(new QuyDinhTotNghiep
            {
                MaNganh = model.MaNganh,
                TongTTC = 130,
                TCBatBuoc = 100,
                TCTuChon = 30,
                MinGPA = 2.0
            });
            await _db.SaveChangesAsync();
        }

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Ngành", $"Đã thêm ngành {model.MaNganh} - {model.TenNganh}");
        return Ok(new { status = "success", message = "Thêm ngành thành công", data = model });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Nganh model)
    {
        var existing = await _db.Nganhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Ngành" });

        existing.TenNganh = model.TenNganh;
        existing.MaKhoa = model.MaKhoa;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Ngành", $"Đã cập nhật ngành {id}");
        return Ok(new { status = "success", message = "Cập nhật ngành thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.Nganhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Ngành" });

        if (await _db.LopHanhChinhs.AnyAsync(l => l.MaNganh == id))
        {
            return BadRequest(new { status = "error", message = "Không thể xóa Ngành đang có Lớp hành chính trực thuộc!" });
        }

        _db.Nganhs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Ngành", $"Đã xóa ngành {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa ngành thành công" });
    }
}
