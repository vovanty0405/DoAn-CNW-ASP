using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;

namespace QL_DiemSV.Controllers;

[Authorize(Roles = "SinhVien,GiangVien,Admin")]
[Route("thoi-khoa-bieu")]
public class ThoiKhoaBieuController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? maHK, int week = 1, string mode = "week", string? maLHP = null, string? maGV = null, string? phong = null)
    {
        var terms = await db.HocKys.AsNoTracking()
            .OrderByDescending(x => x.NamHocBatDau)
            .ThenByDescending(x => x.MaHK)
            .ToListAsync();

        // Nếu người dùng truyền maLHP nhưng chưa truyền maHK, lấy học kỳ của lớp học phần đó
        if (string.IsNullOrWhiteSpace(maHK) && !string.IsNullOrWhiteSpace(maLHP))
        {
            maHK = await db.LopHocPhans.AsNoTracking()
                .Where(x => x.MaLHP == maLHP)
                .Select(x => x.MaHK)
                .FirstOrDefaultAsync();
        }

        maHK = await AcademicQueries.ResolveTermAsync(db, maHK);
        var term = terms.FirstOrDefault(x => x.MaHK == maHK);

        var vm = new TimetableViewModel
        {
            HocKys = terms,
            HocKy = term,
            Week = Math.Clamp(week, 1, term?.SoTuan ?? 20),
            Mode = mode == "all" ? "all" : "week",
            SelectedMaLHP = maLHP,
            SelectedMaGV = maGV,
            SelectedPhong = phong
        };

        if (term == null) return View(vm);

        if (User.IsInRole("Admin"))
        {
            vm.Sections = await db.LopHocPhans.AsNoTracking()
                .Where(x => x.MaHK == term.MaHK)
                .OrderBy(x => x.MaLHP)
                .Select(x => new LopHocPhan { MaLHP = x.MaLHP, TenLopHP = x.TenLopHP, MaMon = x.MaMon,
                    MonHoc = x.MonHoc == null ? null : new MonHoc { TenMon = x.MonHoc.TenMon } }).ToListAsync();
            vm.Teachers = await db.GiangViens.AsNoTracking().OrderBy(x => x.HoTen).ToListAsync();
            vm.Rooms = await db.PhongHocs.AsNoTracking().OrderBy(x => x.MaPhong).ToListAsync();
        }

        var q = db.ClassSchedules.AsNoTracking()
            .Where(x => x.LopHocPhan!.MaHK == term.MaHK);

        if (User.IsInRole("SinhVien"))
        {
            var id = User.FindFirst("MaSV")?.Value;
            q = q.Where(x => db.KetQuaHocTaps.Any(k => k.MaSV == id && k.MaLHP == x.MaLHP));
        }
        else if (User.IsInRole("GiangVien"))
        {
            var id = User.FindFirst("MaGV")?.Value;
            q = q.Where(x => x.MaGV == id);
        }
        else if (User.IsInRole("Admin"))
        {
            if (!string.IsNullOrWhiteSpace(maLHP)) q = q.Where(x => x.MaLHP == maLHP);
            if (!string.IsNullOrWhiteSpace(maGV)) q = q.Where(x => x.MaGV == maGV);
            if (!string.IsNullOrWhiteSpace(phong)) q = q.Where(x => x.PhongHoc == phong);
        }

        vm.Schedules = await q.OrderBy(x => x.Thu).ThenBy(x => x.TietBatDau).Select(AcademicQueries.ScheduleList).ToListAsync();
        return View(vm);
    }
}
