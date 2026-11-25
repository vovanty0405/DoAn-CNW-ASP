using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Services;

public static class GradePermissions
{
    public static async Task<HashSet<string>> ForUserAsync(ApplicationDbContext db, ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true ||
            !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return new();
        // Read the current account and persisted matrix on every request, including Admin.
        var actions = await (from permission in db.PhanQuyens.AsNoTracking()
                             join account in db.UserAccounts.AsNoTracking() on permission.RoleID equals account.RoleID
                             where account.UserID == id && account.IsActive && permission.MaChucNang == "DIEM_SO"
                             select permission.MaHanhDong).ToListAsync(ct);
        return actions.ToHashSet(StringComparer.Ordinal);
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class GradePermissionAttribute(params string[] actions) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
        var granted = await GradePermissions.ForUserAsync(db, http.User, http.RequestAborted);
        if (granted.Contains("VIEW") && actions.All(granted.Contains)) return;
        context.Result = new ObjectResult(new {
            status = "error", message = "Bạn chưa được cấp quyền thực hiện thao tác này trong phân hệ Điểm số."
        }) { StatusCode = http.User.Identity?.IsAuthenticated == true ? 403 : 401 };
    }
}
