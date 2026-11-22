using System.ComponentModel.DataAnnotations;
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
public class MonHocController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CourseCatalogCache _catalog;

    public MonHocController(ApplicationDbContext db, CourseCatalogCache catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public class MonHocDto
    {
        [Required, MaxLength(20)]
        public string MaMon { get; set; } = string.Empty;
        [Required, MaxLength(255)]
        public string TenMon { get; set; } = string.Empty;
        [Range(0, 30)]
        public int SoTinChi { get; set; }
        [Range(0, int.MaxValue)]
        public int? SoTietLyThuyet { get; set; }
        [Range(0, int.MaxValue)]
        public int? SoTietThucHanh { get; set; }
        public string? MaKhoa { get; set; }
        public List<string>? MonTienQuyet { get; set; }
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
        => Ok(new { status = "success", total = (await _catalog.GetAsync()).Count });

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? maKhoa,
        [FromQuery] int? soTC = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? page = null,
        [FromQuery] int pageSize = 10)
    {
        var subjects = await _catalog.GetAsync();
        var query = subjects.Where(m => string.IsNullOrWhiteSpace(maKhoa) || m.MaKhoa == maKhoa);

        if (soTC.HasValue)
        {
            query = query.Where(m => m.SoTinChi == soTC.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(m => m.MaMon.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || m.TenMon.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = (sort?.Trim().ToLowerInvariant()) switch
        {
            "mamon_desc" or "newest" => query.OrderByDescending(m => m.MaMon),
            "oldest" => query.OrderBy(m => m.MaMon),
            "tenmon_asc" => query.OrderBy(m => m.TenMon).ThenBy(m => m.MaMon),
            "tenmon_desc" => query.OrderByDescending(m => m.TenMon).ThenByDescending(m => m.MaMon),
            "sotc_asc" => query.OrderBy(m => m.SoTinChi).ThenBy(m => m.MaMon),
            "sotc_desc" => query.OrderByDescending(m => m.SoTinChi).ThenByDescending(m => m.MaMon),
            "lt_asc" => query.OrderBy(m => m.SoTietLyThuyet).ThenBy(m => m.MaMon),
            "lt_desc" => query.OrderByDescending(m => m.SoTietLyThuyet).ThenByDescending(m => m.MaMon),
            "khoa_asc" => query.OrderBy(m => m.Khoa?.TenKhoa ?? m.MaKhoa).ThenBy(m => m.MaMon),
            "khoa_desc" => query.OrderByDescending(m => m.Khoa?.TenKhoa ?? m.MaKhoa).ThenByDescending(m => m.MaMon),
            _ => query.OrderBy(m => m.MaMon)
        };

        if (page.HasValue)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            var list = ordered.ToList();
            int total = list.Count;
            int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
            int currentPage = Math.Clamp(page.Value, 1, totalPages);
            var paged = list.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { status = "success", data = paged, total, page = currentPage, pageSize, totalPages });
        }

        return Ok(new { status = "success", data = ordered });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = (await _catalog.GetAsync()).FirstOrDefault(m => m.MaMon == id);
        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });
        return Ok(new { status = "success", data = item });
    }

    private async Task<bool> HasCircularDependencyAsync(string currentMaMon, List<string> newPrerequisites)
    {
        var allPrereqs = await _db.DieuKienMonHocs.ToListAsync();
        foreach (var prereq in newPrerequisites)
        {
            if (string.Equals(prereq, currentMaMon, StringComparison.OrdinalIgnoreCase)) return true;

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(prereq);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (string.Equals(node, currentMaMon, StringComparison.OrdinalIgnoreCase)) return true;
                if (!visited.Add(node)) continue;

                var nextNodes = allPrereqs
                    .Where(d => string.Equals(d.MaMon, node, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(d.MaMonTienQuyet))
                    .Select(d => d.MaMonTienQuyet!);

                foreach (var next in nextNodes)
                {
                    queue.Enqueue(next);
                }
            }
        }
        return false;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] MonHocDto req)
    {
        if (string.IsNullOrWhiteSpace(req.MaMon) || string.IsNullOrWhiteSpace(req.TenMon))
            return BadRequest(new { status = "error", message = "Mã môn và Tên môn không được để trống" });

        req.MaMon = req.MaMon.Trim();
        if (!ProfileValidation.Code().IsMatch(req.MaMon))
            return BadRequest(new { message = "Mã môn học không hợp lệ." });
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await _db.MonHocs.AnyAsync(m => m.MaMon == req.MaMon))
            return BadRequest(new { status = "error", message = $"Mã môn {req.MaMon} đã tồn tại!" });

        var mh = new MonHoc
        {
            MaMon = req.MaMon.Trim(),
            TenMon = req.TenMon.Trim(),
            SoTinChi = req.SoTinChi,
            SoTietLyThuyet = req.SoTietLyThuyet ?? 30,
            SoTietThucHanh = req.SoTietThucHanh ?? 15,
            MaKhoa = ProfileValidation.Optional(req.MaKhoa)
        };

        if (mh.MaKhoa != null && !await _db.Khoas.AnyAsync(x => x.MaKhoa == mh.MaKhoa))
            return BadRequest(new { message = "Khoa phụ trách không tồn tại." });
        _db.MonHocs.Add(mh);

        if (req.MonTienQuyet != null && req.MonTienQuyet.Any())
        {
            var distinctPrereqs = req.MonTienQuyet.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (await HasCircularDependencyAsync(mh.MaMon, distinctPrereqs))
            {
                return BadRequest(new { status = "error", message = "Phát hiện vòng lặp môn tiên quyết (Circular Dependency)! Không thể thêm môn học." });
            }
            if (await _db.MonHocs.CountAsync(m => distinctPrereqs.Contains(m.MaMon)) != distinctPrereqs.Count)
                return BadRequest(new { message = "Có môn tiên quyết không tồn tại." });
            foreach (var p in distinctPrereqs)
            {
                _db.DieuKienMonHocs.Add(new DieuKienMonHoc { MaMon = mh.MaMon, MaMonTienQuyet = p });
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        _catalog.Invalidate();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Môn học", $"Đã thêm môn học {mh.MaMon} - {mh.TenMon}");
        return Ok(new { status = "success", message = "Thêm môn học thành công", data = mh });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<MonHocDto> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách môn học trống" });

        var existingCodes = (await _db.MonHocs.AsNoTracking().Select(m => m.MaMon).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var facultyCodes = (await _db.Khoas.Select(k => k.MaKhoa).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        int row = 1;
        foreach (var req in list)
        {
            row++;
            if (req == null || !ProfileValidation.Code().IsMatch(req.MaMon.Trim()))
                return BadRequest(new { message = $"Dòng {row}: Mã môn học không hợp lệ." });
            var code = req.MaMon.Trim();
            if (existingCodes.Contains(code))
                continue;

            var faculty = ProfileValidation.Optional(req.MaKhoa);
            if (faculty != null && !facultyCodes.Contains(faculty))
                return BadRequest(new { message = $"Dòng {row}: Khoa phụ trách không tồn tại." });

            existingCodes.Add(code);
            _db.MonHocs.Add(new MonHoc
            {
                MaMon = code,
                TenMon = req.TenMon.Trim(),
                SoTinChi = req.SoTinChi,
                SoTietLyThuyet = req.SoTietLyThuyet ?? 30,
                SoTietThucHanh = req.SoTietThucHanh ?? 15,
                MaKhoa = ProfileValidation.Optional(req.MaKhoa)
            });
            count++;
        }
        if (count > 0)
        {
            await _db.SaveChangesAsync();
            _catalog.Invalidate();
        }
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Môn học", $"Đã import {count} môn học từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} môn học" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] MonHocDto req)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        req.MaKhoa = ProfileValidation.Optional(req.MaKhoa);
        if (req.MaKhoa != null && !await _db.Khoas.AnyAsync(x => x.MaKhoa == req.MaKhoa))
            return BadRequest(new { message = "Khoa phụ trách không tồn tại." });

        var existing = await _db.MonHocs
            .Include(m => m.DieuKienTienQuyets)
            .FirstOrDefaultAsync(m => m.MaMon == id);

        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });

        if (req.MonTienQuyet != null)
        {
            var distinctPrereqs = req.MonTienQuyet.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (await HasCircularDependencyAsync(id, distinctPrereqs))
            {
                return BadRequest(new { status = "error", message = "Phát hiện vòng lặp môn tiên quyết (Circular Dependency)! Không thể cập nhật." });
            }

            _db.DieuKienMonHocs.RemoveRange(existing.DieuKienTienQuyets);
            if (await _db.MonHocs.CountAsync(m => distinctPrereqs.Contains(m.MaMon)) != distinctPrereqs.Count)
                return BadRequest(new { message = "Có môn tiên quyết không tồn tại." });
            foreach (var p in distinctPrereqs)
            {
                _db.DieuKienMonHocs.Add(new DieuKienMonHoc { MaMon = id, MaMonTienQuyet = p });
            }
        }

        existing.TenMon = req.TenMon;
        existing.SoTinChi = req.SoTinChi;
        existing.SoTietLyThuyet = req.SoTietLyThuyet;
        existing.SoTietThucHanh = req.SoTietThucHanh;
        existing.MaKhoa = ProfileValidation.Optional(req.MaKhoa);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        _catalog.Invalidate();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Môn học", $"Đã cập nhật môn học {id}");
        return Ok(new { status = "success", message = "Cập nhật môn học thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.MonHocs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });

        if (await _db.LopHocPhans.AnyAsync(l => l.MaMon == id))
            return BadRequest(new { status = "error", message = "Không thể xóa Môn học đã mở Lớp học phần!" });

        if (await _db.KhungChuongTrinhs.AnyAsync(k => k.MaMon == id))
            return BadRequest(new { message = "Không thể xóa môn đang nằm trong khung đào tạo." });

        var asPrereq = await _db.DieuKienMonHocs.Where(d => d.MaMon == id || d.MaMonTienQuyet == id).ToListAsync();
        _db.DieuKienMonHocs.RemoveRange(asPrereq);

        _db.MonHocs.Remove(existing);
        await _db.SaveChangesAsync();
        _catalog.Invalidate();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Môn học", $"Đã xóa môn học {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa môn học thành công" });
    }
}
