using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
using System.Data;
namespace QL_DiemSV.Controllers;
[Authorize(Roles = "SinhVien")]
[Route("dang-ky-hoc-phan")]
public class DangKyHocPhanController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? maHK, string? search, string? condition, string? value)
    {
        var terms = await db.HocKys.AsNoTracking().OrderByDescending(x => x.NamHocBatDau).ThenByDescending(x => x.MaHK).ToListAsync();
        maHK = await AcademicQueries.ResolveTermAsync(db, maHK);
        var term = terms.FirstOrDefault(x => x.MaHK == maHK);
        var vm = new RegistrationViewModel { HocKys = terms, HocKy = term, Search = search, Condition = condition, Value = value, IsOpen = term != null && ScheduleRules.IsOpen(term) };
        if (term == null) return View(vm);
        var q = db.LopHocPhans.AsNoTracking().AsSplitQuery().Include(x => x.MonHoc).Include(x => x.LichHocs).ThenInclude(x => x.GiangVien).Where(x => x.MaHK == term.MaHK && x.TrangThai == "MoDangKy");
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.MaMon!.Contains(search) || x.MonHoc!.TenMon.Contains(search));
        vm.Options = condition switch {
            "class" => await db.LopHanhChinhs.Select(x => new FilterOption(x.MaLop, x.TenLop)).ToListAsync(),
            "faculty" => await db.Khoas.Select(x => new FilterOption(x.MaKhoa, x.TenKhoa)).ToListAsync(),
            "major" or "curriculum" => await db.Nganhs.Select(x => new FilterOption(x.MaNganh, x.TenNganh)).ToListAsync(),
            _ => new()
        };
        if (!string.IsNullOrEmpty(value)) q = condition switch {
            "class" => q.Where(x => x.MaLop == value),
            "faculty" => q.Where(x => x.MonHoc!.MaKhoa == value),
            "major" => q.Where(x => db.LopHanhChinhs.Any(l => l.MaLop == x.MaLop && l.MaNganh == value)),
            "curriculum" => q.Where(x => db.KhungChuongTrinhs.Any(k => k.MaMon == x.MaMon && k.MaNganh == value)),
            _ => q
        };
        vm.Sections = await q.OrderBy(x => x.MaMon).ThenBy(x => x.NMH).ToListAsync();
        var sectionIds = vm.Sections.Select(x => x.MaLHP).ToArray();
        vm.SectionSizes = await db.KetQuaHocTaps.AsNoTracking().Where(x => sectionIds.Contains(x.MaLHP!))
            .GroupBy(x => x.MaLHP!).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        string sv = User.FindFirst("MaSV")?.Value ?? "";
        vm.Selected = await db.KetQuaHocTaps.AsNoTracking().AsSplitQuery().Include(x => x.LopHocPhan).ThenInclude(x => x!.MonHoc).Include(x => x.LopHocPhan).ThenInclude(x => x!.LichHocs).Where(x => x.MaSV == sv && x.LopHocPhan!.MaHK == term.MaHK).ToListAsync();
        return View(vm);
    }
    [HttpPost("luu"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] RegistrationRequest request)
    {
        if (!ModelState.IsValid || request == null || request.SectionIds == null) return BadRequest(new { message = "Dữ liệu đăng ký không hợp lệ." });
        string sv = User.FindFirst("MaSV")?.Value ?? "";
        if (!await db.SinhViens.AnyAsync(x => x.MaSV == sv && x.TrangThai == "Đang học")) return BadRequest(new { message = "Hồ sơ sinh viên không hợp lệ hoặc không còn đang học." });
        // Serializable protects capacity checks and same-student concurrent requests.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var term = await db.HocKys.FindAsync(request.MaHK);
        if (term == null || !ScheduleRules.IsOpen(term)) return BadRequest(new { message = "Ngoài thời gian đăng ký học phần." });
        var ids = request.SectionIds.Distinct().ToList();
        var sections = await db.LopHocPhans.Include(x => x.LichHocs).Include(x => x.MonHoc).Include(x => x.KetQuaHocTaps).Where(x => ids.Contains(x.MaLHP) && x.MaHK == request.MaHK).ToListAsync();
        if (sections.Count != ids.Count) return BadRequest(new { message = "Lớp học phần không tồn tại hoặc khác học kỳ." });
        if (sections.GroupBy(x => x.MaMon).Any(g => g.Count() > 1)) return BadRequest(new { message = "Không được đăng ký hai nhóm của cùng một môn học." });
        var current = await db.KetQuaHocTaps.Include(x => x.LopHocPhan).Where(x => x.MaSV == sv && x.LopHocPhan!.MaHK == request.MaHK).ToListAsync();
        var removed = current.Where(x => !ids.Contains(x.MaLHP!)).ToList();
        if (removed.Any(ScheduleRules.HasGrades)) return BadRequest(new { message = "Không thể hủy môn đã có điểm." });
        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            if (!current.Any(x => x.MaLHP == s.MaLHP))
            {
                if (s.TrangThai != "MoDangKy" || s.KetQuaHocTaps.Count >= (s.SiSoToiDa ?? 40)) return BadRequest(new { message = $"Lớp {s.MaLHP} đã hết chỗ hoặc đóng đăng ký." });
                var prerequisites = await db.DieuKienMonHocs.Where(x => x.MaMon == s.MaMon).Select(x => x.MaMonTienQuyet).ToListAsync();
                foreach (var prerequisite in prerequisites.Where(x => !string.IsNullOrEmpty(x)))
                    if (!await db.KetQuaHocTaps.AnyAsync(x => x.MaSV == sv && x.LopHocPhan!.MaMon == prerequisite && x.DiemTongKet >= 4)) return BadRequest(new { message = $"Môn {s.MaMon} yêu cầu đạt môn tiên quyết {prerequisite}." });
            }
            for (int j = 0; j < i; j++)
                foreach (var a in s.LichHocs)
                    foreach (var b in sections[j].LichHocs)
                        if (ScheduleRules.Overlaps(a,b)) return BadRequest(new { message = $"{s.MaMon} trùng lịch với {sections[j].MaMon}: {ScheduleRules.DayName(a.Thu)}, tiết {Math.Max(a.TietBatDau,b.TietBatDau)}, tuần {string.Join(",",ScheduleRules.Weeks(a.ChuoiTuanHoc).Intersect(ScheduleRules.Weeks(b.ChuoiTuanHoc)))}." });
        }
        foreach (var record in current.Where(x => ids.Contains(x.MaLHP!)))
        {
            var section = sections.Single(x => x.MaLHP == record.MaLHP);
            record.HocPhiDangKy ??= section.STC == 0 ? 0 : section.STCHP * term.DonGiaTinChi;
        }
        db.KetQuaHocTaps.RemoveRange(removed);
        foreach (var s in sections.Where(s => !current.Any(x => x.MaLHP == s.MaLHP))) db.KetQuaHocTaps.Add(new KetQuaHocTap { MaSV = sv, MaLHP = s.MaLHP, HocPhiDangKy = s.STC == 0 ? 0 : s.STCHP * term.DonGiaTinChi, NgayDangKy = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { message = "Đã lưu đăng ký vào CSDL." });
    }
}
