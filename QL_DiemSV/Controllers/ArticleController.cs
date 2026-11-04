using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;

namespace QL_DiemSV.Controllers;

[Authorize(Roles = "Admin,GiangVien")]
[Route("bai-viet")]
[Route("AdminArticle")]
public class ArticleController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    private int AuthorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;
    private bool CanEdit(Article article) => User.IsInRole("Admin") || article.NguoiDangId == AuthorId;

    private async Task<List<string>> GetDepartmentsListAsync()
    {
        var dbFaculties = await db.Khoas.AsNoTracking().Select(k => k.TenKhoa).ToListAsync();
        var dbUnits = await db.Articles.AsNoTracking().Select(a => a.DonViDang).Distinct().ToListAsync();
        return PortalRules.DefaultDepartments
            .Concat(dbFaculties)
            .Concat(dbUnits)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? search,
        string? category,
        string? department,
        string? program,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 10)
    {
        pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10;

        var baseQuery = db.Articles.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin"))
            baseQuery = baseQuery.Where(x => x.NguoiDangId == AuthorId);

        int totalPublished = await baseQuery.CountAsync(x => x.TrangThai == "DaXuatBan");
        int totalDraft = await baseQuery.CountAsync(x => x.TrangThai == "BanNhap");
        int totalPinned = await baseQuery.CountAsync(x => x.GhimDauTrang);

        var q = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            q = q.Where(x => x.TieuDe.Contains(kw) || x.NoiDung.Contains(kw) || x.TomTat.Contains(kw) || x.DonViDang.Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(x => x.DanhMuc == category);
        if (!string.IsNullOrWhiteSpace(department))
            q = q.Where(x => x.DonViDang == department);
        if (!string.IsNullOrWhiteSpace(program))
            q = q.Where(x => x.HeDaoTao == program);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.TrangThai == status);
        if (fromDate.HasValue)
        {
            var fromUtc = fromDate.Value.Date.AddHours(-7);
            q = q.Where(x => x.NgayDang >= fromUtc);
        }
        if (toDate.HasValue)
        {
            var toUtc = toDate.Value.Date.AddDays(1).AddHours(-7);
            q = q.Where(x => x.NgayDang < toUtc);
        }

        int total = await q.CountAsync();
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var articles = await q
            .OrderByDescending(x => x.GhimDauTrang)
            .ThenByDescending(x => x.NgayDang).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AcademicQueries.ArticleList)
            .ToListAsync();

        return View("~/Views/Article/Index.cshtml", new ArticleListViewModel
        {
            Articles = articles,
            Departments = await GetDepartmentsListAsync(),
            Search = search,
            Category = category,
            Department = department,
            Program = program,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPublished = totalPublished,
            TotalDraft = totalDraft,
            TotalPinned = totalPinned
        });
    }

    [HttpGet("them")]
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var defaultUnit = "Phòng Đào tạo Đại học";
        if (User.IsInRole("GiangVien"))
        {
            var maGV = User.FindFirst("MaGV")?.Value;
            if (!string.IsNullOrEmpty(maGV))
            {
                var gv = await db.GiangViens.AsNoTracking().Include(g => g.Khoa).FirstOrDefaultAsync(g => g.MaGV == maGV);
                if (!string.IsNullOrEmpty(gv?.Khoa?.TenKhoa))
                    defaultUnit = gv.Khoa.TenKhoa;
            }
        }

        return View("~/Views/Article/Edit.cshtml", new ArticleEditViewModel
        {
            DonViDang = defaultUnit,
            HeDaoTao = "Đại học chính quy",
            TrangThai = "DaXuatBan",
            NgayDang = PortalRules.Local(DateTime.UtcNow),
            Departments = await GetDepartmentsListAsync()
        });
    }

    [HttpGet("{id:int}/sua")]
    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await db.Articles.FindAsync(id);
        if (a == null) return NotFound();
        if (!CanEdit(a)) return Forbid();

        return View("~/Views/Article/Edit.cshtml", new ArticleEditViewModel
        {
            Id = a.Id,
            TieuDe = a.TieuDe,
            TomTat = a.TomTat,
            NoiDung = a.NoiDung,
            DanhMuc = a.DanhMuc,
            HeDaoTao = string.IsNullOrWhiteSpace(a.HeDaoTao) ? "Đại học chính quy" : a.HeDaoTao,
            DonViDang = a.DonViDang,
            GhimDauTrang = a.GhimDauTrang,
            ThumbnailUrl = a.ThumbnailUrl,
            TrangThai = a.TrangThai,
            NgayDang = PortalRules.Local(a.NgayDang),
            CurrentAttachment = a.TenFileDinhKem,
            CurrentAttachmentUrl = a.FileDinhKemUrl,
            CurrentAttachmentSize = a.DungLuongFile,
            Departments = await GetDepartmentsListAsync()
        });
    }

    [HttpGet("{id:int}/json")]
    public async Task<IActionResult> GetArticleJson(int id)
    {
        var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null || !CanEdit(a)) return NotFound();
        return Json(new
        {
            id = a.Id,
            tieuDe = a.TieuDe,
            tomTat = a.TomTat,
            noiDung = a.NoiDung,
            danhMuc = a.DanhMuc,
            tenDanhMuc = PortalRules.Categories.GetValueOrDefault(a.DanhMuc, a.DanhMuc),
            heDaoTao = string.IsNullOrWhiteSpace(a.HeDaoTao) ? "Đại học chính quy" : a.HeDaoTao,
            donViDang = a.DonViDang,
            ngayDang = PortalRules.FormatVietnameseDateWithDay(a.NgayDang),
            trangThai = a.TrangThai,
            fileDinhKemUrl = a.FileDinhKemUrl,
            tenFileDinhKem = a.TenFileDinhKem
        });
    }

    [HttpPost("luu")]
    [HttpPost("Save")]
    [ValidateAntiForgeryToken, RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Save(ArticleEditViewModel vm)
    {
        var a = vm.Id == 0 ? new Article { NguoiDangId = AuthorId } : await db.Articles.FindAsync(vm.Id);
        if (a == null) return NotFound();
        if (!CanEdit(a)) return Forbid();
        if (!await db.UserAccounts.AnyAsync(x => x.UserID == AuthorId && x.IsActive)) return Forbid();

        if (!PortalRules.Categories.ContainsKey(vm.DanhMuc ?? ""))
            ModelState.AddModelError(nameof(vm.DanhMuc), "Danh mục không hợp lệ.");
        if (vm.TrangThai != "BanNhap" && vm.TrangThai != "DaXuatBan")
            ModelState.AddModelError(nameof(vm.TrangThai), "Trạng thái không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(vm.ThumbnailUrl) && !PortalRules.SafeUrl(vm.ThumbnailUrl, true))
            ModelState.AddModelError(nameof(vm.ThumbnailUrl), "Ảnh phải là URL HTTPS hoặc đường dẫn /images/.");

        var safe = PortalRules.SanitizeHtml(vm.NoiDung);
        var plainText = Regex.Replace(safe, "<[^>]*>", " ").Trim();
        plainText = Regex.Replace(plainText, @"\s+", " ");
        if (string.IsNullOrWhiteSpace(plainText))
            ModelState.AddModelError(nameof(vm.NoiDung), "Nội dung bài viết không được để trống.");

        byte[]? upload = null;
        string? extension = null;
        if (vm.File is { Length: > 0 })
        {
            extension = Path.GetExtension(vm.File.FileName).ToLowerInvariant();
            if (Path.GetFileName(vm.File.FileName).Length > 255)
                ModelState.AddModelError(nameof(vm.File), "Tên tệp tối đa 255 ký tự.");
            if (vm.File.Length > 8 * 1024 * 1024 || !new[] { ".pdf", ".docx", ".xlsx", ".txt", ".png", ".jpg", ".jpeg" }.Contains(extension))
                ModelState.AddModelError(nameof(vm.File), "Tệp tối đa 8 MB: PDF, DOCX, XLSX, TXT, PNG, JPG.");
            else
            {
                using var buffer = new MemoryStream();
                await vm.File.CopyToAsync(buffer);
                upload = buffer.ToArray();
                bool signature = extension switch
                {
                    ".pdf" => upload.AsSpan().StartsWith("%PDF-"u8),
                    ".docx" or ".xlsx" => upload.AsSpan().StartsWith("PK"u8),
                    ".png" => upload.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                    ".jpg" or ".jpeg" => upload.Length > 2 && upload[0] == 255 && upload[1] == 216 && upload[2] == 255,
                    _ => true
                };
                if (!signature)
                    ModelState.AddModelError(nameof(vm.File), "Nội dung tệp không khớp định dạng.");
            }
        }

        vm.CurrentAttachment = a.TenFileDinhKem;
        vm.CurrentAttachmentUrl = a.FileDinhKemUrl;
        vm.CurrentAttachmentSize = a.DungLuongFile;
        if (!ModelState.IsValid)
        {
            vm.Departments = await GetDepartmentsListAsync();
            return View("~/Views/Article/Edit.cshtml", vm);
        }

        string? old = a.FileDinhKemUrl, newFile = null;
        if (upload != null)
        {
            var dir = Path.Combine(environment.ContentRootPath, "App_Data", "PortalFiles");
            Directory.CreateDirectory(dir);
            newFile = Guid.NewGuid().ToString("N") + extension;
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, newFile), upload);
            a.FileDinhKemUrl = "/portal-files/" + newFile;
            a.TenFileDinhKem = Path.GetFileName(vm.File!.FileName);
            a.DungLuongFile = upload.LongLength;
        }
        else if (vm.RemoveAttachment)
        {
            a.FileDinhKemUrl = null;
            a.TenFileDinhKem = null;
            a.DungLuongFile = null;
        }

        a.TieuDe = vm.TieuDe.Trim();
        var summaryText = vm.TomTat?.Trim();
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            summaryText = plainText.Length > 220 ? plainText[..217] + "..." : plainText;
        }
        a.TomTat = summaryText;
        a.NoiDung = safe;
        a.DanhMuc = vm.DanhMuc!;
        a.HeDaoTao = string.IsNullOrWhiteSpace(vm.HeDaoTao) ? "Đại học chính quy" : vm.HeDaoTao.Trim();
        a.DonViDang = vm.DonViDang.Trim();
        a.GhimDauTrang = vm.GhimDauTrang;
        a.ThumbnailUrl = string.IsNullOrWhiteSpace(vm.ThumbnailUrl) ? null : vm.ThumbnailUrl.Trim();

        // Xử lý Ngày đăng bài viết
        DateTime publishDateUtc;
        if (vm.NgayDang.HasValue)
        {
            var localDate = vm.NgayDang.Value;
            if (localDate.TimeOfDay == TimeSpan.Zero)
            {
                var nowLocal = PortalRules.Local(DateTime.UtcNow);
                localDate = localDate.Date.Add(nowLocal.TimeOfDay);
            }
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                publishDateUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), tz);
            }
            catch
            {
                publishDateUtc = localDate.ToUniversalTime();
            }
        }
        else
        {
            publishDateUtc = a.NgayDang != default ? a.NgayDang : DateTime.UtcNow;
        }
        a.NgayDang = publishDateUtc;

        if (vm.TrangThai == "DaXuatBan")
        {
            if (a.TrangThai != "DaXuatBan" || a.XuatBanLuc == null)
            {
                a.XuatBanLuc = DateTime.UtcNow;
            }
        }
        else
        {
            a.XuatBanLuc = null;
        }
        a.TrangThai = vm.TrangThai;

        if (vm.Id == 0) db.Articles.Add(a);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (newFile != null) DeleteStoredFile("/portal-files/" + newFile);
            throw;
        }
        if (old != a.FileDinhKemUrl) DeleteStoredFile(old);

        TempData["Message"] = vm.Id == 0 ? "Đã đăng bài viết mới thành công." : "Đã cập nhật bài viết thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/ghim"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(int id)
    {
        var a = await db.Articles.FindAsync(id);
        if (a == null) return NotFound();
        if (!CanEdit(a)) return Forbid();
        a.GhimDauTrang = !a.GhimDauTrang;
        await db.SaveChangesAsync();
        TempData["Message"] = a.GhimDauTrang ? "Đã ghim bài viết lên đầu trang." : "Đã bỏ ghim bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/xoa")]
    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.Articles.FindAsync(id);
        if (a == null) return NotFound();
        if (!CanEdit(a)) return Forbid();
        db.Articles.Remove(a);
        await db.SaveChangesAsync();
        DeleteStoredFile(a.FileDinhKemUrl);
        TempData["Message"] = "Đã xóa bài viết thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("xoa-nhieu"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMultiple([FromForm] List<int> ids)
    {
        if (ids == null || ids.Count == 0) return RedirectToAction(nameof(Index));
        var items = await db.Articles.Where(a => ids.Contains(a.Id)).ToListAsync();
        var allowed = items.Where(CanEdit).ToList();
        foreach (var item in allowed)
        {
            db.Articles.Remove(item);
            DeleteStoredFile(item.FileDinhKemUrl);
        }
        await db.SaveChangesAsync();
        TempData["Message"] = $"Đã xóa {allowed.Count} bài viết được chọn.";
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous, HttpGet("/portal-files/{name}")]
    public async Task<IActionResult> Attachment(string name, bool inline = false)
    {
        if (!ValidStorageName(name)) return NotFound();
        var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.FileDinhKemUrl == "/portal-files/" + name);
        if (a == null || (a.TrangThai != "DaXuatBan" && !CanEdit(a))) return NotFound();
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "PortalFiles", name);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (inline && ext == ".pdf")
        {
            return PhysicalFile(path, "application/pdf");
        }
        return PhysicalFile(path, "application/octet-stream", a.TenFileDinhKem ?? "tai-lieu");
    }

    private static bool ValidStorageName(string name) =>
        Regex.IsMatch(name, @"\A[a-f0-9]{32}\.(pdf|docx|xlsx|txt|png|jpg|jpeg)\z");

    private void DeleteStoredFile(string? url)
    {
        if (url == null || !url.StartsWith("/portal-files/")) return;
        var name = url[14..];
        if (!ValidStorageName(name)) return;
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "PortalFiles", name);
        try { System.IO.File.Delete(path); } catch (IOException) { }
    }
}
