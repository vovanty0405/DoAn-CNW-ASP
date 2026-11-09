using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class PhanQuyenController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PhanQuyenController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.RoleID)
            .Select(r => new { r.RoleID, r.RoleName, r.MoTa, Count = r.UserAccounts.Count }).ToListAsync();

        var data = roles.Select(r => new
        {
            id = r.RoleID,
            RoleID = r.RoleID,
            name = r.RoleName,
            RoleName = r.RoleName,
            description = r.MoTa ?? "",
            MoTa = r.MoTa ?? "",
            userCount = r.Count,
            systemRole = RoleSetup.IsSystemRole(r.RoleName)
        });

        return Ok(new { status = "success", data });
    }

    public record RoleInput(string? Name, string? Description);

    [HttpPost("roles"), ValidateAntiForgeryToken]
    public Task<IActionResult> CreateRole(RoleInput input) => SaveRole(null, input);

    [HttpPut("roles/{id:int}"), ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateRole(int id, RoleInput input) => SaveRole(id, input);

    private async Task<IActionResult> SaveRole(int? id, RoleInput input)
    {
        var name = input.Name?.Trim() ?? "";
        var description = input.Description?.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]{0,49}$") || description?.Length > 255)
            return BadRequest(new { message = "Mã nhóm phải bắt đầu bằng chữ, tối đa 50 ký tự chữ, số hoặc dấu gạch dưới. Mô tả tối đa 255 ký tự." });

        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var role = id.HasValue ? await _db.Roles.FindAsync(id.Value) : new Role();
        if (role == null) return NotFound();
        if (RoleSetup.IsSystemRole(role.RoleName) && role.RoleName != name)
            return BadRequest(new { message = "Mã nhóm quyền hệ thống được cố định." });
        if (await _db.Roles.AnyAsync(r => r.RoleID != id && r.RoleName.ToUpper() == name.ToUpper()))
            return Conflict(new { message = "Mã nhóm quyền đã tồn tại." });

        role.RoleName = name;
        role.MoTa = description;
        if (!id.HasValue) _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, id.HasValue ? "Sửa nhóm quyền" : "Thêm nhóm quyền", name);
        return Ok(new { status = "success", message = "Đã lưu nhóm quyền." });
    }

    [HttpDelete("roles/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(int id)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return NotFound();
        if (RoleSetup.IsSystemRole(role.RoleName))
            return BadRequest(new { message = "Không thể xóa nhóm quyền hệ thống." });
        if (await _db.UserAccounts.AnyAsync(u => u.RoleID == id))
            return Conflict(new { message = "Hãy chuyển các tài khoản sang nhóm khác trước khi xóa." });
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa nhóm quyền", role.RoleName);
        return Ok(new { status = "success", message = "Đã xóa nhóm quyền." });
    }

    [HttpGet("{roleId}/permissions")]
    public async Task<IActionResult> GetPermissions(int roleId)
    {
        var modules = await _db.ChucNangs
            .OrderBy(c => c.MaChucNang)
            .Select(c => new { id = c.MaChucNang, name = c.TenChucNang, description = c.MoTa })
            .ToListAsync();

        var actions = await _db.HanhDongs
            .OrderBy(h => h.MaHanhDong)
            .Select(h => new { id = h.MaHanhDong, name = h.TenHanhDong })
            .ToListAsync();

        var granted = await _db.PhanQuyens
            .Where(p => p.RoleID == roleId)
            .ToListAsync();

        var matrix = new Dictionary<string, Dictionary<string, bool>>();
        foreach (var mod in modules)
        {
            matrix[mod.id] = new Dictionary<string, bool>();
            foreach (var act in actions)
            {
                matrix[mod.id][act.id] = granted.Any(g => g.MaChucNang == mod.id && g.MaHanhDong == act.id);
            }
        }

        return Ok(new
        {
            status = "success",
            data = new
            {
                modules,
                actions,
                permissions = matrix
            }
        });
    }

    public class UpdatePermissionsDto
    {
        public Dictionary<string, Dictionary<string, bool>>? permissions { get; set; }
    }

    [HttpPut("{roleId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int roleId, [FromBody] UpdatePermissionsDto req)
    {
        if (req.permissions == null)
            return BadRequest(new { status = "error", message = "Dữ liệu phân quyền không hợp lệ" });

        if (!await _db.Roles.AnyAsync(r => r.RoleID == roleId)) return NotFound();
        var modules = await _db.ChucNangs.Select(c => c.MaChucNang).ToListAsync();
        var actions = await _db.HanhDongs.Select(h => h.MaHanhDong).ToListAsync();
        var requested = req.permissions.SelectMany(module => module.Value
            .Where(action => action.Value).Select(action => (Module: module.Key, Action: action.Key))).ToHashSet();
        if (requested.Any(p => !modules.Contains(p.Module) || !actions.Contains(p.Action)))
            return BadRequest(new { message = "Chức năng hoặc hành động không hợp lệ." });
        var existing = await _db.PhanQuyens.Where(p => p.RoleID == roleId).ToListAsync();
        foreach (var permission in existing)
            if (!requested.Remove((permission.MaChucNang, permission.MaHanhDong))) _db.PhanQuyens.Remove(permission);
        foreach (var permission in requested)
            _db.PhanQuyens.Add(new PhanQuyen { RoleID = roleId, MaChucNang = permission.Module, MaHanhDong = permission.Action });

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Phân quyền", $"Đã cập nhật ma trận phân quyền cho RoleID {roleId}");
        return Ok(new { status = "success", message = "Đã cập nhật ma trận phân quyền thành công!" });
    }

    [HttpGet("{roleId}/scopes")]
    public async Task<IActionResult> GetScopes(int roleId)
    {
        var scopes = await _db.PhanQuyenPhamVis
            .Where(s => s.RoleID == roleId)
            .ToListAsync();

        var dict = new Dictionary<string, bool>();
        foreach (var s in scopes)
        {
            dict[s.MaPhamVi] = s.IsActive;
        }

        return Ok(new { status = "success", data = dict });
    }

    public class UpdateScopesDto
    {
        public Dictionary<string, bool>? scopes { get; set; }
    }

    [HttpPut("{roleId}/scopes")]
    public async Task<IActionResult> UpdateScopes(int roleId, [FromBody] UpdateScopesDto req)
    {
        if (req.scopes == null)
            return BadRequest(new { status = "error", message = "Dữ liệu phạm vi không hợp lệ" });

        var existing = await _db.PhanQuyenPhamVis.Where(s => s.RoleID == roleId).ToListAsync();
        _db.PhanQuyenPhamVis.RemoveRange(existing);

        foreach (var kv in req.scopes)
        {
            _db.PhanQuyenPhamVis.Add(new PhanQuyenPhamVi
            {
                RoleID = roleId,
                MaPhamVi = kv.Key,
                Module = kv.Key.Contains("thongke") ? "Thống kê" : "Tra cứu điểm",
                Rule = kv.Key,
                IsActive = kv.Value
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Đã cập nhật phạm vi dữ liệu thành công!" });
    }
}
