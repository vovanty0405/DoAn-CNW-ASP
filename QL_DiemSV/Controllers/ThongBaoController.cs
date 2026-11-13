using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ThongBaoController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ThongBaoController(ApplicationDbContext db)
    {
        _db = db;
    }

    private bool CanAccessRecipient(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (code.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)) return User.IsInRole("Admin");

        return string.Equals(code, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)
            || (User.IsInRole("SinhVien") && string.Equals(code, User.FindFirstValue("MaSV"), StringComparison.OrdinalIgnoreCase))
            || (User.IsInRole("GiangVien") && string.Equals(code, User.FindFirstValue("MaGV"), StringComparison.OrdinalIgnoreCase));
    }

    [HttpGet("{maNguoiNhan}")]
    public async Task<IActionResult> GetByRecipient(string maNguoiNhan)
    {
        if (!CanAccessRecipient(maNguoiNhan)) return Forbid();
        var query = _db.ThongBaos.AsNoTracking().AsQueryable();
        if (maNguoiNhan.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.MaNguoiNhan == "ADMIN" || t.MaNguoiNhan == "admin");
        }
        else
        {
            query = query.Where(t => t.MaNguoiNhan == maNguoiNhan);
        }

        var data = await query
            .OrderByDescending(t => t.NgayGui)
            .Take(30)
            .ToListAsync();

        return Ok(new { status = "success", data });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var item = await _db.ThongBaos.FindAsync(id);
        if (item == null || !CanAccessRecipient(item.MaNguoiNhan)) return NotFound(new { status = "error", message = "Không tìm thấy thông báo" });

        item.DaDoc = true;
        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Đã đánh dấu đã đọc" });
    }

    [HttpPut("read-all/{maNguoiNhan}")]
    public async Task<IActionResult> MarkAllAsRead(string maNguoiNhan)
    {
        if (!CanAccessRecipient(maNguoiNhan)) return Forbid();
        var items = await _db.ThongBaos
            .Where(t => (t.MaNguoiNhan == maNguoiNhan || (maNguoiNhan.ToUpper() == "ADMIN" && t.MaNguoiNhan == "ADMIN")) && !t.DaDoc)
            .ToListAsync();

        foreach (var t in items) t.DaDoc = true;
        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Đã đánh dấu tất cả là đã đọc" });
    }
}
