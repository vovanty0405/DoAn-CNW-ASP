using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
using System.Data;

namespace QL_DiemSV.Controllers;

[Authorize(Roles = "Admin")]
[Route("quan-ly-lich-hoc")]
public class LichHocController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? id, string? configHK)
    {
        var sections = await db.LopHocPhans
            .AsNoTracking()
            .Include(x => x.MonHoc)
                .ThenInclude(m => m!.Khoa)
            .Include(x => x.HocKy)
            .Include(x => x.GiangVien)
            .Include(x => x.KetQuaHocTaps)
                .ThenInclude(k => k.SinhVien)
                    .ThenInclude(s => s!.LopHanhChinh)
            .Include(x => x.LichHocs)
                .ThenInclude(l => l.GiangVien)
            .OrderByDescending(x => x.LichHocs.Count > 0)
            .ThenByDescending(x => x.MaHK)
            .ThenBy(x => x.MaLHP)
            .ToListAsync();

        var sectionId = sections.FirstOrDefault(x => x.MaLHP == id)?.MaLHP ?? sections.FirstOrDefault()?.MaLHP;
        var section = sections.FirstOrDefault(x => x.MaLHP == sectionId) ?? new LopHocPhan();
        var terms = await db.HocKys.AsNoTracking().OrderByDescending(x => x.NamHocBatDau).ThenByDescending(x => x.MaHK).ToListAsync();
        var adminClasses = await db.LopHanhChinhs.AsNoTracking()
            .Include(x => x.Nganh)
                .ThenInclude(n => n!.Khoa)
            .OrderBy(x => x.MaLop)
            .ToListAsync();
        var faculties = await db.Khoas.AsNoTracking().OrderBy(x => x.MaKhoa).ToListAsync();
        var cohorts = adminClasses
            .Where(x => !string.IsNullOrWhiteSpace(x.NienKhoa))
            .Select(x => x.NienKhoa!.Trim())
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        return View(new ScheduleEditorViewModel
        {
            Section = section,
            Sections = sections,
            Terms = terms,
            Faculties = faculties,
            Cohorts = cohorts,
            ConfigurationTerm = terms.FirstOrDefault(x => x.MaHK == configHK)
                ?? terms.FirstOrDefault(x => x.MaHK == section?.MaHK)
                ?? terms.FirstOrDefault(),
            AdministrativeClasses = adminClasses,
            Rooms = await db.PhongHocs.AsNoTracking().OrderBy(x => x.MaPhong).ToListAsync(),
            Teachers = await db.GiangViens.AsNoTracking().OrderBy(x => x.HoTen).ToListAsync()
        });
    }

    [HttpPost("luu"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string id, string nmh, string? tth, int stc, decimal stchp, string? maLop, int capacity, List<ClassSchedule>? schedules, string? configHK = null)
    {
        schedules ??= new List<ClassSchedule>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var section = await db.LopHocPhans
            .Include(x => x.LichHocs)
            .Include(x => x.HocKy)
            .Include(x => x.KetQuaHocTaps)
            .FirstOrDefaultAsync(x => x.MaLHP == id);

        if (section == null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu lịch học không hợp lệ. Kiểm tra thứ, tiết, giảng viên và tuần học.";
            return RedirectToAction(nameof(Index), new { id, configHK });
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(nmh) || nmh.Length > 10 || (tth?.Length ?? 0) > 10 || stc < 0 || stc > 30 || stchp < 0 || stchp > 100 || capacity < 1 || capacity < section.KetQuaHocTaps.Count || (maLop?.Length ?? 0) > 20)
            errors.Add("Nhóm, tín chỉ, mã lớp hoặc sĩ số không hợp lệ.");

        if (!string.IsNullOrEmpty(maLop) && !await db.LopHanhChinhs.AnyAsync(x => x.MaLop == maLop))
            errors.Add("Lớp hành chính không tồn tại.");

        var others = await db.ClassSchedules
            .Include(x => x.LopHocPhan)
            .Where(x => x.MaLHP != id && x.LopHocPhan!.MaHK == section.MaHK)
            .ToListAsync();

        var rooms = await db.PhongHocs.ToListAsync();
        var teachers = await db.GiangViens.Select(x => x.MaGV).ToListAsync();
        var students = section.KetQuaHocTaps.Select(x => x.MaSV).ToList();
        var studentSections = await db.KetQuaHocTaps
            .Where(x => students.Contains(x.MaSV) && x.MaLHP != id)
            .Select(x => x.MaLHP)
            .Distinct()
            .ToListAsync();

        for (int i = 0; i < schedules.Count; i++)
        {
            var a = schedules[i];
            a.Id = 0;
            a.MaLHP = id;
            a.PhongHoc = (a.PhongHoc ?? "").Trim();

            if (a.Thu < 2 || a.Thu > 8 || a.TietBatDau < 1 || a.SoTiet < 1 || a.TietBatDau + a.SoTiet > 13)
            {
                errors.Add($"Buổi {i + 1}: thứ/tiết không hợp lệ.");
                continue;
            }
            try
            {
                a.ChuoiTuanHoc = string.Join(",", ScheduleRules.Weeks(a.ChuoiTuanHoc ?? "", section.HocKy?.SoTuan ?? 20).Order());
            }
            catch (ArgumentException e)
            {
                errors.Add($"Buổi {i + 1}: {e.Message}");
                continue;
            }
            try
            {
                var absent = TimetablePresentation.AbsenceWeeks(a);
                if (!absent.IsSubsetOf(ScheduleRules.Weeks(a.ChuoiTuanHoc)))
                    errors.Add($"Buổi {i + 1}: tuần báo nghỉ phải thuộc các tuần có lịch học.");
                a.TuanBaoNghi = string.Join(",", absent.Order());
            }
            catch (ArgumentException e)
            {
                errors.Add($"Buổi {i + 1}: {e.Message}");
            }

            var room = rooms.FirstOrDefault(x => x.MaPhong == a.PhongHoc);
            if (room == null) errors.Add($"Phòng {a.PhongHoc} chưa được khai báo.");
            else if (room.TrangThai != "HoatDong") errors.Add($"Phòng {a.PhongHoc} đang bảo trì hoặc bị khóa, không thể xếp lịch.");
            else if (capacity > room.SucChua) warnings.Add($"Phòng {a.PhongHoc} chỉ có {room.SucChua} chỗ, lớp có sĩ số tối đa {capacity}.");

            if (!RoomMatrixRules.ScheduleTypes.ContainsKey(a.LoaiLich ?? "")) errors.Add($"Buổi {i + 1}: loại lịch không hợp lệ.");
            if (!teachers.Contains(a.MaGV)) errors.Add($"Buổi {i + 1}: giảng viên không tồn tại.");
            if (errors.Count > 0) continue;

            foreach (var b in others.Concat(schedules.Take(i)))
            {
                if (ScheduleRules.Overlaps(a, b))
                {
                    if (a.PhongHoc.Equals(b.PhongHoc, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Trùng phòng {a.PhongHoc} với lớp {b.MaLHP}, {ScheduleRules.DayName(a.Thu)}, tuần {string.Join(",", ScheduleRules.Weeks(a.ChuoiTuanHoc).Intersect(ScheduleRules.Weeks(b.ChuoiTuanHoc)))}.");
                    if (a.MaGV == b.MaGV)
                        errors.Add($"Giảng viên {a.MaGV} trùng lịch với lớp {b.MaLHP}.");
                    if (b.MaLHP == id)
                        errors.Add("Các buổi của cùng lớp học phần không được trùng nhau.");
                }
            }

            if (others.Any(b => studentSections.Contains(b.MaLHP) && ScheduleRules.Overlaps(a, b)))
                errors.Add($"Buổi {i + 1} trùng lịch của sinh viên đã đăng ký lớp.");
        }

        if (errors.Count > 0)
        {
            TempData["Error"] = string.Join(" ", errors.Distinct());
            return RedirectToAction(nameof(Index), new { id, configHK });
        }

        section.NMH = nmh;
        section.TTH = tth;
        section.STC = stc;
        section.STCHP = stchp;
        section.MaLop = maLop;
        section.SiSoToiDa = capacity;

        if (schedules.Count > 0)
        {
            section.PhongHoc = schedules[0].PhongHoc;
            section.MaGV = schedules[0].MaGV;
        }

        db.ClassSchedules.RemoveRange(section.LichHocs);
        db.ClassSchedules.AddRange(schedules);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Message"] = $"Đã lưu lịch học cho lớp {id} ({schedules.Count} buổi). " + string.Join(" ", warnings.Distinct());
        return RedirectToAction(nameof(Index), new { id, configHK });
    }

    [HttpPost("xoa-lich"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSchedule(string id, string? configHK = null)
    {
        var section = await db.LopHocPhans
            .Include(x => x.LichHocs)
            .FirstOrDefaultAsync(x => x.MaLHP == id);

        if (section == null)
        {
            TempData["Error"] = "Không tìm thấy lớp học phần.";
            return RedirectToAction(nameof(Index), new { configHK });
        }

        int count = section.LichHocs.Count;
        if (count == 0)
        {
            TempData["Error"] = $"Lớp học phần {id} hiện chưa có buổi học nào để xóa.";
            return RedirectToAction(nameof(Index), new { configHK });
        }

        db.ClassSchedules.RemoveRange(section.LichHocs);
        section.PhongHoc = null;
        await db.SaveChangesAsync();

        GradeHelper.GhiNhatKy(db, User.Identity?.Name, "Xóa lịch học LHP", $"Đã xóa {count} buổi học của lớp học phần {id}", "Cảnh báo");
        TempData["Message"] = $"Đã xóa toàn bộ {count} buổi học của lớp học phần {id}.";
        return RedirectToAction(nameof(Index), new { configHK });
    }

    [HttpPost("phong"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Room(string maPhong, int sucChua, string toaNha = "Khac", string trangThai = "HoatDong", string? originalMaPhong = null, string? id = null, string? configHK = null)
    {
        maPhong = (maPhong ?? "").Trim();
        originalMaPhong = originalMaPhong?.Trim();
        IActionResult Back() => RedirectToAction(nameof(Index), new { id, configHK, fragment = "rooms" });

        if (!ModelState.IsValid || maPhong.Length == 0 || maPhong.Length > 50 || sucChua < 1 || sucChua > 10000 || !RoomMatrixRules.Buildings.ContainsKey(toaNha ?? "") || !RoomMatrixRules.Statuses.ContainsKey(trangThai ?? ""))
        {
            TempData["Error"] = "Mã phòng, tòa nhà, sức chứa hoặc trạng thái không hợp lệ.";
            return Back();
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var original = string.IsNullOrEmpty(originalMaPhong) ? null : await db.PhongHocs.FindAsync(originalMaPhong);
        if (!string.IsNullOrEmpty(originalMaPhong) && original == null)
        {
            TempData["Error"] = "Phòng cần sửa không còn tồn tại.";
            return Back();
        }

        var duplicate = await db.PhongHocs.FirstOrDefaultAsync(x => x.MaPhong == maPhong);
        if (duplicate != null && duplicate.MaPhong != original?.MaPhong)
        {
            TempData["Error"] = "Mã phòng đã tồn tại. Dùng nút Sửa để cập nhật.";
            return Back();
        }

        if (original == null)
        {
            db.PhongHocs.Add(new PhongHoc { MaPhong = maPhong, SucChua = sucChua, ToaNha = toaNha!, TrangThai = trangThai! });
        }
        else if (!string.Equals(original.MaPhong, maPhong, StringComparison.OrdinalIgnoreCase))
        {
            db.PhongHocs.Add(new PhongHoc { MaPhong = maPhong, SucChua = sucChua, ToaNha = toaNha!, TrangThai = trangThai! });
            await db.SaveChangesAsync();
            var lessons = await db.ClassSchedules.Where(x => x.PhongHoc == original.MaPhong).ToListAsync();
            foreach (var lesson in lessons) lesson.PhongHoc = maPhong;
            var legacySections = await db.LopHocPhans.Where(x => x.PhongHoc == original.MaPhong).ToListAsync();
            foreach (var section in legacySections) section.PhongHoc = maPhong;
            await db.SaveChangesAsync();
            db.PhongHocs.Remove(original);
        }
        else
        {
            original.SucChua = sucChua;
            original.ToaNha = toaNha!;
            original.TrangThai = trangThai!;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        var oversized = await db.ClassSchedules.AnyAsync(x => x.PhongHoc == maPhong && x.LopHocPhan!.SiSoToiDa > sucChua);
        TempData["Message"] = "Đã lưu phòng học." + (oversized ? " Cảnh báo: có lớp đã xếp vượt sức chứa mới của phòng." : "") + (trangThai != "HoatDong" ? " Lịch đã có vẫn được giữ; không cho xếp thêm vào phòng này." : "");
        return Back();
    }

    [HttpPost("phong/xoa"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(string maPhong, string? id = null, string? configHK = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var room = await db.PhongHocs.FindAsync(maPhong);
        if (room == null) TempData["Error"] = "Phòng không tồn tại.";
        else if (await db.ClassSchedules.AnyAsync(x => x.PhongHoc == maPhong) || await db.LopHocPhans.AnyAsync(x => x.PhongHoc == maPhong))
            TempData["Error"] = "Không thể xóa phòng đang được sử dụng trong lịch học hoặc lớp học phần.";
        else
        {
            db.PhongHocs.Remove(room);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Message"] = "Đã xóa phòng học.";
        }
        return RedirectToAction(nameof(Index), new { id, configHK });
    }

    [HttpPost("hoc-ky"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Term(string maHK, DateTime ngayBatDauTuan1, DateTime batDauDangKy, DateTime ketThucDangKy, int soTuan, decimal donGiaTinChi, string? id = null)
    {
        // Coordinate changes with schedule saves and guard against reducing the semester under an existing booking.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var hk = await db.HocKys.FirstOrDefaultAsync(x=>x.MaHK==maHK);
        if (hk == null) { TempData["Error"]="Học kỳ không còn tồn tại. Vui lòng chọn lại."; return RedirectToAction(nameof(Index),new { id }); }
        if (!ModelState.IsValid || ngayBatDauTuan1==default || batDauDangKy==default || ketThucDangKy==default ||
            ngayBatDauTuan1.DayOfWeek!=DayOfWeek.Monday || ketThucDangKy<=batDauDangKy ||
            soTuan<1 || soTuan>52 || donGiaTinChi<0 || donGiaTinChi>100000000)
        {
            TempData["Error"]="Cấu hình không hợp lệ: nhập đủ ngày giờ; tuần 1 bắt đầu Thứ Hai; kết thúc ĐK sau bắt đầu ĐK; số tuần 1–52 và đơn giá không âm.";
            return RedirectToAction(nameof(Index),new { id, configHK=maHK });
        }
        var schedules=await db.ClassSchedules.Where(x=>x.LopHocPhan!.MaHK==maHK).ToListAsync();
        if(schedules.Any(x=>ScheduleRules.Weeks(x.ChuoiTuanHoc).Any(w=>w>soTuan)))
        {
            TempData["Error"]="Không thể giảm số tuần xuống thấp hơn tuần đã xếp lịch.";
            return RedirectToAction(nameof(Index),new { id, configHK=maHK });
        }
        hk.NgayBatDauTuan1=ngayBatDauTuan1.Date;
        hk.BatDauDangKy=batDauDangKy;hk.KetThucDangKy=ketThucDangKy;hk.SoTuan=soTuan;hk.DonGiaTinChi=donGiaTinChi;
        await db.SaveChangesAsync();await tx.CommitAsync();
        TempData["Message"]="Đã lưu cấu hình học kỳ (giờ Việt Nam).";
        return RedirectToAction(nameof(Index),new { id, configHK=maHK });
    }

    [HttpPost("hoc-ky/xoa"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTerm(string maHK,string? id=null,string? configHK=null)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var term=await db.HocKys.FirstOrDefaultAsync(x=>x.MaHK==maHK);
        if(term==null) TempData["Error"]="Học kỳ không còn tồn tại.";
        else if(await db.LopHocPhans.AnyAsync(x=>x.MaHK==maHK))
            TempData["Error"]="Không thể xóa học kỳ đã có Lớp học phần. Vui lòng xử lý các lớp liên quan trước để bảo đảm toàn vẹn dữ liệu.";
        else {
            db.HocKys.Remove(term);await db.SaveChangesAsync();await tx.CommitAsync();
            TempData["Message"]=$"Đã xóa {term.TenHK} · {term.NamHocBatDau}-{term.NamHocKetThuc}.";
            if(configHK==maHK)configHK=null;
        }
        return RedirectToAction(nameof(Index),new { id, configHK });
    }

    [AllowAnonymous, HttpGet("ma-tran")]
    public async Task<IActionResult> Matrix(string? maHK = null, int? week = null, int? day = null, string viewMode = "week", string scheduleType = "all", string building = "all", DateTime? date = null)
    {
        var terms = await db.HocKys.AsNoTracking().OrderByDescending(x => x.NamHocBatDau).ThenByDescending(x => x.MaHK).ToListAsync();
        var today = RoomMatrixRules.Today;

        if (string.IsNullOrWhiteSpace(maHK) && !date.HasValue)
        {
            var termIdsWithSchedules = await db.ClassSchedules
                .AsNoTracking()
                .Where(x => x.LopHocPhan != null && x.LopHocPhan.MaHK != null)
                .Select(x => x.LopHocPhan!.MaHK!)
                .Distinct()
                .ToListAsync();

            var activeTermWithSchedule = terms.FirstOrDefault(t => termIdsWithSchedules.Contains(t.MaHK) && RoomMatrixRules.ContainsDate(t, today))
                ?? terms.FirstOrDefault(t => termIdsWithSchedules.Contains(t.MaHK));

            if (activeTermWithSchedule != null)
            {
                maHK = activeTermWithSchedule.MaHK;
            }
        }

        var calendar = RoomMatrixRules.ResolveCalendar(terms, maHK, week, day, date, today);
        var term = calendar.Term;

        int effectiveWeek = calendar.Week;
        bool effectiveOutside = calendar.OutsideTerm;
        if (!date.HasValue && (effectiveOutside || term?.NgayBatDauTuan1 == null))
        {
            effectiveOutside = false;
            effectiveWeek = Math.Clamp(week ?? 1, 1, Math.Max(1, term?.SoTuan ?? 20));
        }

        var vm = new RoomMatrixViewModel
        {
            HocKys = terms,
            HocKy = term,
            Week = effectiveWeek,
            Day = calendar.Day,
            Date = term?.NgayBatDauTuan1 is DateTime start
                ? start.Date.AddDays((effectiveWeek - 1) * 7 + calendar.Day - 2)
                : calendar.Date,
            Today = today,
            OutsideTerm = effectiveOutside,
            HasCurrentTerm = terms.Any(x => RoomMatrixRules.ContainsDate(x, today)),
            ViewMode = new[] { "day", "week", "compact" }.Contains(viewMode) ? viewMode : "week",
            ScheduleType = RoomMatrixRules.ScheduleTypes.ContainsKey(scheduleType) ? scheduleType : "all",
            Building = RoomMatrixRules.Buildings.ContainsKey(building) ? building : "all"
        };

        var rooms = db.PhongHocs.AsNoTracking().AsQueryable();
        if (vm.Building != "all") rooms = rooms.Where(x => x.ToaNha == vm.Building);
        vm.Rooms = await rooms.OrderBy(x => x.MaPhong).ToListAsync();

        if (term != null && !vm.OutsideTerm)
        {
            var query = db.ClassSchedules.AsNoTracking()
                .Include(x => x.LopHocPhan).ThenInclude(x => x!.MonHoc)
                .Include(x => x.LopHocPhan).ThenInclude(x => x!.KetQuaHocTaps)
                .Include(x => x.GiangVien)
                .Where(x => x.LopHocPhan!.MaHK == term.MaHK);

            if (vm.ViewMode != "week") query = query.Where(x => x.Thu == vm.Day);
            if (vm.ScheduleType != "all") query = query.Where(x => x.LoaiLich == vm.ScheduleType);

            var allTermLessons = await query.ToListAsync();
            vm.Schedules = allTermLessons
                .Where(x => ScheduleRules.Weeks(x.ChuoiTuanHoc).Contains(vm.Week) && vm.Rooms.Any(r => r.MaPhong == x.PhongHoc))
                .ToList();
        }

        if (vm.ViewMode == "compact")
            vm.Rooms = vm.Rooms.Where(r => vm.Schedules.Any(x => x.PhongHoc == r.MaPhong)).ToList();

        return View(vm);
    }
}
