using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
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
        var roles = await _db.Roles
            .Include(r => r.UserAccounts)
            .OrderBy(r => r.RoleID)
            .ToListAsync();

        var data = roles.Select(r => new
        {
            id = r.RoleID,
            RoleID = r.RoleID,
            name = r.RoleName,
            RoleName = r.RoleName,
            description = r.MoTa ?? "",
            MoTa = r.MoTa ?? "",
            userCount = r.UserAccounts.Count
        });

        return Ok(new { status = "success", data });
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

        var existing = await _db.PhanQuyens.Where(p => p.RoleID == roleId).ToListAsync();
        _db.PhanQuyens.RemoveRange(existing);

        foreach (var modKv in req.permissions)
        {
            foreach (var actKv in modKv.Value)
            {
                if (actKv.Value)
                {
                    _db.PhanQuyens.Add(new PhanQuyen
                    {
                        RoleID = roleId,
                        MaChucNang = modKv.Key,
                        MaHanhDong = actKv.Key
                    });
                }
            }
        }

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
