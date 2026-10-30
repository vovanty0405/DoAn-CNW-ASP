using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
namespace QL_DiemSV.Controllers;
[Authorize(Roles = "SinhVien,GiangVien,Admin")]
[Route("thoi-khoa-bieu")]
public class ThoiKhoaBieuController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? maHK, int week = 1, string mode = "week")
    {
        if (User.IsInRole("Admin")) return RedirectToAction("Matrix", "LichHoc", new { maHK, week });
        var terms = await db.HocKys.OrderByDescending(x => x.NamHocBatDau).ThenByDescending(x => x.MaHK).ToListAsync();
        var term = terms.FirstOrDefault(x => x.MaHK == maHK) ?? terms.FirstOrDefault();
        var vm = new TimetableViewModel { HocKys = terms, HocKy = term, Week = Math.Clamp(week,1,term?.SoTuan ?? 20), Mode = mode == "all" ? "all" : "week" };
        if (term == null) return View(vm);
        var q = db.ClassSchedules.Include(x => x.LopHocPhan).ThenInclude(x => x!.MonHoc).Include(x => x.GiangVien).Where(x => x.LopHocPhan!.MaHK == term.MaHK);
        if (User.IsInRole("SinhVien")) {
            var id = User.FindFirst("MaSV")?.Value;
            q = q.Where(x => db.KetQuaHocTaps.Any(k => k.MaSV == id && k.MaLHP == x.MaLHP));
        } else if (User.IsInRole("GiangVien")) {
            var id = User.FindFirst("MaGV")?.Value;
            q = q.Where(x => x.MaGV == id);
        }
        vm.Schedules = await q.OrderBy(x => x.Thu).ThenBy(x => x.TietBatDau).ToListAsync();
        return View(vm);
    }
}
