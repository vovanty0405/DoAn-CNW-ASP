using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Services;

public static class ContentAccess
{
    public const string Articles = "BAI_VIET";
    public const string Curriculums = "CHUONG_TRINH_DAO_TAO";
    public const string ArticleView = Articles + ":VIEW";
    public const string CurriculumView = Curriculums + ":VIEW";

    public static Task<bool> AllowsAsync(ApplicationDbContext db, ClaimsPrincipal user, string module, string action)
    {
        if (user.IsInRole("Admin")) return Task.FromResult(true);
        if (!user.IsInRole("DaoTao") || !int.TryParse(user.FindFirstValue("RoleID"), out var role))
            return Task.FromResult(false);
        return db.PhanQuyens.AnyAsync(p => p.RoleID == role && p.MaChucNang == module && p.MaHanhDong == action);
    }
}
