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
            .Select(k => new
            {
                k.SinhVien!.MaSV,
                k.SinhVien.HoTen,
                k.SinhVien.MaLop,
                LopHanhChinh = k.SinhVien.LopHanhChinh == null ? null : new
                {
                    k.SinhVien.LopHanhChinh.MaLop,
                    k.SinhVien.LopHanhChinh.TenLop
                }
            })
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

    [HttpGet("enrollment-options")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EnrollmentOptions()
    {
        var faculties = await _db.Khoas.AsNoTracking().OrderBy(k => k.TenKhoa)
            .Select(k => new { k.MaKhoa, k.TenKhoa }).ToListAsync(HttpContext.RequestAborted);
        var classes = await _db.LopHanhChinhs.AsNoTracking().OrderBy(l => l.MaLop)
            .Select(l => new { l.MaLop, l.TenLop, MaKhoa = l.Nganh!.MaKhoa }).ToListAsync(HttpContext.RequestAborted);
        return Ok(new { faculties, classes });
    }

    [HttpGet("{id}/available-students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAvailableStudents(string id, string? maKhoa = null,
        string? maLop = null, string? search = null, int page = 1)
    {
        var ct = HttpContext.RequestAborted;
        var lhp = await _db.LopHocPhans.AsNoTracking().FirstOrDefaultAsync(l => l.MaLHP == id, ct);
        if (lhp == null) return NotFound(new { message = "Không tìm thấy lớp học phần." });
        var query = await EligibleStudents(lhp, ct);
        if (!string.IsNullOrWhiteSpace(maKhoa))
            query = query.Where(s => s.LopHanhChinh!.Nganh!.MaKhoa == maKhoa);
        if (!string.IsNullOrWhiteSpace(maLop))
            query = query.Where(s => s.MaLop == maLop);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(s => s.MaSV.Contains(search) || s.HoTen.Contains(search));
        }
        const int pageSize = 50;
        page = Math.Clamp(page, 1, int.MaxValue / pageSize);
        var data = await query.OrderBy(s => s.MaSV).Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(s => new { s.MaSV, s.HoTen, s.MaLop }).ToListAsync(ct);
        return Ok(new { status = "success", data = data.Take(pageSize), hasNext = data.Count > pageSize });
    }

    [HttpGet("{id}/students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EnrollmentRoster(string id)
    {
        var ct = HttpContext.RequestAborted;
        var section = await _db.LopHocPhans.AsNoTracking().Where(l => l.MaLHP == id)
            .Select(l => new { l.MaLop, l.SiSoToiDa }).FirstOrDefaultAsync(ct);
        if (section == null) return NotFound(new { message = "Không tìm thấy lớp học phần." });
        var students = await _db.KetQuaHocTaps.AsNoTracking().Where(k => k.MaLHP == id && k.SinhVien != null)
            .OrderBy(k => k.MaSV).Select(k => new { k.SinhVien!.MaSV, k.SinhVien.HoTen, k.SinhVien.MaLop }).ToListAsync(ct);
        return Ok(new { data = new { section.MaLop, section.SiSoToiDa, SiSoHienTai = students.Count, students } });
    }

    private async Task<IQueryable<SinhVien>> EligibleStudents(LopHocPhan lhp, CancellationToken ct)
    {
        var prerequisites = await _db.DieuKienMonHocs.AsNoTracking()
            .Where(d => d.MaMon == lhp.MaMon && d.MaMonTienQuyet != null && d.MaMonTienQuyet != "")
            .Select(d => d.MaMonTienQuyet!).Distinct().ToListAsync(ct);
        var query = _db.SinhViens.AsNoTracking().Where(s => s.TrangThai == "Đang học"
            && !s.KetQuaHocTaps.Any(k => k.LopHocPhan!.MaMon == lhp.MaMon && k.LopHocPhan.MaHK == lhp.MaHK));
        foreach (var code in prerequisites)
            query = query.Where(s => s.KetQuaHocTaps.Any(k => k.LopHocPhan!.MaMon == code && k.DiemTongKet >= 4.0));
        return query;
    }

    public class EnrollDto
    {
        public List<string> studentIds { get; set; } = new();
    }

    public record ImportStudentRow(int Row, string? MaSV);
    private record EnrollmentCheck(int Row, string MaSV, string? HoTen, string? MaLop, string? Error);

    [HttpPost("{id}/students/preview")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> PreviewStudents(string id, [FromBody] List<ImportStudentRow> rows)
    {
        if (rows.Count is < 1 or > 1000)
            return BadRequest(new { message = "File cần có từ 1 đến 1.000 dòng sinh viên." });

        var ct = HttpContext.RequestAborted;
        var lhp = await _db.LopHocPhans.AsNoTracking().Include(l => l.LichHocs)
            .FirstOrDefaultAsync(l => l.MaLHP == id, ct);
        if (lhp == null) return NotFound(new { message = "Không tìm thấy lớp học phần." });
        var checks = await CheckEnrollment(lhp, rows, ct);
        return Ok(new { data = checks, valid = checks.Count(r => r.Error == null), invalid = checks.Count(r => r.Error != null) });
    }

    private async Task<List<EnrollmentCheck>> CheckEnrollment(LopHocPhan lhp,
        IEnumerable<ImportStudentRow> rows, CancellationToken ct)
    {
        var entries = rows.Select(r => new ImportStudentRow(r.Row, (r.MaSV ?? "").Trim().ToUpperInvariant())).ToList();
        var ids = entries.Select(r => r.MaSV!).Where(code => code.Length is > 0 and <= 20).Distinct().ToArray();
        var students = await _db.SinhViens.AsNoTracking().Where(s => ids.Contains(s.MaSV))
            .Select(s => new { s.MaSV, s.HoTen, s.MaLop, s.TrangThai })
            .ToDictionaryAsync(s => s.MaSV, StringComparer.OrdinalIgnoreCase, ct);
        var eligible = await EligibleStudents(lhp, ct);
        var eligibleIds = (await eligible.Where(s => ids.Contains(s.MaSV)).Select(s => s.MaSV).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enrollment = await _db.KetQuaHocTaps.AsNoTracking().Where(k => ids.Contains(k.MaSV!) && k.LopHocPhan!.MaHK == lhp.MaHK)
            .Select(k => new { k.MaSV, k.MaLHP, k.LopHocPhan!.MaMon }).ToListAsync(ct);
        var sectionIds = enrollment.Select(k => k.MaLHP).Distinct().ToArray();
        var schedules = await _db.ClassSchedules.AsNoTracking().Where(s => sectionIds.Contains(s.MaLHP)).ToListAsync(ct);
        var conflicts = schedules.Where(s => lhp.LichHocs.Any(target => ScheduleRules.Overlaps(s, target)))
            .Select(s => s.MaLHP).ToHashSet();
        var byStudent = enrollment.ToLookup(k => k.MaSV, StringComparer.OrdinalIgnoreCase);
        var occupied = await _db.KetQuaHocTaps.CountAsync(k => k.MaLHP == lhp.MaLHP, ct);
        var remaining = (lhp.SiSoToiDa ?? 40) - occupied;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checks = new List<EnrollmentCheck>(entries.Count);

        foreach (var row in entries)
        {
            var code = row.MaSV!;
            students.TryGetValue(code, out var student);
            var registered = byStudent[code];
            string? error = null;
            if (code.Length == 0) error = "Thiếu mã sinh viên.";
            else if (code.Length > 20) error = "Mã sinh viên vượt quá 20 ký tự.";
            else if (!seen.Add(code)) error = "Mã sinh viên bị lặp trong danh sách.";
            else if (student == null) error = "Không tìm thấy sinh viên.";
            else if (registered.Any(k => k.MaLHP == lhp.MaLHP)) error = "Sinh viên đã ở trong lớp này.";
            else if (student.TrangThai != "Đang học") error = "Sinh viên không còn ở trạng thái đang học.";
            else if (registered.Any(k => k.MaMon == lhp.MaMon)) error = "Đã xếp lớp cùng môn trong học kỳ.";
            else if (!eligibleIds.Contains(code)) error = "Không đạt điều kiện môn tiên quyết.";
            else if (registered.Any(k => conflicts.Contains(k.MaLHP!))) error = "Trùng lịch với lớp học phần khác.";
            else if (remaining <= 0) error = "Lớp học phần đã hết chỗ.";
            else remaining--;
            checks.Add(new EnrollmentCheck(row.Row, student?.MaSV ?? code, student?.HoTen, student?.MaLop, error));
        }
        return checks;
    }

    [HttpPost("{id}/students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddStudentsToClass(string id, [FromBody] EnrollDto req)
    {
        if (req.studentIds is not { Count: > 0 and <= 1000 })
            return BadRequest(new { message = "Cần chọn từ 1 đến 1.000 sinh viên." });

        var ct = HttpContext.RequestAborted;
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lhp = await _db.LopHocPhans.AsNoTracking().Include(l => l.LichHocs)
            .FirstOrDefaultAsync(l => l.MaLHP == id, ct);
        if (lhp == null) return NotFound(new { message = "Không tìm thấy lớp học phần." });

        var checks = await CheckEnrollment(lhp, req.studentIds.Select((code, i) => new ImportStudentRow(i + 1, code)), ct);
        var invalid = checks.FirstOrDefault(r => r.Error != null);
        if (invalid != null)
            return BadRequest(new { message = $"{invalid.MaSV}: {invalid.Error}" });

        _db.KetQuaHocTaps.AddRange(checks.Select(r => new KetQuaHocTap { MaSV = r.MaSV, MaLHP = lhp.MaLHP }));
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xếp lớp học phần", $"Đã thêm {checks.Count} sinh viên vào lớp {id}");
        return Ok(new { status = "success", message = $"Đã thêm {checks.Count} sinh viên vào lớp học phần" });
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
        var existingLhpIds = (await _db.LopHocPhans.AsNoTracking().Select(l => l.MaLHP).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var courseMap = await _db.MonHocs.AsNoTracking().ToDictionaryAsync(m => m.MaMon, StringComparer.OrdinalIgnoreCase);
        var termSet = (await _db.HocKys.AsNoTracking().Select(h => h.MaHK).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int count = 0;

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.MaLHP) || string.IsNullOrWhiteSpace(item.MaMon) || string.IsNullOrWhiteSpace(item.MaHK))
                continue;

            item.MaLHP = item.MaLHP.Trim();
            if (existingLhpIds.Contains(item.MaLHP))
                continue;
            if (!courseMap.TryGetValue(item.MaMon.Trim(), out var course) || !termSet.Contains(item.MaHK.Trim()))
                continue;

            existingLhpIds.Add(item.MaLHP);
            item.CauHinhDiem ??= defaultCauHinh;
            item.TrangThaiNhapDiem ??= "Vui lòng nhập điểm";
            item.TrangThai ??= "MoDangKy";
            if (item.TyLeQuaTrinh < 0 || item.TyLeThiKetThuc < 0 || item.TyLeQuaTrinh + item.TyLeThiKetThuc != 100)
            {
                item.TyLeQuaTrinh = 40;
                item.TyLeThiKetThuc = 60;
            }
            item.STC = course.SoTinChi;
            item.STCHP = course.SoTinChi;
            _db.LopHocPhans.Add(item);
            count++;
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
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
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var existing = await _db.LopHocPhans
            .Include(l => l.KetQuaHocTaps)
            .FirstOrDefaultAsync(l => l.MaLHP == id);

        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (existing.KetQuaHocTaps.Any(ScheduleRules.HasGrades))
            return BadRequest(new { status = "error", message = "Không thể xóa Lớp học phần đã có điểm sinh viên!" });

        _db.LopHocPhans.Remove(existing);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Lớp học phần", $"Đã xóa lớp học phần {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa lớp học phần thành công" });
    }
}
