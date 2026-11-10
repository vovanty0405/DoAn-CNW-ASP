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
public class KhungChuongTrinhController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CourseCatalogCache _catalog;

    // Phạm vi gốc từ #Courses trong SeedData_QuanLyDiemSV.sql, độc lập với các dòng đã gán vào khung.
    private static readonly (string Scope, string Codes)[] SeedCourseGroups =
    [
        ("ALL", "PHI104 MAX309 MAX310 VRP505 HCM101 LAW101 ENG110 ENG111 ENG302 COS101"),
        ("faculty:CNTT", "COS104 SEE101 MAT104 PHY118 MAT113 COS106 CON301 MAT105 COS108 COS330"),
        ("faculty:CNTT", "COS304 COS303 COS107 MAT106 COS310 COS311 COS313 COS309 SEE301 PRS302"),
        ("faculty:CNTT", "COS503 CON503 IMS301 COS504 SEE910 SEE527 TIE501 IMS302 SEE529 COS508"),
        ("faculty:CNTT", "COS525 IMS912 MOR514 IMS501 CON915 SEE505 SEE530 SEE512 CON502 COS529"),
        ("faculty:CNTT", "COS521 COS526 SEE528 CON511 COS527 SEE531 COS528 TIE903 TIE913 SEE919"),
        ("faculty:CNTT", "SEE920 COS915 COS916 COS917"),
        ("faculty:SP", "PED117 PSY109 EDU103 PED317 PED318"),
        ("SPTOAN", "EDU126 ALG115 CAL111 ALG114 ALG116 CAL112 GEM111 PED338 CAL505 ALG512"),
        ("SPTOAN", "PRS303 EDU512 EDU812 EDU945"),
        ("SPAV", "EDU134 LAS113 LAS501 GSK106 GSK107 LAS115 GSK108 GSK311 PED583 LAS525"),
        ("SPAV", "LAS533 EDU519 EDU819 EDU952"),
        ("SPVL", "PHY115 MAT111 PHY116 PHY114 TEC111 PHY117 PED545 PHY309 PHY304 PHY509"),
        ("SPVL", "PHY546 PHY514 EDU518 EDU813 EDU946"),
    ];

    private static readonly Dictionary<string, string> SeedCourseScopes = SeedCourseGroups
        .SelectMany(group => group.Codes.Split(' ').Select(code => (Code: code, group.Scope)))
        .ToDictionary(entry => entry.Code, entry => entry.Scope, StringComparer.OrdinalIgnoreCase);

    public KhungChuongTrinhController(ApplicationDbContext db, CourseCatalogCache catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet("{maNganh}/course-options")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetCourseOptions(string maNganh, CancellationToken cancellationToken)
    {
        var major = await _db.Nganhs.AsNoTracking().Where(n => n.MaNganh == maNganh)
            .Select(n => new { n.MaNganh, n.MaKhoa }).SingleOrDefaultAsync(cancellationToken);
        if (major == null) return NotFound(new { message = "Không tìm thấy ngành đào tạo." });

        var assignments = await _db.KhungChuongTrinhs.AsNoTracking().Where(k => k.MaMon != null)
            .GroupBy(k => k.MaMon!)
            .Select(g => new { MaMon = g.Key, InMajor = g.Max(k => k.MaNganh == major.MaNganh ? 1 : 0) == 1 })
            .ToDictionaryAsync(k => k.MaMon, k => k.InMajor, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var options = new List<object>();
        foreach (var course in await _catalog.GetAsync())
        {
            var scope = ClassifyCourseOption(course, major.MaNganh, major.MaKhoa, assignments.GetValueOrDefault(course.MaMon));
            if (scope == null) continue;
            options.Add(new
            {
                course.MaMon, course.TenMon, course.SoTinChi, course.SoTietLyThuyet,
                course.SoTietThucHanh, course.MaKhoa, course.Khoa,
                PhamVi = scope
            });
        }
        return Ok(new { data = options });
    }

    private static string? ClassifyCourseOption(MonHoc course, string maNganh, string? maKhoa,
        bool inMajor)
    {
        var seedScope = SeedCourseScopes.GetValueOrDefault(course.MaMon);
        if (seedScope == "ALL") return "general";
        if (inMajor) return "major";
        if (seedScope != null)
            return seedScope == maNganh || seedScope == $"faculty:{maKhoa}" ? "major" : null;

        // Mã môn bổ sung của seed giữ được ngành ngay cả khi đã gỡ khỏi khung.
        var parts = course.MaMon.Split('_');
        if (parts.Length == 3 && parts[0] == "BS" && int.TryParse(parts[2], out _))
            return string.Equals(parts[1], maNganh, StringComparison.OrdinalIgnoreCase) ? "major" : null;

        return "unclassified";
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("{maNganh}")]
    public async Task<IActionResult> GetByNganh(string maNganh, [FromQuery] string? sort = null)
    {
        var subjects = (await _catalog.GetAsync()).ToDictionary(m => m.MaMon);
        var query = _db.KhungChuongTrinhs.AsNoTracking().Where(k => k.MaNganh == maNganh);
        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "newest" => query.OrderBy(k => k.HocKyDuKien).ThenByDescending(k => k.ID),
            "oldest" => query.OrderBy(k => k.HocKyDuKien).ThenBy(k => k.ID),
            "mamon_desc" => query.OrderBy(k => k.HocKyDuKien).ThenByDescending(k => k.MaMon),
            "tenmon_asc" => query.OrderBy(k => k.HocKyDuKien).ThenBy(k => k.MonHoc!.TenMon).ThenBy(k => k.MaMon),
            "tenmon_desc" => query.OrderBy(k => k.HocKyDuKien).ThenByDescending(k => k.MonHoc!.TenMon).ThenByDescending(k => k.MaMon),
            "sotc_asc" => query.OrderBy(k => k.HocKyDuKien).ThenBy(k => k.MonHoc!.SoTinChi).ThenBy(k => k.MaMon),
            "sotc_desc" => query.OrderBy(k => k.HocKyDuKien).ThenByDescending(k => k.MonHoc!.SoTinChi).ThenByDescending(k => k.MaMon),
            "loai_asc" => query.OrderBy(k => k.HocKyDuKien).ThenBy(k => k.LoaiMon).ThenBy(k => k.MaMon),
            "loai_desc" => query.OrderBy(k => k.HocKyDuKien).ThenByDescending(k => k.LoaiMon).ThenByDescending(k => k.MaMon),
            _ => query.OrderBy(k => k.HocKyDuKien).ThenBy(k => k.MaMon)
        };
        var danhSachMon = await query
            .Select(k => new KhungChuongTrinh { ID = k.ID, MaNganh = k.MaNganh, MaMon = k.MaMon, HocKyDuKien = k.HocKyDuKien, LoaiMon = k.LoaiMon })
            .ToListAsync();
        foreach (var row in danhSachMon) row.MonHoc = subjects.GetValueOrDefault(row.MaMon);

        var quyDinh = await _db.QuyDinhTotNghieps.AsNoTracking().FirstOrDefaultAsync(q => q.MaNganh == maNganh);
        if (quyDinh == null)
        {
            int sumBB = danhSachMon.Where(k => k.LoaiMon != "Tự chọn").Sum(k => k.MonHoc?.SoTinChi ?? 0);
            int sumTC = danhSachMon.Where(k => k.LoaiMon == "Tự chọn").Sum(k => k.MonHoc?.SoTinChi ?? 0);
            int tcBB = sumBB > 0 ? sumBB : 100;
            int tcTC = sumTC > 0 ? sumTC : 30;
            quyDinh = new QuyDinhTotNghiep
            {
                MaNganh = maNganh,
                TongTTC = (sumBB + sumTC) > 0 ? (sumBB + sumTC) : 130,
                TCBatBuoc = tcBB,
                TCTuChon = tcTC,
                MinGPA = 2.0
            };
        }

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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveSubject(int id)
    {
        var existing = await _db.KhungChuongTrinhs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy môn trong khung chương trình" });

        _db.KhungChuongTrinhs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa môn khỏi Khung đào tạo", $"Đã xóa môn {existing.MaMon} khỏi ngành {existing.MaNganh}");
        return Ok(new { status = "success", message = "Đã gỡ môn khỏi khung chương trình.", existing.MaMon, existing.MaNganh });
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
