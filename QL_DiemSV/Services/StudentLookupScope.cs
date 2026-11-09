using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public static class StudentLookupScope
{
    public const string Roles = "Admin,KhaoThi,GiangVien,GiaoVuKhoa,SinhVien";

    public static async Task<IQueryable<SinhVien>> StudentsAsync(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var students = db.SinhViens.AsNoTracking();
        if (user.IsInRole("Admin") || user.IsInRole("KhaoThi")) return students;
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return students.Where(s => false);
        if (user.IsInRole("SinhVien")) return students.Where(s => s.UserID == id);
        var classes = await ClassesAsync(db, user);
        return students.Where(s => classes.Any(l => l.MaLop == s.MaLop));
    }

    public static async Task<IQueryable<LopHanhChinh>> ClassesAsync(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var classes = db.LopHanhChinhs.AsNoTracking();
        if (user.IsInRole("Admin") || user.IsInRole("KhaoThi")) return classes;
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return classes.Where(l => false);
        if (user.IsInRole("GiangVien"))
            return classes.Where(l => l.GiangVien != null && (l.GiangVien.UserID == id
                || db.UserAccounts.Any(u => u.UserID == id && u.MaGV == l.MaGVCN)));
        if (user.IsInRole("GiaoVuKhoa"))
        {
            var faculty = await db.UserAccounts.Where(u => u.UserID == id)
                .Select(u => u.MaKhoa ?? u.GiangVien!.MaKhoa).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(faculty))
                return classes.Where(l => l.Nganh != null && l.Nganh.MaKhoa == faculty);
        }
        return classes.Where(l => false);
    }
}
