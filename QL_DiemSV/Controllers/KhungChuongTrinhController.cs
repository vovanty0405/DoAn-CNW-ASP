using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class KhungChuongTrinhController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public KhungChuongTrinhController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{maNganh}")]
    public async Task<IActionResult> GetByNganh(string maNganh)
    {
        var quyDinh = await _db.QuyDinhTotNghieps.FindAsync(maNganh);
        if (quyDinh == null)
        {
            quyDinh = new QuyDinhTotNghiep
            {
                MaNganh = maNganh,
                TongTTC = 130,
                TCBatBuoc = 100,
                TCTuChon = 30,
                MinGPA = 2.0
            };
        }

        var danhSachMon = await _db.KhungChuongTrinhs
            .Include(k => k.MonHoc)
                .ThenInclude(m => m!.Khoa)
            .Include(k => k.MonHoc)
                .ThenInclude(m => m!.DieuKienTienQuyets)
                    .ThenInclude(d => d.MonHocTienQuyet)
            .Where(k => k.MaNganh == maNganh)
            .OrderBy(k => k.HocKyDuKien)
            .ThenBy(k => k.MaMon)
            .ToListAsync();

        return Ok(new
        {
            status = "success",
            data = new
            {
                quyDinh,
                danhSachMon
            }
        });
    }

    [HttpPut("{maNganh}/quydinh")]
    public async Task<IActionResult> UpdateQuyDinh(string maNganh, [FromBody] QuyDinhTotNghiep req)
    {
        var existing = await _db.QuyDinhTotNghieps.FindAsync(maNganh);
        if (existing == null)
        {
            existing = new QuyDinhTotNghiep
            {
                MaNganh = maNganh,
                TongTTC = req.TongTTC,
                TCBatBuoc = req.TCBatBuoc,
                TCTuChon = req.TCTuChon,
                MinGPA = req.MinGPA
            };
            _db.QuyDinhTotNghieps.Add(existing);
        }
        else
        {
            existing.TongTTC = req.TongTTC;
            existing.TCBatBuoc = req.TCBatBuoc;
            existing.TCTuChon = req.TCTuChon;
            existing.MinGPA = req.MinGPA;
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Quy định Tốt nghiệp", $"Cập nhật quy định tốt nghiệp cho ngành {maNganh}");
        return Ok(new { status = "success", message = "Cập nhật quy định tốt nghiệp thành công", data = existing });
    }

    [HttpPost]
    public async Task<IActionResult> AddSubject([FromBody] KhungChuongTrinh req)
    {
        if (string.IsNullOrWhiteSpace(req.MaNganh) || string.IsNullOrWhiteSpace(req.MaMon))
            return BadRequest(new { status = "error", message = "Mã ngành và Mã môn không được để trống" });

        if (await _db.KhungChuongTrinhs.AnyAsync(k => k.MaNganh == req.MaNganh && k.MaMon == req.MaMon))
            return BadRequest(new { status = "error", message = "Môn học này đã có trong khung đào tạo của ngành!" });

        var item = new KhungChuongTrinh
        {
            MaNganh = req.MaNganh,
            MaMon = req.MaMon,
            HocKyDuKien = req.HocKyDuKien > 0 ? req.HocKyDuKien : 1,
            LoaiMon = string.IsNullOrWhiteSpace(req.LoaiMon) ? "Bắt buộc" : req.LoaiMon
        };

        _db.KhungChuongTrinhs.Add(item);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm môn vào Khung đào tạo", $"Đã thêm môn {req.MaMon} vào ngành {req.MaNganh} (HK {item.HocKyDuKien})");
        return Ok(new { status = "success", message = "Đã thêm môn học vào khung chương trình", data = item });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubject(int id, [FromBody] KhungChuongTrinh req)
    {
        var existing = await _db.KhungChuongTrinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy bản ghi trong khung chương trình" });

        existing.HocKyDuKien = req.HocKyDuKien;
        existing.LoaiMon = req.LoaiMon;

        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Cập nhật thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveSubject(int id)
    {
        var existing = await _db.KhungChuongTrinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy môn trong khung chương trình" });

        _db.KhungChuongTrinhs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa môn khỏi Khung đào tạo", $"Đã xóa môn {existing.MaMon} khỏi ngành {existing.MaNganh}");
        return Ok(new { status = "success", message = "Đã xóa môn khỏi khung chương trình" });
    }
}
