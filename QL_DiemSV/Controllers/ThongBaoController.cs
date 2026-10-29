using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ThongBaoController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ThongBaoController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{maNguoiNhan}")]
    public async Task<IActionResult> GetByRecipient(string maNguoiNhan)
    {
        var query = _db.ThongBaos.AsQueryable();
        if (maNguoiNhan.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
            maNguoiNhan.Equals("admin", StringComparison.OrdinalIgnoreCase))
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
        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy thông báo" });

        item.DaDoc = true;
        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Đã đánh dấu đã đọc" });
    }

    [HttpPut("read-all/{maNguoiNhan}")]
    public async Task<IActionResult> MarkAllAsRead(string maNguoiNhan)
    {
        var items = await _db.ThongBaos
            .Where(t => (t.MaNguoiNhan == maNguoiNhan || (maNguoiNhan.ToUpper() == "ADMIN" && t.MaNguoiNhan == "ADMIN")) && !t.DaDoc)
            .ToListAsync();

        foreach (var t in items) t.DaDoc = true;
        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = "Đã đánh dấu tất cả là đã đọc" });
    }
}
