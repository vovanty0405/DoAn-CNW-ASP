using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien")]
public class LopHocPhanController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LopHocPhanController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maHK, [FromQuery] string? maGV, [FromQuery] string? maMon)
    {
        var query = _db.LopHocPhans
            .Include(l => l.MonHoc)
                .ThenInclude(m => m!.Khoa)
            .Include(l => l.HocKy)
            .Include(l => l.GiangVien)
            .Include(l => l.KetQuaHocTaps)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maHK)) query = query.Where(l => l.MaHK == maHK);
        if (!string.IsNullOrWhiteSpace(maGV)) query = query.Where(l => l.MaGV == maGV);
        if (!string.IsNullOrWhiteSpace(maMon)) query = query.Where(l => l.MaMon == maMon);

        var list = await query.OrderByDescending(l => l.MaHK).ThenBy(l => l.MaLHP).ToListAsync();
        var data = list.Select(l => new
        {
            l.MaLHP,
            l.TenLopHP,
            l.MaMon,
            l.MaHK,
            l.MaGV,
            l.PhongHoc,
            l.SiSoToiDa,
            SiSoHienTai = l.KetQuaHocTaps.Count,
            l.TrangThai,
            l.CauHinhDiem,
            l.TrangThaiNhapDiem,
            l.MonHoc,
            l.HocKy,
            l.GiangVien
        });

        return Ok(new { status = "success", data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var l = await _db.LopHocPhans
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
                SiSoHienTai = students.Count,
                l.TrangThai,
                l.CauHinhDiem,
                l.TrangThaiNhapDiem,
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
        var lhp = await _db.LopHocPhans
            .Include(l => l.LichHocs)
            .Include(l => l.KetQuaHocTaps)
            .FirstOrDefaultAsync(l => l.MaLHP == id);

        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        var enrolledIds = lhp.KetQuaHocTaps
            .Where(k => !string.IsNullOrEmpty(k.MaSV))
            .Select(k => k.MaSV!)
            .ToHashSet();

        // Lấy danh sách môn tiên quyết của môn học này
        var prereqCourses = await _db.DieuKienMonHocs
            .Where(d => d.MaMon == lhp.MaMon && !string.IsNullOrEmpty(d.MaMonTienQuyet))
            .Select(d => d.MaMonTienQuyet!)
            .ToListAsync();

        var allStudents = await _db.SinhViens
            .Include(s => s.LopHanhChinh)
            .Include(s => s.KetQuaHocTaps)
                .ThenInclude(k => k.LopHocPhan)
            .Where(s => !enrolledIds.Contains(s.MaSV) && s.TrangThai != "Thôi học")
            .OrderBy(s => s.MaSV)
            .ToListAsync();

        var available = new List<SinhVien>();
        foreach (var sv in allStudents)
        {
            // Kiểm tra đã đang học một lớp học phần khác của cùng môn trong cùng học kỳ chưa
            bool alreadyInSameCourseTerm = sv.KetQuaHocTaps.Any(k =>
                k.LopHocPhan != null &&
                k.LopHocPhan.MaMon == lhp.MaMon &&
                k.LopHocPhan.MaHK == lhp.MaHK);

            if (alreadyInSameCourseTerm) continue;

            // Kiểm tra điều kiện tiên quyết (nếu có): sinh viên phải từng học môn tiên quyết và có điểm tổng kết >= 4.0 (hoặc chưa rớt)
            bool passedPrereq = true;
            foreach (var preMon in prereqCourses)
            {
                bool hasPassed = sv.KetQuaHocTaps.Any(k =>
                    k.LopHocPhan != null &&
                    k.LopHocPhan.MaMon == preMon &&
                    (k.DiemTongKet == null || k.DiemTongKet >= 4.0));

                // Nếu trong hệ thống sinh viên có học môn tiên quyết mà rớt (< 4.0) và chưa đậu lần nào thì chặn
                bool hasFailedOnly = sv.KetQuaHocTaps.Any(k => k.LopHocPhan != null && k.LopHocPhan.MaMon == preMon) && !hasPassed;
                if (hasFailedOnly)
                {
                    passedPrereq = false;
                    break;
                }
            }

            if (passedPrereq)
            {
                available.Add(sv);
            }
        }

        return Ok(new { status = "success", data = available });
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
