using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class NhatKyHoatDongController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NhatKyHoatDongController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? account = null,
        [FromQuery] string? actionName = null,
        [FromQuery] string? type = null,
        [FromQuery] string? desc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        var query = _db.NhatKyHoatDongs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(account))
        {
            var acc = account.Trim();
            query = query.Where(l => l.NguoiDung != null && l.NguoiDung.Contains(acc));
        }
        if (!string.IsNullOrWhiteSpace(actionName))
        {
            var act = actionName.Trim();
            query = query.Where(l => l.HanhDong != null && l.HanhDong.Contains(act));
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim();
            query = query.Where(l => l.Loai == t);
        }
        if (!string.IsNullOrWhiteSpace(desc))
        {
            var d = desc.Trim();
            query = query.Where(l => l.ChiTiet != null && l.ChiTiet.Contains(d));
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        int total = await query.CountAsync();
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var list = await query
            .OrderByDescending(l => l.ThoiGian)
            .ThenByDescending(l => l.MaLog)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = list.Select(l => new
        {
            id = l.MaLog,
            MaLog = l.MaLog,
            account = l.NguoiDung ?? "Hệ thống",
            NguoiDung = l.NguoiDung ?? "Hệ thống",
            action = l.HanhDong ?? "",
            HanhDong = l.HanhDong ?? "",
            type = l.Loai ?? "Thông tin",
            Loai = l.Loai ?? "Thông tin",
            time = l.ThoiGian.ToString("yyyy-MM-dd HH:mm:ss"),
            ThoiGian = l.ThoiGian,
            description = l.ChiTiet ?? "",
            ChiTiet = l.ChiTiet ?? ""
        });

        return Ok(new { status = "success", data, total, page, pageSize, totalPages });
    }
}
