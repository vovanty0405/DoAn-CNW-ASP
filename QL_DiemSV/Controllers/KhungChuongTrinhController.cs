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
    private readonly CourseCatalogCache _catalog;

    public KhungChuongTrinhController(ApplicationDbContext db, CourseCatalogCache catalog)
    {
        _db = db;
        _catalog = catalog;
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

        var subjects = (await _catalog.GetAsync()).ToDictionary(m => m.MaMon);
        var danhSachMon = await _db.KhungChuongTrinhs.AsNoTracking()
            .Where(k => k.MaNganh == maNganh).OrderBy(k => k.HocKyDuKien).ThenBy(k => k.MaMon)
            .Select(k => new KhungChuongTrinh { ID = k.ID, MaNganh = k.MaNganh, MaMon = k.MaMon, HocKyDuKien = k.HocKyDuKien, LoaiMon = k.LoaiMon })
            .ToListAsync();
        foreach (var row in danhSachMon) row.MonHoc = subjects.GetValueOrDefault(row.MaMon);

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

    [HttpPost("batch")]
    public async Task<IActionResult> AddSubjectsBatch([FromBody] BatchAddKhungDto req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.MaNganh))
            return BadRequest(new { status = "error", message = "Vui lòng chọn Ngành đào tạo!" });

        var subjectCodes = new List<string>();
        var customLoaiMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (req.Items != null && req.Items.Any())
        {
            foreach (var item in req.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.MaMon))
                {
                    var code = item.MaMon.Trim();
                    subjectCodes.Add(code);
                    if (!string.IsNullOrWhiteSpace(item.LoaiMon))
                    {
                        customLoaiMap[code] = item.LoaiMon.Trim();
                    }
                }
            }
        }

        if (req.MaMonList != null && req.MaMonList.Any())
        {
            foreach (var code in req.MaMonList)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    subjectCodes.Add(code.Trim());
                }
            }
        }

        subjectCodes = subjectCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!subjectCodes.Any())
            return BadRequest(new { status = "error", message = "Vui lòng chọn ít nhất 1 môn học để thêm vào khung đào tạo!" });

        int hocKy = req.HocKy > 0 ? req.HocKy : 1;
        string defaultLoai = string.IsNullOrWhiteSpace(req.LoaiHocPhan) ? "Bắt buộc" : req.LoaiHocPhan.Trim();

        // Lấy danh sách mã môn đã tồn tại trong khung đào tạo của ngành này
        var existingCodes = await _db.KhungChuongTrinhs
            .Where(k => k.MaNganh == req.MaNganh)
            .Select(k => k.MaMon)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<KhungChuongTrinh>();
        foreach (var maMon in subjectCodes)
        {
            if (existingSet.Contains(maMon))
                continue;

            var loai = customLoaiMap.TryGetValue(maMon, out var customLoai) ? customLoai : defaultLoai;
            toAdd.Add(new KhungChuongTrinh
            {
                MaNganh = req.MaNganh.Trim(),
                MaMon = maMon,
                HocKyDuKien = hocKy,
                LoaiMon = loai
            });
            existingSet.Add(maMon);
        }

        if (!toAdd.Any())
        {
            return BadRequest(new { status = "error", message = "Tất cả các môn học được chọn đều đã tồn tại trong khung đào tạo của ngành này!" });
        }

        _db.KhungChuongTrinhs.AddRange(toAdd);
        await _db.SaveChangesAsync();

        GradeHelper.GhiNhatKy(
            _db,
            User.Identity?.Name,
            "Thêm hàng loạt môn vào Khung đào tạo",
            $"Đã thêm {toAdd.Count} môn học vào Học kỳ {hocKy} của ngành {req.MaNganh}: {string.Join(", ", toAdd.Select(x => x.MaMon))}"
        );

        return Ok(new
        {
            status = "success",
            message = $"Đã thêm thành công {toAdd.Count} môn học vào Học kỳ {hocKy}",
            addedCount = toAdd.Count,
            data = toAdd
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubject(int id, [FromBody] UpdateKhungSubjectDto req)
    {
        var existing = await _db.KhungChuongTrinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy bản ghi trong khung chương trình" });

        if (req.HocKyDuKien.HasValue && req.HocKyDuKien.Value > 0)
            existing.HocKyDuKien = req.HocKyDuKien.Value;

        if (!string.IsNullOrWhiteSpace(req.LoaiMon))
            existing.LoaiMon = req.LoaiMon.Trim();

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật môn trong Khung đào tạo", $"Đã cập nhật môn {existing.MaMon} (Ngành {existing.MaNganh}): {existing.LoaiMon} - HK {existing.HocKyDuKien}");
        return Ok(new { status = "success", message = $"Đã cập nhật môn {existing.MaMon} thành {existing.LoaiMon}", data = existing });
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

public class BatchAddKhungDto
{
    public string MaNganh { get; set; } = string.Empty;
    public int HocKy { get; set; } = 1;
    public string LoaiHocPhan { get; set; } = "Bắt buộc";
    public List<string> MaMonList { get; set; } = new();
    public List<BatchSubjectItemDto>? Items { get; set; }
}

public class BatchSubjectItemDto
{
    public string MaMon { get; set; } = string.Empty;
    public string? LoaiMon { get; set; }
}

public class UpdateKhungSubjectDto
{
    public int? HocKyDuKien { get; set; }
    public string? LoaiMon { get; set; }
}
