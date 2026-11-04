using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MonHocController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public MonHocController(ApplicationDbContext db)
    {
        _db = db;
    }

    public class MonHocDto
    {
        public string MaMon { get; set; } = string.Empty;
        public string TenMon { get; set; } = string.Empty;
        public int SoTinChi { get; set; }
        public int? SoTietLyThuyet { get; set; }
        public int? SoTietThucHanh { get; set; }
        public string? MaKhoa { get; set; }
        public List<string>? MonTienQuyet { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maKhoa)
    {
        var query = _db.MonHocs
            .Include(m => m.Khoa)
            .Include(m => m.DieuKienTienQuyets)
                .ThenInclude(d => d.MonHocTienQuyet)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            query = query.Where(m => m.MaKhoa == maKhoa);
        }

        var data = await query.OrderBy(m => m.MaMon).ToListAsync();
        return Ok(new { status = "success", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.MonHocs
            .Include(m => m.Khoa)
            .Include(m => m.DieuKienTienQuyets)
                .ThenInclude(d => d.MonHocTienQuyet)
            .FirstOrDefaultAsync(m => m.MaMon == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });
        return Ok(new { status = "success", data = item });
    }

    private async Task<bool> HasCircularDependencyAsync(string currentMaMon, List<string> newPrerequisites)
    {
        var allPrereqs = await _db.DieuKienMonHocs.ToListAsync();
        foreach (var prereq in newPrerequisites)
        {
            if (prereq == currentMaMon) return true;

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(prereq);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == currentMaMon) return true;
                if (!visited.Add(node)) continue;

                var nextNodes = allPrereqs
                    .Where(d => d.MaMon == node && !string.IsNullOrEmpty(d.MaMonTienQuyet))
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
    public async Task<IActionResult> Create([FromBody] MonHocDto req)
    {
        if (string.IsNullOrWhiteSpace(req.MaMon) || string.IsNullOrWhiteSpace(req.TenMon))
            return BadRequest(new { status = "error", message = "Mã môn và Tên môn không được để trống" });

        if (await _db.MonHocs.AnyAsync(m => m.MaMon == req.MaMon))
            return BadRequest(new { status = "error", message = $"Mã môn {req.MaMon} đã tồn tại!" });

        var mh = new MonHoc
        {
            MaMon = req.MaMon.Trim(),
            TenMon = req.TenMon.Trim(),
            SoTinChi = req.SoTinChi,
            SoTietLyThuyet = req.SoTietLyThuyet ?? 30,
            SoTietThucHanh = req.SoTietThucHanh ?? 15,
            MaKhoa = string.IsNullOrWhiteSpace(req.MaKhoa) ? null : req.MaKhoa
        };

        _db.MonHocs.Add(mh);
        await _db.SaveChangesAsync();

        if (req.MonTienQuyet != null && req.MonTienQuyet.Any())
        {
            var distinctPrereqs = req.MonTienQuyet.Where(p => !string.IsNullOrWhiteSpace(p) && p != mh.MaMon).Distinct().ToList();
            if (await HasCircularDependencyAsync(mh.MaMon, distinctPrereqs))
            {
                return BadRequest(new { status = "error", message = "Phát hiện vòng lặp môn tiên quyết (Circular Dependency)! Không thể thêm môn học." });
            }
            foreach (var p in distinctPrereqs)
            {
                _db.DieuKienMonHocs.Add(new DieuKienMonHoc { MaMon = mh.MaMon, MaMonTienQuyet = p });
            }
            await _db.SaveChangesAsync();
        }

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Môn học", $"Đã thêm môn học {mh.MaMon} - {mh.TenMon}");
        return Ok(new { status = "success", message = "Thêm môn học thành công", data = mh });
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] List<MonHocDto> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách môn học trống" });

        int count = 0;
        foreach (var req in list)
        {
            if (string.IsNullOrWhiteSpace(req.MaMon) || await _db.MonHocs.AnyAsync(m => m.MaMon == req.MaMon))
                continue;

            _db.MonHocs.Add(new MonHoc
            {
                MaMon = req.MaMon.Trim(),
                TenMon = req.TenMon?.Trim() ?? req.MaMon,
                SoTinChi = req.SoTinChi > 0 ? req.SoTinChi : 3,
                SoTietLyThuyet = req.SoTietLyThuyet ?? 30,
                SoTietThucHanh = req.SoTietThucHanh ?? 15,
                MaKhoa = string.IsNullOrWhiteSpace(req.MaKhoa) ? null : req.MaKhoa
            });
            count++;
        }
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Môn học", $"Đã import {count} môn học từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} môn học" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] MonHocDto req)
    {
        var existing = await _db.MonHocs
            .Include(m => m.DieuKienTienQuyets)
            .FirstOrDefaultAsync(m => m.MaMon == id);

        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });

        if (req.MonTienQuyet != null)
        {
            var distinctPrereqs = req.MonTienQuyet.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            if (await HasCircularDependencyAsync(id, distinctPrereqs))
            {
                return BadRequest(new { status = "error", message = "Phát hiện vòng lặp môn tiên quyết (Circular Dependency)! Không thể cập nhật." });
            }

            _db.DieuKienMonHocs.RemoveRange(existing.DieuKienTienQuyets);
            foreach (var p in distinctPrereqs)
            {
                _db.DieuKienMonHocs.Add(new DieuKienMonHoc { MaMon = id, MaMonTienQuyet = p });
            }
        }

        existing.TenMon = req.TenMon;
        existing.SoTinChi = req.SoTinChi;
        existing.SoTietLyThuyet = req.SoTietLyThuyet;
        existing.SoTietThucHanh = req.SoTietThucHanh;
        existing.MaKhoa = string.IsNullOrWhiteSpace(req.MaKhoa) ? null : req.MaKhoa;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Môn học", $"Đã cập nhật môn học {id}");
        return Ok(new { status = "success", message = "Cập nhật môn học thành công", data = existing });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.MonHocs.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Môn học" });

        if (await _db.LopHocPhans.AnyAsync(l => l.MaMon == id))
            return BadRequest(new { status = "error", message = "Không thể xóa Môn học đã mở Lớp học phần!" });

        var asPrereq = await _db.DieuKienMonHocs.Where(d => d.MaMon == id || d.MaMonTienQuyet == id).ToListAsync();
        _db.DieuKienMonHocs.RemoveRange(asPrereq);

        _db.MonHocs.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Môn học", $"Đã xóa môn học {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa môn học thành công" });
    }
}
