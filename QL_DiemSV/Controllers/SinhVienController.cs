using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien,KhaoThi,GiaoVuKhoa")]
public class SinhVienController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SinhVienController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maLop, int page = 1, int pageSize = 20,
        string? search = null, string? maNganh = null, string? maKhoa = null, string? status = null, string? cohort = null, string? sort = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = await StudentLookupScope.StudentsAsync(_db, User);
        var canViewIdentity = User.IsInRole("Admin");
        if (!string.IsNullOrWhiteSpace(maLop)) query = query.Where(s => s.MaLop == maLop);
        if (!string.IsNullOrWhiteSpace(maNganh)) query = query.Where(s => s.LopHanhChinh!.MaNganh == maNganh);
        if (!string.IsNullOrWhiteSpace(maKhoa)) query = query.Where(s => s.LopHanhChinh!.Nganh!.MaKhoa == maKhoa);
        if (!string.IsNullOrWhiteSpace(cohort)) query = query.Where(s => s.LopHanhChinh!.NienKhoa == cohort);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(s => s.MaSV.Contains(search) || s.HoTen.Contains(search)
                || (canViewIdentity && s.Email != null && s.Email.Contains(search)));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "newest" => query.OrderByDescending(s => s.UserID).ThenByDescending(s => s.MaSV),
            "oldest" => query.OrderBy(s => s.UserID).ThenBy(s => s.MaSV),
            "masv_desc" => query.OrderByDescending(s => s.MaSV),
            "hoten_asc" => query.OrderBy(s => s.HoTen).ThenBy(s => s.MaSV),
            "hoten_desc" => query.OrderByDescending(s => s.HoTen).ThenByDescending(s => s.MaSV),
            "gioitinh_asc" => query.OrderBy(s => s.GioiTinh).ThenBy(s => s.MaSV),
            "gioitinh_desc" => query.OrderByDescending(s => s.GioiTinh).ThenByDescending(s => s.MaSV),
            "ngaysinh_asc" => query.OrderBy(s => s.NgaySinh).ThenBy(s => s.MaSV),
            "ngaysinh_desc" => query.OrderByDescending(s => s.NgaySinh).ThenByDescending(s => s.MaSV),
            "malop_asc" => query.OrderBy(s => s.MaLop).ThenBy(s => s.MaSV),
            "malop_desc" => query.OrderByDescending(s => s.MaLop).ThenByDescending(s => s.MaSV),
            "nganh_asc" => query.OrderBy(s => s.LopHanhChinh!.Nganh!.TenNganh).ThenBy(s => s.MaSV),
            "nganh_desc" => query.OrderByDescending(s => s.LopHanhChinh!.Nganh!.TenNganh).ThenByDescending(s => s.MaSV),
            "trangthai_asc" => query.OrderBy(s => s.TrangThai).ThenBy(s => s.MaSV),
            "trangthai_desc" => query.OrderByDescending(s => s.TrangThai).ThenByDescending(s => s.MaSV),
            _ => query.OrderBy(s => s.MaSV)
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.MaSV, s.HoTen, s.NgaySinh, s.GioiTinh, s.MaLop, s.TrangThai,
                Email = canViewIdentity ? s.Email : null, SDT = canViewIdentity ? s.SDT : null,
                CCCD = canViewIdentity ? s.CCCD : null, DiaChi = canViewIdentity ? s.DiaChi : null,
                LopHanhChinh = s.LopHanhChinh == null ? null : new { s.LopHanhChinh.MaLop, s.LopHanhChinh.TenLop, s.LopHanhChinh.MaNganh, s.LopHanhChinh.NienKhoa }
            }).ToListAsync();
        return Ok(new { status = "success", data, total, page, pageSize, totalPages });
    }

    [Authorize(Roles = "Admin"), HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var students = await StudentLookupScope.StudentsAsync(_db, User);
        var item = await students
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.Khoa)
            .FirstOrDefaultAsync(s => s.MaSV == id);

        if (item == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });
        return Ok(new { status = "success", data = item });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] SinhVien model)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (string.IsNullOrWhiteSpace(model.MaSV) || string.IsNullOrWhiteSpace(model.HoTen))
            return BadRequest(new { status = "error", message = "Mã sinh viên và Họ tên không được để trống" });

        model.MaSV = model.MaSV.Trim();
        if (!ProfileValidation.Code().IsMatch(model.MaSV))
            return BadRequest(new { message = "Mã hồ sơ chỉ nhận tối đa 20 chữ cái, chữ số, dấu gạch ngang hoặc gạch dưới." });
        var validationError = await ValidateProfileAsync(model);
        if (validationError != null) return BadRequest(new { message = validationError });
        if (await _db.SinhViens.AnyAsync(s => s.MaSV == model.MaSV))
            return BadRequest(new { status = "error", message = $"Mã sinh viên {model.MaSV} đã tồn tại!" });

        var svRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "SinhVien");
        if (svRole == null) return BadRequest(new { message = "Nhóm quyền hồ sơ chưa được cấu hình." });
        var existingAcc = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == model.MaSV);
        if (existingAcc != null && (existingAcc.RoleID != svRole.RoleID
            || await _db.SinhViens.AnyAsync(x => x.UserID == existingAcc.UserID)))
            return BadRequest(new { message = "Tên tài khoản đã được sử dụng bởi hồ sơ khác hoặc thuộc nhóm quyền khác." });
        if (existingAcc == null)
        {
            existingAcc = new UserAccount
            {
                Username = model.MaSV,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                RoleID = svRole.RoleID,
                IsActive = true,
                NgayTao = DateTime.Now
            };
            _db.UserAccounts.Add(existingAcc);
            await _db.SaveChangesAsync();
        }

        model.UserID = existingAcc.UserID;
        if (string.IsNullOrWhiteSpace(model.MaLop)) model.MaLop = null;
        if (string.IsNullOrWhiteSpace(model.TrangThai)) model.TrangThai = "Đang học";

        model.LopHanhChinh = null;
        _db.SinhViens.Add(model);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Thêm Sinh viên", $"Đã thêm sinh viên {model.MaSV} - {model.HoTen}");
        return Ok(new { status = "success", message = "Thêm sinh viên và tạo tài khoản thành công", data = model });
    }

    [HttpPost("bulk/existing")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetExistingImportCodes([FromBody] List<string> codes, CancellationToken cancellationToken)
    {
        if (codes.Count > 1000)
            return BadRequest(new { message = "Tối đa 1.000 mã sinh viên mỗi lần kiểm tra." });

        var data = await _db.SinhViens.AsNoTracking()
            .Where(s => codes.Contains(s.MaSV))
            .Select(s => s.MaSV).ToListAsync(cancellationToken);
        return Ok(new { data });
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkCreate([FromBody] List<SinhVien> list)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (list == null || list.Count is < 1 or > 1000)
            return BadRequest(new { status = "error", message = "Danh sách phải có từ 1 đến 1.000 hồ sơ." });

        var svRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "SinhVien");
        if (svRole == null) return BadRequest(new { message = "Nhóm quyền hồ sơ chưa được cấu hình." });
        int roleId = svRole.RoleID;
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var existingSvIds = (await _db.SinhViens.AsNoTracking().Select(s => s.MaSV).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAccounts = await _db.UserAccounts
            .ToDictionaryAsync(u => u.Username, StringComparer.OrdinalIgnoreCase);

        var newAccounts = new List<UserAccount>();
        var pendingStudents = new List<(SinhVien sv, UserAccount acc)>();

        var contacts = await ProfileValidation.FindContactsAsync(_db, list.Select(x => x?.CCCD), list.Select(x => x?.SDT));
        var validReferences = (await _db.LopHanhChinhs.Select(x => x.MaLop).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var phones = new HashSet<string>(StringComparer.Ordinal);
        var linkedAccountIds = (await _db.SinhViens.Where(x => x.UserID != null).Select(x => x.UserID!.Value).ToListAsync()).ToHashSet();
        int row = 1;
        foreach (var item in list)
        {
            row++;
            if (item == null) return BadRequest(new { message = $"Dòng {row}: Hồ sơ trống." });
            if (string.IsNullOrWhiteSpace(item.MaSV))
                return BadRequest(new { message = $"Dòng {row}: Mã hồ sơ không được để trống." });
            item.MaSV = item.MaSV.Trim();
            if (existingSvIds.Contains(item.MaSV)) continue;

            if (!ProfileValidation.Code().IsMatch(item.MaSV))
                return BadRequest(new { message = $"Dòng {row}: Mã hồ sơ không hợp lệ." });
            var validationError = NormalizeAndValidate(item);
            if (validationError == null && item.MaLop != null && !validReferences.Contains(item.MaLop))
                validationError = "Lớp hành chính không tồn tại.";
            validationError ??= contacts.DuplicateError(item.CCCD, item.SDT);
            if (validationError != null) return BadRequest(new { message = $"Dòng {row} ({item.MaSV}): {validationError}" });
            if ((item.CCCD != null && !identities.Add(item.CCCD)) || (item.SDT != null && !phones.Add(item.SDT)))
                return BadRequest(new { message = $"Dòng {row} ({item.MaSV}): CCCD hoặc số điện thoại bị trùng trong tệp." });

            existingSvIds.Add(item.MaSV);
            if (existingAccounts.TryGetValue(item.MaSV, out var ua)
                && (ua.RoleID != roleId || linkedAccountIds.Contains(ua.UserID)))
                return BadRequest(new { message = $"Dòng {row}: Tên tài khoản đã được sử dụng bởi hồ sơ khác hoặc thuộc nhóm quyền khác." });
            if (ua == null)
            {
                ua = new UserAccount
                {
                    Username = item.MaSV,
                    PasswordHash = defaultHash,
                    RoleID = roleId,
                    IsActive = true,
                    NgayTao = DateTime.Now
                };
                newAccounts.Add(ua);
                existingAccounts[item.MaSV] = ua;
            }

            if (string.IsNullOrWhiteSpace(item.MaLop)) item.MaLop = null;
            if (string.IsNullOrWhiteSpace(item.TrangThai)) item.TrangThai = "Đang học";
            pendingStudents.Add((item, ua));
        }

        if (newAccounts.Count > 0)
        {
            _db.UserAccounts.AddRange(newAccounts);
            await _db.SaveChangesAsync();
        }

        foreach (var (sv, ua) in pendingStudents)
        {
            sv.UserID = ua.UserID;
            sv.LopHanhChinh = null;
            _db.SinhViens.Add(sv);
        }

        int count = pendingStudents.Count;
        if (count > 0)
        {
            await _db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Import Excel Sinh viên", $"Đã import {count} sinh viên từ Excel");
        return Ok(new { status = "success", message = $"Đã thêm thành công {count} sinh viên" });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] SinhVien model)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var existing = await _db.SinhViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });

        var validationError = await ValidateProfileAsync(model, existing.MaSV);
        if (validationError != null) return BadRequest(new { message = validationError });

        existing.HoTen = model.HoTen;
        existing.NgaySinh = model.NgaySinh;
        existing.GioiTinh = model.GioiTinh;
        existing.DiaChi = model.DiaChi;
        existing.CCCD = model.CCCD;
        existing.Email = model.Email;
        existing.SDT = model.SDT;
        existing.MaLop = string.IsNullOrWhiteSpace(model.MaLop) ? null : model.MaLop;
        existing.TrangThai = model.TrangThai;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật Sinh viên", $"Đã cập nhật sinh viên {id}");
        return Ok(new { status = "success", message = "Cập nhật sinh viên thành công", data = existing });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var existing = await _db.SinhViens.FindAsync(id);
        if (existing == null) return NotFound(new { status = "error", message = "Không tìm thấy Sinh viên" });

        var emptyEnrollments = await _db.KetQuaHocTaps.Where(k => k.MaSV == id).ToListAsync();
        if (emptyEnrollments.Any(ScheduleRules.HasGrades) || await _db.DonKhieuNais.AnyAsync(x => x.MaSV == id))
        {
            return BadRequest(new { status = "error", message = $"Không thể xóa sinh viên {id} vì đã có điểm hoặc đơn phúc khảo trên hệ thống! Vui lòng chuyển trạng thái sang 'Thôi học' hoặc 'Bảo lưu'." });
        }

        if (emptyEnrollments.Count > 0)
        {
            _db.KetQuaHocTaps.RemoveRange(emptyEnrollments);
        }

        int? userId = existing.UserID;
        _db.SinhViens.Remove(existing);
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
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xóa Sinh viên", $"Đã xóa sinh viên {id}", "Cảnh báo");
        return Ok(new { status = "success", message = "Xóa sinh viên thành công" });
    }

    private async Task<string?> ValidateProfileAsync(SinhVien model, string? existingId = null)
    {
        var error = NormalizeAndValidate(model);
        if (error != null) return error;
        if (model.MaLop != null && !await _db.LopHanhChinhs.AnyAsync(x => x.MaLop == model.MaLop))
            return "Lớp hành chính không tồn tại.";

        return await ProfileValidation.DuplicateErrorAsync(_db, model.CCCD, model.SDT, studentId: existingId);
    }

    private static string? NormalizeAndValidate(SinhVien model)
    {
        model.HoTen = model.HoTen?.Trim() ?? "";
        if (model.HoTen.Length == 0) return "Họ tên không được để trống.";

        model.CCCD = ProfileValidation.Optional(model.CCCD);
        model.SDT = ProfileValidation.Optional(model.SDT);
        model.Email = ProfileValidation.Optional(model.Email);
        model.GioiTinh = ProfileValidation.Optional(model.GioiTinh);
        model.MaLop = ProfileValidation.Optional(model.MaLop);
        model.DiaChi = ProfileValidation.Optional(model.DiaChi);
        model.TrangThai = ProfileValidation.Optional(model.TrangThai) ?? "Đang học";
        if (model.TrangThai is not ("Đang học" or "Bảo lưu" or "Đã tốt nghiệp" or "Thôi học"))
            return "Trạng thái học tập không hợp lệ.";
        return ProfileValidation.FormatError(model.NgaySinh, model.GioiTinh, model.CCCD, model.SDT, model.Email, model.DiaChi);
    }
}
