using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien,KhaoThi")]
public class LopHocPhanController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LopHocPhanController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maHK, [FromQuery] string? maGV,
        [FromQuery] string? maMon, int page = 1, int pageSize = 20, string? search = null,
        string? maKhoa = null, string? gradeStatus = null, string? sort = null)
    {
        maHK = await AcademicQueries.ResolveTermAsync(_db, maHK);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.LopHocPhans.AsNoTracking().Where(l => maHK != null && l.MaHK == maHK);
        if (!string.IsNullOrWhiteSpace(maGV)) query = query.Where(l => l.MaGV == maGV);
        if (!string.IsNullOrWhiteSpace(maMon)) query = query.Where(l => l.MaMon == maMon);
        if (!string.IsNullOrWhiteSpace(maKhoa)) query = query.Where(l => l.MonHoc!.MaKhoa == maKhoa);
        if (gradeStatus == "OPEN") query = query.Where(l => l.TrangThaiNhapDiem == null || l.TrangThaiNhapDiem == "" || l.TrangThaiNhapDiem == "Đang nhập" || l.TrangThaiNhapDiem == "Vui lòng nhập điểm");
        if (gradeStatus == "LOCKED") query = query.Where(l => l.TrangThaiNhapDiem == "Đã khóa");
        if (gradeStatus == "PENDING_UNLOCK") query = query.Where(l => l.TrangThaiNhapDiem == "Chờ mở khóa");
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(l => l.MaLHP.Contains(search) || (l.TenLopHP != null && l.TenLopHP.Contains(search))
                || l.MonHoc!.TenMon.Contains(search) || (l.PhongHoc != null && l.PhongHoc.Contains(search)) || l.GiangVien!.HoTen.Contains(search));
        }
        var summary = await query.GroupBy(l => 1).Select(g => new
        {
            Total = g.Count(),
            Open = g.Count(l => l.TrangThaiNhapDiem == null || l.TrangThaiNhapDiem == "" || l.TrangThaiNhapDiem == "Đang nhập" || l.TrangThaiNhapDiem == "Vui lòng nhập điểm"),
            Locked = g.Count(l => l.TrangThaiNhapDiem == "Đã khóa")
        }).FirstOrDefaultAsync();
        var total = summary?.Total ?? 0;
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "malhp_desc" or "newest" => query.OrderByDescending(l => l.MaLHP),
            "oldest" => query.OrderBy(l => l.MaLHP),
            "tenlhp_asc" => query.OrderBy(l => l.TenLopHP).ThenBy(l => l.MaLHP),
            "tenlhp_desc" => query.OrderByDescending(l => l.TenLopHP).ThenByDescending(l => l.MaLHP),
            "hocky_asc" => query.OrderBy(l => l.MaHK).ThenBy(l => l.MaLHP),
            "hocky_desc" => query.OrderByDescending(l => l.MaHK).ThenByDescending(l => l.MaLHP),
            "gv_asc" => query.OrderBy(l => l.GiangVien!.HoTen).ThenBy(l => l.MaLHP),
            "gv_desc" => query.OrderByDescending(l => l.GiangVien!.HoTen).ThenByDescending(l => l.MaLHP),
            "phong_asc" => query.OrderBy(l => l.PhongHoc).ThenBy(l => l.MaLHP),
            "phong_desc" => query.OrderByDescending(l => l.PhongHoc).ThenByDescending(l => l.MaLHP),
            "siso_asc" => query.OrderBy(l => l.KetQuaHocTaps.Count()).ThenBy(l => l.MaLHP),
            "siso_desc" => query.OrderByDescending(l => l.KetQuaHocTaps.Count()).ThenByDescending(l => l.MaLHP),
            "trangthai_asc" => query.OrderBy(l => l.TrangThai).ThenBy(l => l.MaLHP),
            "trangthai_desc" => query.OrderByDescending(l => l.TrangThai).ThenByDescending(l => l.MaLHP),
            _ => query.OrderBy(l => l.MaLHP)
        };

        var sections = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new
            {
                l.MaLHP, l.TenLopHP, l.MaMon, l.MaHK, l.MaGV, l.PhongHoc, l.SiSoToiDa,
                NMH = l.NMH == "" ? "01" : l.NMH,
                STC = l.STC > 0 ? l.STC : (l.MonHoc != null ? l.MonHoc.SoTinChi : 3),
                l.TrangThai, l.TrangThaiNhapDiem,
                TyLeQuaTrinh = l.TyLeQuaTrinh + l.TyLeThiKetThuc == 100 ? l.TyLeQuaTrinh : 40,
                TyLeThiKetThuc = l.TyLeQuaTrinh + l.TyLeThiKetThuc == 100 ? l.TyLeThiKetThuc : 60,
                MonHoc = l.MonHoc == null ? null : new { l.MonHoc.MaMon, l.MonHoc.TenMon, l.MonHoc.SoTinChi, l.MonHoc.MaKhoa },
                HocKy = l.HocKy == null ? null : new { l.HocKy.TenHK, l.HocKy.NamHocBatDau, l.HocKy.NamHocKetThuc },
                GiangVien = l.GiangVien == null ? null : new { l.GiangVien.MaGV, l.GiangVien.HoTen, l.GiangVien.MaKhoa }
            }).ToListAsync();
        var ids = sections.Select(l => l.MaLHP).ToArray();
        var counts = await _db.KetQuaHocTaps.AsNoTracking().Where(k => ids.Contains(k.MaLHP!))
            .GroupBy(k => k.MaLHP!).Select(g => new { Id = g.Key, Total = g.Count(), Graded = g.Count(k => k.DiemTongKet != null) })
            .ToDictionaryAsync(x => x.Id);
        var data = sections.Select(l => new
        {
            l.MaLHP, l.TenLopHP, l.MaMon, l.MaHK, l.MaGV, l.PhongHoc, l.SiSoToiDa, l.NMH, l.STC,
            SiSoHienTai = counts.GetValueOrDefault(l.MaLHP)?.Total ?? 0,
            SoSVDaNhapDiem = counts.GetValueOrDefault(l.MaLHP)?.Graded ?? 0,
            l.TrangThai, l.TrangThaiNhapDiem, l.TyLeQuaTrinh, l.TyLeThiKetThuc, l.MonHoc, l.HocKy, l.GiangVien
        });
        return Ok(new { status = "success", data, total, page, pageSize, totalPages, maHK, open = summary?.Open ?? 0, locked = summary?.Locked ?? 0 });
    }

    // Lightweight dropdown data, scoped to exactly one semester. Not used for table rows.
    [HttpGet("options")]
    public async Task<IActionResult> Options(string? maHK = null)
    {
        maHK = await AcademicQueries.ResolveTermAsync(_db, maHK);
        var data = await _db.LopHocPhans.AsNoTracking().Where(l => maHK != null && l.MaHK == maHK)
            .OrderBy(l => l.MaLHP).Select(l => new { l.MaLHP, l.TenLopHP, l.MaMon, l.MaHK }).ToListAsync();
        return Ok(new { status = "success", data, maHK });
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
        => Ok(new { status = "success", total = await _db.LopHocPhans.AsNoTracking().CountAsync() });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var l = await _db.LopHocPhans.AsNoTracking().AsSplitQuery()
            .Include(x => x.MonHoc)
                .ThenInclude(m => m!.Khoa)
            .Include(x => x.HocKy)
            .Include(x => x.GiangVien)
            .Include(x => x.KetQuaHocTaps)
                .ThenInclude(k => k.SinhVien)
                    .ThenInclude(s => s!.LopHanhChinh)
            .FirstOrDefaultAsync(x => x.MaLHP == id);

        if (l == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        var students = l.KetQuaHocTaps
            .Where(k => k.SinhVien != null)
            .Select(k => k.SinhVien!)
            .OrderBy(s => s.MaSV)
            .ToList();

        return Ok(new
        {
            status = "success",
            data = new
            {
                l.MaLHP,
                l.TenLopHP,
                l.MaMon,
                l.MaHK,
                l.MaGV,
                l.PhongHoc,
                l.SiSoToiDa,
                NMH = string.IsNullOrWhiteSpace(l.NMH) ? "01" : l.NMH,
                STC = l.STC > 0 ? l.STC : (l.MonHoc != null ? l.MonHoc.SoTinChi : 3),
                SiSoHienTai = students.Count,
                SoSVDaNhapDiem = l.KetQuaHocTaps.Count(k => k.DiemTongKet != null),
                l.TrangThai,
                l.CauHinhDiem,
                l.TrangThaiNhapDiem,
                TyLeQuaTrinh = (l.TyLeQuaTrinh + l.TyLeThiKetThuc == 100) ? l.TyLeQuaTrinh : 40,
                TyLeThiKetThuc = (l.TyLeQuaTrinh + l.TyLeThiKetThuc == 100) ? l.TyLeThiKetThuc : 60,
                l.MonHoc,
                l.HocKy,
                l.GiangVien,
                students
            }
        });
    }

    [HttpGet("{id}/available-students")]
    public async Task<IActionResult> GetAvailableStudents(string id)
    {
        var lhp = await _db.LopHocPhans.AsNoTracking().Where(l => l.MaLHP == id)
            .Select(l => new { l.MaMon, l.MaHK }).FirstOrDefaultAsync();
        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });
        var prerequisites = await _db.DieuKienMonHocs.AsNoTracking()
            .Where(d => d.MaMon == lhp.MaMon && d.MaMonTienQuyet != null && d.MaMonTienQuyet != "")
            .Select(d => d.MaMonTienQuyet!).Distinct().ToListAsync();
        var query = _db.SinhViens.AsNoTracking().Where(s => s.TrangThai != "Thôi học"
            && !s.KetQuaHocTaps.Any(k => k.LopHocPhan!.MaMon == lhp.MaMon && k.LopHocPhan.MaHK == lhp.MaHK));
        // Preserve existing eligibility semantics, but evaluate using SQL EXISTS instead of loading grades.
        foreach (var prerequisite in prerequisites)
        {
            var code = prerequisite;
            query = query.Where(s => !s.KetQuaHocTaps.Any(k => k.LopHocPhan!.MaMon == code)
                || s.KetQuaHocTaps.Any(k => k.LopHocPhan!.MaMon == code && (k.DiemTongKet == null || k.DiemTongKet >= 4.0)));
        }
        var data = await query.OrderBy(s => s.MaSV).Select(s => new
        {
            s.MaSV, s.HoTen, s.MaLop,
            LopHanhChinh = s.LopHanhChinh == null ? null : new { s.LopHanhChinh.MaLop, s.LopHanhChinh.TenLop }
        }).ToListAsync();
        return Ok(new { status = "success", data });
    }

    public class EnrollDto
    {
        public List<string> studentIds { get; set; } = new();
    }

    [HttpPost("{id}/students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddStudentsToClass(string id, [FromBody] EnrollDto req)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var lhp = await _db.LopHocPhans
            .Include(l => l.LichHocs)
            .Include(l => l.KetQuaHocTaps)
            .FirstOrDefaultAsync(l => l.MaLHP == id);

        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (req.studentIds == null || !req.studentIds.Any())
            return BadRequest(new { status = "error", message = "Danh sách sinh viên trống" });

        int currentCount = lhp.KetQuaHocTaps.Count;
        int maxCount = lhp.SiSoToiDa ?? 40;
        var existingIds = lhp.KetQuaHocTaps.Select(k => k.MaSV).ToHashSet();

        var newIds = req.studentIds.Where(s => !existingIds.Contains(s)).Distinct().ToList();
        if (currentCount + newIds.Count > maxCount)
        {
            return BadRequest(new { status = "error", message = $"Vượt quá sĩ số tối đa ({maxCount} sinh viên)! Hiện tại: {currentCount}." });
        }

        if (await _db.SinhViens.CountAsync(x => newIds.Contains(x.MaSV) && x.TrangThai == "Đang học") != newIds.Count)
            return BadRequest(new { status = "error", message = "Có sinh viên không tồn tại hoặc không còn đang học." });
        var enrolled = await _db.KetQuaHocTaps.Include(x => x.LopHocPhan).ThenInclude(x => x!.LichHocs)
            .Where(x => newIds.Contains(x.MaSV!) && x.LopHocPhan!.MaHK == lhp.MaHK).ToListAsync();
        foreach (var k in enrolled)
            if (k.LopHocPhan!.MaMon == lhp.MaMon || k.LopHocPhan.LichHocs.Any(a => lhp.LichHocs.Any(b => ScheduleRules.Overlaps(a,b))))
                return BadRequest(new { status = "error", message = $"Sinh viên {k.MaSV} đã đăng ký môn này hoặc bị trùng lịch." });
        foreach (var maSV in newIds)
        {
            _db.KetQuaHocTaps.Add(new KetQuaHocTap
            {
                MaSV = maSV,
                MaLHP = id
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xếp lớp học phần", $"Đã thêm {newIds.Count} sinh viên vào lớp {id}");
        return Ok(new { status = "success", message = $"Đã thêm {newIds.Count} sinh viên vào lớp học phần" });
    }

    [HttpDelete("{id}/students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveStudentsFromClass(string id, [FromBody] EnrollDto req)
    {
        if (req?.studentIds == null || !req.studentIds.Any())
            return BadRequest(new { status = "error", message = "Danh sách sinh viên cần xóa trống" });

        var records = await _db.KetQuaHocTaps
            .Where(k => k.MaLHP == id && k.MaSV != null && req.studentIds.Contains(k.MaSV))
            .ToListAsync();

        // Không cho phép xóa sinh viên đã có điểm tổng kết
        if (records.Any(ScheduleRules.HasGrades))
        {
            return BadRequest(new { status = "error", message = "Không thể xóa sinh viên đã có điểm thi khỏi lớp học phần!" });
        }

        _db.KetQuaHocTaps.RemoveRange(records);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa SV khỏi lớp học phần", $"Đã xóa {records.Count} sinh viên khỏi lớp {id}", "Cảnh báo");
        return Ok(new { status = "success", message = $"Đã xóa {records.Count} sinh viên khỏi lớp học phần" });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LopHocPhan model)
    {
        if (string.IsNullOrWhiteSpace(model.MaLHP) || string.IsNullOrWhiteSpace(model.TenLopHP))
            return BadRequest(new { status = "error", message = "Mã LHP và Tên lớp học phần không được để trống" });

        model.MaLHP = model.MaLHP.Trim();
        if (await _db.LopHocPhans.AnyAsync(l => l.MaLHP == model.MaLHP))
            return BadRequest(new { status = "error", message = $"Mã lớp học phần {model.MaLHP} đã tồn tại!" });

        if (string.IsNullOrWhiteSpace(model.CauHinhDiem))
        {
            model.CauHinhDiem = "[{\"id\":\"col_cc\",\"name\":\"Chuyên cần\",\"weight\":25},{\"id\":\"col_gk\",\"name\":\"Giữa kỳ\",\"weight\":75}]";
        }
        if (string.IsNullOrWhiteSpace(model.TrangThaiNhapDiem))
        {
            model.TrangThaiNhapDiem = "Vui lòng nhập điểm";
        }
        if (model.TyLeQuaTrinh < 0 || model.TyLeThiKetThuc < 0 || model.TyLeQuaTrinh + model.TyLeThiKetThuc != 100)
        {
            model.TyLeQuaTrinh = 40;
            model.TyLeThiKetThuc = 60;
        }

        var course = await _db.MonHocs.FindAsync(model.MaMon);
        if (course == null || !await _db.HocKys.AnyAsync(x => x.MaHK == model.MaHK)) return BadRequest(new { status = "error", message = "Môn học hoặc học kỳ không tồn tại." });
        model.STC = course.SoTinChi;
        model.STCHP = course.SoTinChi;
        _db.LopHocPhans.Add(model);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Lớp học phần", $"Đã tạo lớp học phần {model.MaLHP} - {model.TenLopHP}");
        return Ok(new { status = "success", message = "Thêm lớp học phần thành công", data = model });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<LopHocPhan> list)
    {
        if (list == null || !list.Any())
            return BadRequest(new { status = "error", message = "Danh sách lớp học phần trống" });

        string defaultCauHinh = "[{\"id\":\"col_cc\",\"name\":\"Chuyên cần\",\"weight\":25},{\"id\":\"col_gk\",\"name\":\"Giữa kỳ\",\"weight\":75}]";
        int count = 0;

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaLHP) || await _db.LopHocPhans.AnyAsync(l => l.MaLHP == item.MaLHP))
                continue;

            item.CauHinhDiem ??= defaultCauHinh;
            item.TrangThaiNhapDiem ??= "Vui lòng nhập điểm";
            item.TrangThai ??= "MoDangKy";
            if (item.TyLeQuaTrinh < 0 || item.TyLeThiKetThuc < 0 || item.TyLeQuaTrinh + item.TyLeThiKetThuc != 100)
            {
                item.TyLeQuaTrinh = 40;
                item.TyLeThiKetThuc = 60;
            }
            var course = await _db.MonHocs.FindAsync(item.MaMon);
            if (course == null || !await _db.HocKys.AnyAsync(x => x.MaHK == item.MaHK)) continue;
            item.STC = course.SoTinChi; item.STCHP = course.SoTinChi;
            _db.LopHocPhans.Add(item);
            count++;
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Lớp học phần", $"Đã import {count} lớp học phần từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} lớp học phần" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] LopHocPhan model)
    {
        var existing = await _db.LopHocPhans.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if ((existing.MaHK != model.MaHK || existing.MaMon != model.MaMon) &&
            (await _db.ClassSchedules.AnyAsync(x => x.MaLHP == id) || await _db.KetQuaHocTaps.AnyAsync(x => x.MaLHP == id)))
            return BadRequest(new { status = "error", message = "Không thể đổi môn/học kỳ khi lớp đã có lịch hoặc sinh viên." });
        if (model.SiSoToiDa < await _db.KetQuaHocTaps.CountAsync(x => x.MaLHP == id))
            return BadRequest(new { status = "error", message = "Sĩ số tối đa nhỏ hơn số sinh viên đã đăng ký." });
        if (existing.MaMon != model.MaMon)
        {
            var course = await _db.MonHocs.FindAsync(model.MaMon);
            if (course == null) return BadRequest(new { status = "error", message = "Môn học không tồn tại." });
            existing.STC = course.SoTinChi; existing.STCHP = course.SoTinChi;
        }
        existing.TenLopHP = model.TenLopHP;
        existing.MaMon = model.MaMon;
        existing.MaGV = model.MaGV;
        existing.MaHK = model.MaHK;
        existing.SiSoToiDa = model.SiSoToiDa;
        existing.PhongHoc = model.PhongHoc;
        existing.TrangThai = model.TrangThai;

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Lớp học phần", $"Đã cập nhật lớp học phần {id}");
        return Ok(new { status = "success", message = "Cập nhật lớp học phần thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.LopHocPhans
            .Include(l => l.KetQuaHocTaps)
            .FirstOrDefaultAsync(l => l.MaLHP == id);

        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (existing.KetQuaHocTaps.Any(k => k.DiemTongKet != null))
            return BadRequest(new { status = "error", message = "Không thể xóa Lớp học phần đã có điểm sinh viên!" });

        _db.LopHocPhans.Remove(existing);
        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Lớp học phần", $"Đã xóa lớp học phần {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa lớp học phần thành công" });
    }
}
