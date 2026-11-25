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

    private IQueryable<QL_DiemSV.Models.NhatKyHoatDong> Filter(string? account, string? actionName,
        string? type, string? desc, string? actionGroup, bool exactAction, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from > to) throw new ArgumentException("Thời gian bắt đầu phải trước thời gian kết thúc.");
        if (!string.IsNullOrEmpty(actionGroup) && actionGroup is not ("login" or "create" or "update" or "delete" or "export" or "grade"))
            throw new ArgumentException("Nhóm hành động không hợp lệ.");
        var query = _db.NhatKyHoatDongs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(account)) { account = account.Trim(); query = query.Where(l => l.NguoiDung != null && l.NguoiDung.Contains(account)); }
        if (!string.IsNullOrWhiteSpace(actionName)) {
            actionName = actionName.Trim();
            query = exactAction ? query.Where(l => l.HanhDong == actionName)
                : query.Where(l => l.HanhDong != null && l.HanhDong.Contains(actionName));
        }
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(l => l.Loai == type);
        if (!string.IsNullOrWhiteSpace(desc)) { desc = desc.Trim(); query = query.Where(l => l.ChiTiet != null && l.ChiTiet.Contains(desc)); }
        if (from.HasValue) query = query.Where(l => l.ThoiGian >= from.Value);
        if (to.HasValue) query = query.Where(l => l.ThoiGian <= to.Value);
        query = actionGroup switch {
            "login" => query.Where(l => l.HanhDong == "Đăng nhập" || l.HanhDong == "Đăng xuất"),
            "create" => query.Where(l => l.HanhDong != null && (l.HanhDong.StartsWith("Thêm") || l.HanhDong.StartsWith("Tạo"))),
            "update" => query.Where(l => l.HanhDong != null && (l.HanhDong.StartsWith("Sửa") || l.HanhDong.StartsWith("Cập nhật") || l.HanhDong.StartsWith("Đổi"))),
            "delete" => query.Where(l => l.HanhDong != null && l.HanhDong.StartsWith("Xóa")),
            "export" => query.Where(l => l.HanhDong != null && (l.HanhDong.StartsWith("Xuất") || l.HanhDong.StartsWith("In "))),
            "grade" => query.Where(l => l.HanhDong != null && (l.HanhDong.Contains("điểm") || l.HanhDong.Contains("phúc khảo") || l.HanhDong.Contains("khiếu nại"))),
            _ => query
        };
        return query;
    }

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var actions = await _db.NhatKyHoatDongs.AsNoTracking().Where(l => l.HanhDong != null && l.HanhDong != "")
            .Select(l => l.HanhDong).Distinct().OrderBy(x => x).ToListAsync(HttpContext.RequestAborted);
        return Ok(new { status = "success", actions });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(string? account = null, string? actionName = null, string? type = null,
        string? desc = null, string? actionGroup = null, bool exactAction = false, DateTime? from = null, DateTime? to = null)
    {
        try {
            var query = Filter(account, actionName, type, desc, actionGroup, exactAction, from, to);
            if (await query.CountAsync(HttpContext.RequestAborted) > 50000)
                return BadRequest(new { message = "Kết quả vượt 50.000 dòng. Vui lòng thu hẹp thời gian hoặc bộ lọc để xuất." });
            var data = await query.OrderByDescending(l => l.ThoiGian).ThenByDescending(l => l.MaLog)
                .Take(50000).Select(l => new { l.MaLog, l.NguoiDung, l.HanhDong, l.Loai, l.ThoiGian, l.ChiTiet })
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(new { status = "success", data });
        } catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? account = null, string? actionName = null, string? type = null,
        string? desc = null, int page = 1, int pageSize = 15, string? actionGroup = null,
        bool exactAction = false, DateTime? from = null, DateTime? to = null)
    {
        IQueryable<QL_DiemSV.Models.NhatKyHoatDong> query;
        try { query = Filter(account, actionName, type, desc, actionGroup, exactAction, from, to); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        pageSize = Math.Clamp(pageSize, 1, 100);
        int total = await query.CountAsync(HttpContext.RequestAborted);
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var list = await query
            .OrderByDescending(l => l.ThoiGian)
            .ThenByDescending(l => l.MaLog)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(HttpContext.RequestAborted);

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
