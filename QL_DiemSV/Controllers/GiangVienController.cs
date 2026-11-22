using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GiangVienController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GiangVienController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
        => Ok(new { status = "success", total = await _db.GiangViens.AsNoTracking().CountAsync() });

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? maKhoa,
        [FromQuery] string? hocVi = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? page = null,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.GiangViens.AsNoTracking()
            .Include(g => g.Khoa)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            query = query.Where(g => g.MaKhoa == maKhoa);
        }

        if (!string.IsNullOrWhiteSpace(hocVi))
        {
            var hv = hocVi.Trim();
            query = query.Where(g => g.HocVi != null && g.HocVi.Contains(hv));
        }

        var canViewProfile = User.IsInRole("Admin");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(g => g.MaGV.Contains(kw) || g.HoTen.Contains(kw)
                || (canViewProfile && g.Email != null && g.Email.Contains(kw)));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "newest" => query.OrderByDescending(g => g.UserID).ThenByDescending(g => g.MaGV),
            "oldest" => query.OrderBy(g => g.UserID).ThenBy(g => g.MaGV),
            "magv_desc" => query.OrderByDescending(g => g.MaGV),
            "hoten_asc" => query.OrderBy(g => g.HoTen).ThenBy(g => g.MaGV),
            "hoten_desc" => query.OrderByDescending(g => g.HoTen).ThenByDescending(g => g.MaGV),
            "hocvi_asc" => query.OrderBy(g => g.HocVi).ThenBy(g => g.MaGV),
            "hocvi_desc" => query.OrderByDescending(g => g.HocVi).ThenByDescending(g => g.MaGV),
            "khoa_asc" => query.OrderBy(g => g.Khoa!.TenKhoa).ThenBy(g => g.MaGV),
            "khoa_desc" => query.OrderByDescending(g => g.Khoa!.TenKhoa).ThenByDescending(g => g.MaGV),
            "email_asc" when canViewProfile => query.OrderBy(g => g.Email).ThenBy(g => g.MaGV),
            "email_desc" when canViewProfile => query.OrderByDescending(g => g.Email).ThenByDescending(g => g.MaGV),
            _ => query.OrderBy(g => g.MaGV)
        };

        if (page.HasValue)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            int total = await query.CountAsync();
            int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
            int currentPage = Math.Clamp(page.Value, 1, totalPages);

            if (!canViewProfile)
            {
                var pagedDir = await query.Skip((currentPage - 1) * pageSize).Take(pageSize)
                    .Select(g => new { g.MaGV, g.HoTen, g.HocVi, g.MaKhoa }).ToListAsync();
                return Ok(new { status = "success", data = pagedDir, total, page = currentPage, pageSize, totalPages });
            }

            var pagedData = await query.Skip((currentPage - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { status = "success", data = pagedData, total, page = currentPage, pageSize, totalPages });
        }

        if (!canViewProfile)
        {
            var directory = await query.Select(g => new { g.MaGV, g.HoTen, g.HocVi, g.MaKhoa }).ToListAsync();
            return Ok(new { status = "success", data = directory });
        }

        var data = await query.ToListAsync();
        return Ok(new { status = "success", data });
    }

    [Authorize(Roles = "Admin"), HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await _db.GiangViens
            .Include(g => g.Khoa)
            .FirstOrDefaultAsync(g => g.MaGV == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] GiangVien model)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (string.IsNullOrWhiteSpace(model.MaGV) || string.IsNullOrWhiteSpace(model.HoTen))
            return BadRequest(new { status = "error", message = "Mã giảng viên và Họ tên không được để trống" });

        model.MaGV = model.MaGV.Trim();
        if (!ProfileValidation.Code().IsMatch(model.MaGV))
            return BadRequest(new { message = "Mã hồ sơ chỉ nhận tối đa 20 chữ cái, chữ số, dấu gạch ngang hoặc gạch dưới." });
        var validationError = await ValidateProfileAsync(model);
        if (validationError != null) return BadRequest(new { message = validationError });
        if (await _db.GiangViens.AnyAsync(g => g.MaGV == model.MaGV))
            return BadRequest(new { status = "error", message = $"Mã giảng viên {model.MaGV} đã tồn tại!" });

        // Tự động tạo tài khoản đăng nhập cho Giảng viên (mật khẩu mặc định 123456)
        var gvRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "GiangVien");
        if (gvRole == null) return BadRequest(new { message = "Nhóm quyền hồ sơ chưa được cấu hình." });
        var existingAcc = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == model.MaGV);
        if (existingAcc != null && (existingAcc.RoleID != gvRole.RoleID
            || await _db.GiangViens.AnyAsync(x => x.UserID == existingAcc.UserID)))
            return BadRequest(new { message = "Tên tài khoản đã được sử dụng bởi hồ sơ khác hoặc thuộc nhóm quyền khác." });
        if (existingAcc == null)
        {
            existingAcc = new UserAccount
            {
                Username = model.MaGV,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                RoleID = gvRole.RoleID,
                IsActive = true,
                MaGV = model.MaGV,
                NgayTao = DateTime.Now
            };
            _db.UserAccounts.Add(existingAcc);
            await _db.SaveChangesAsync();
        }

        model.UserID = existingAcc.UserID;
        if (string.IsNullOrWhiteSpace(model.MaKhoa)) model.MaKhoa = null;

        model.Khoa = null;
        _db.GiangViens.Add(model);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Giảng viên", $"Đã thêm giảng viên {model.MaGV} - {model.HoTen}");
        return Ok(new { status = "success", message = "Thêm giảng viên và tạo tài khoản thành công", data = model });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<GiangVien> list)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (list == null || list.Count is < 1 or > 1000)
            return BadRequest(new { status = "error", message = "Danh sách phải có từ 1 đến 1.000 hồ sơ." });

        var gvRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "GiangVien");
        if (gvRole == null) return BadRequest(new { message = "Nhóm quyền hồ sơ chưa được cấu hình." });
        int roleId = gvRole.RoleID;
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var existingGvIds = (await _db.GiangViens.AsNoTracking().Select(g => g.MaGV).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAccounts = await _db.UserAccounts
            .ToDictionaryAsync(u => u.Username, StringComparer.OrdinalIgnoreCase);

        var newAccounts = new List<UserAccount>();
        var pendingLecturers = new List<(GiangVien gv, UserAccount acc)>();

        var contacts = await ProfileValidation.FindContactsAsync(_db, list.Select(x => x?.CCCD), list.Select(x => x?.SDT));
        var validReferences = (await _db.Khoas.Select(x => x.MaKhoa).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var phones = new HashSet<string>(StringComparer.Ordinal);
        var linkedAccountIds = (await _db.GiangViens.Where(x => x.UserID != null).Select(x => x.UserID!.Value).ToListAsync()).ToHashSet();
        int row = 1;
        foreach (var item in list)
        {
            row++;
            if (item == null) return BadRequest(new { message = $"Dòng {row}: Hồ sơ trống." });
            if (string.IsNullOrWhiteSpace(item.MaGV))
                return BadRequest(new { message = $"Dòng {row}: Mã hồ sơ không được để trống." });
            item.MaGV = item.MaGV.Trim();
            if (existingGvIds.Contains(item.MaGV)) continue;

            if (!ProfileValidation.Code().IsMatch(item.MaGV))
                return BadRequest(new { message = $"Dòng {row}: Mã hồ sơ không hợp lệ." });
            var validationError = NormalizeAndValidate(item);
            if (validationError == null && item.MaKhoa != null && !validReferences.Contains(item.MaKhoa))
                validationError = "Khoa không tồn tại.";
            validationError ??= contacts.DuplicateError(item.CCCD, item.SDT);
            if (validationError != null) return BadRequest(new { message = $"Dòng {row} ({item.MaGV}): {validationError}" });
            if ((item.CCCD != null && !identities.Add(item.CCCD)) || (item.SDT != null && !phones.Add(item.SDT)))
                return BadRequest(new { message = $"Dòng {row} ({item.MaGV}): CCCD hoặc số điện thoại bị trùng trong tệp." });

            existingGvIds.Add(item.MaGV);
            if (existingAccounts.TryGetValue(item.MaGV, out var ua)
                && (ua.RoleID != roleId || linkedAccountIds.Contains(ua.UserID)))
                return BadRequest(new { message = $"Dòng {row}: Tên tài khoản đã được sử dụng bởi hồ sơ khác hoặc thuộc nhóm quyền khác." });
            if (ua == null)
            {
                ua = new UserAccount
                {
                    Username = item.MaGV,
                    PasswordHash = defaultHash,
                    RoleID = roleId,
                    IsActive = true,
                    MaGV = item.MaGV,
                    NgayTao = DateTime.Now
                };
                newAccounts.Add(ua);
                existingAccounts[item.MaGV] = ua;
            }

            if (string.IsNullOrWhiteSpace(item.MaKhoa)) item.MaKhoa = null;
            pendingLecturers.Add((item, ua));
        }

        if (newAccounts.Count > 0)
        {
            _db.UserAccounts.AddRange(newAccounts);
            await _db.SaveChangesAsync();
        }

        foreach (var (gv, ua) in pendingLecturers)
        {
            gv.UserID = ua.UserID;
            gv.Khoa = null;
            _db.GiangViens.Add(gv);
        }

        int count = pendingLecturers.Count;
        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Giảng viên", $"Đã import {count} giảng viên từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} giảng viên" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] GiangVien model)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var existing = await _db.GiangViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });

        var validationError = await ValidateProfileAsync(model, existing.MaGV);
        if (validationError != null) return BadRequest(new { message = validationError });

        existing.HoTen = model.HoTen;
        existing.NgaySinh = model.NgaySinh;
        existing.GioiTinh = model.GioiTinh;
        existing.Email = model.Email;
        existing.SDT = model.SDT;
        existing.CCCD = model.CCCD;
        existing.HocVi = model.HocVi;
        existing.MaKhoa = string.IsNullOrWhiteSpace(model.MaKhoa) ? null : model.MaKhoa;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Giảng viên", $"Đã cập nhật giảng viên {id}");
        return Ok(new { status = "success", message = "Cập nhật giảng viên thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var existing = await _db.GiangViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Giảng viên" });

        if (await _db.ClassSchedules.AnyAsync(c => c.MaGV == id))
        {
            return BadRequest(new { status = "error", message = $"Không thể xóa giảng viên {id} vì đang có buổi học được phân công trong Thời khóa biểu! Vui lòng đổi giảng viên cho các buổi học đó trước." });
        }

        // Gỡ liên kết lớp học phần và lớp hành chính trước khi xóa
        var lhps = await _db.LopHocPhans.Where(l => l.MaGV == id).ToListAsync();
        foreach (var l in lhps) l.MaGV = null;

        var lhcs = await _db.LopHanhChinhs.Where(l => l.MaGVCN == id).ToListAsync();
        foreach (var l in lhcs) l.MaGVCN = null;

        int? userId = existing.UserID;
        _db.GiangViens.Remove(existing);
        await _db.SaveChangesAsync();

        if (userId != null)
        {
            var acc = await _db.UserAccounts.FindAsync(userId);
            if (acc != null)
            {
                _db.UserAccounts.Remove(acc);
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Giảng viên", $"Đã xóa giảng viên {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa giảng viên thành công" });
    }

    private async Task<string?> ValidateProfileAsync(GiangVien model, string? existingId = null)
    {
        var error = NormalizeAndValidate(model);
        if (error != null) return error;
        if (model.MaKhoa != null && !await _db.Khoas.AnyAsync(x => x.MaKhoa == model.MaKhoa))
            return "Khoa không tồn tại.";

        return await ProfileValidation.DuplicateErrorAsync(_db, model.CCCD, model.SDT, lecturerId: existingId);
    }

    private static string? NormalizeAndValidate(GiangVien model)
    {
        model.HoTen = model.HoTen?.Trim() ?? "";
        if (model.HoTen.Length == 0) return "Họ tên không được để trống.";

        model.CCCD = ProfileValidation.Optional(model.CCCD);
        model.SDT = ProfileValidation.Optional(model.SDT);
        model.Email = ProfileValidation.Optional(model.Email);
        model.GioiTinh = ProfileValidation.Optional(model.GioiTinh);
        model.MaKhoa = ProfileValidation.Optional(model.MaKhoa);
        model.HocVi = ProfileValidation.Optional(model.HocVi);
        return ProfileValidation.FormatError(model.NgaySinh, model.GioiTinh, model.CCCD, model.SDT, model.Email);
    }
}
