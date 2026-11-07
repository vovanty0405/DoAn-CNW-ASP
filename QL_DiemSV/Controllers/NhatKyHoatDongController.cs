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
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.NhatKyHoatDongs.AsNoTracking()
            .OrderByDescending(l => l.ThoiGian)
            .ThenByDescending(l => l.MaLog)
            .Take(200)
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

        return Ok(new { status = "success", data });
    }
}
