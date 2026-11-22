using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = StudentLookupScope.Roles)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class TraCuuDiemController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly TranscriptQueryService _queryService;
    private readonly TranscriptPdfService _pdfService;

    public TraCuuDiemController(
        ApplicationDbContext db,
        TranscriptQueryService queryService,
        TranscriptPdfService pdfService)
    {
        _db = db;
        _queryService = queryService;
        _pdfService = pdfService;
    }

    public sealed class BatchPdfRequestDto
    {
        public List<string>? StudentIds { get; set; }
    }

    /// <summary>
    /// Tra cứu dữ liệu JSON bảng điểm & xét tốt nghiệp của 1 sinh viên (hiển thị tại màn hình Details).
    /// </summary>
    [HttpGet("{maSV}")]
    public async Task<IActionResult> TraCuuTheoMaSV(string maSV, CancellationToken cancellationToken = default)
    {
        var result = await _queryService.GetSingleAsync(maSV, User, cancellationToken);
        if (!result.Success || result.Documents.Count == 0)
        {
            return StatusCode(result.StatusCode, new
            {
                status = "error",
                message = result.ErrorMessage ?? "Không thể tải bảng điểm sinh viên!"
            });
        }

        var doc = result.Documents[0];
        return Ok(new
        {
            status = "success",
            data = new
            {
                thongTinSinhVien = doc.ThongTinSinhVien,
                tongKetToanKhoa = doc.TongKetToanKhoa,
                ketQuaTheoHocKy = doc.KetQuaTheoHocKy
            }
        });
    }

    /// <summary>
    /// Xuất PDF bảng điểm cá nhân của 1 sinh viên.
    /// Mặc định trả về Content-Disposition: inline để mở trên tab mới, hoặc attachment khi download=true.
    /// </summary>
    [HttpGet("{maSV}/pdf")]
    public async Task<IActionResult> ExportSingleTranscriptPdf(
        string maSV,
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _queryService.GetSingleAsync(maSV, User, cancellationToken);
        if (!result.Success || result.Documents.Count == 0)
        {
            return StatusCode(result.StatusCode, new
            {
                status = "error",
                message = result.ErrorMessage ?? "Không thể xuất PDF bảng điểm sinh viên!"
            });
        }

        var doc = result.Documents[0];
        var pdfBytes = _pdfService.GenerateSinglePdf(doc);
        var safeCode = string.Concat(doc.ThongTinSinhVien.MaSV.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-'));
        var fileName = $"BangDiem_{safeCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        if (download)
        {
            return File(pdfBytes, "application/pdf", fileName);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Xuất hàng loạt nhiều bảng điểm sinh viên thành MỘT file PDF duy nhất.
    /// Mỗi sinh viên bắt đầu trên một trang A4 mới độc lập.
    /// </summary>
    [Authorize(Roles = "Admin,KhaoThi,GiangVien,GiaoVuKhoa")]
    [HttpPost("pdf/batch")]
    public async Task<IActionResult> ExportBatchTranscriptPdf(
        [FromBody] BatchPdfRequestDto? req,
        CancellationToken cancellationToken = default)
    {
        if (req?.StudentIds == null || req.StudentIds.Count == 0)
        {
            return BadRequest(new
            {
                status = "error",
                message = "Vui lòng chọn ít nhất một sinh viên để xuất bảng điểm."
            });
        }

        // Khử trùng lặp theo đúng thứ tự người dùng chọn
        var distinctOrderedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in req.StudentIds)
        {
            var code = (raw ?? "").Trim();
            if (code.Length > 0 && seen.Add(code))
            {
                distinctOrderedIds.Add(code);
            }
        }

        if (distinctOrderedIds.Count == 0)
        {
            return BadRequest(new
            {
                status = "error",
                message = "Vui lòng chọn ít nhất một sinh viên để xuất bảng điểm."
            });
        }

        int maxBatch = _pdfService.MaxStudentsPerBatch;
        if (distinctOrderedIds.Count > maxBatch)
        {
            return BadRequest(new
            {
                status = "error",
                maxAllowed = maxBatch,
                selectedCount = distinctOrderedIds.Count,
                message = $"Số lượng sinh viên chọn xuất hàng loạt ({distinctOrderedIds.Count} SV) vượt quá giới hạn tối đa cho phép là {maxBatch} sinh viên mỗi lần xuất. Vui lòng bỏ bớt lựa chọn!"
            });
        }

        var result = await _queryService.GetBatchAsync(distinctOrderedIds, User, cancellationToken);
        if (!result.Success || result.Documents.Count == 0)
        {
            return StatusCode(result.StatusCode, new
            {
                status = "error",
                message = result.ErrorMessage ?? "Không thể xuất PDF bảng điểm hàng loạt!"
            });
        }

        var pdfBytes = _pdfService.GenerateBatchPdf(result.Documents);
        var fileName = $"BangDiem_DanhSach_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        GradeHelper.GhiNhatKy(
            _db,
            User.Identity?.Name,
            "Xuất PDF Bảng điểm hàng loạt",
            $"Đã xuất 1 file PDF gộp bảng điểm của {result.Documents.Count} sinh viên.");

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["X-Transcript-Count"] = result.Documents.Count.ToString();
        Response.Headers["X-Transcript-FileName"] = fileName;

        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Tìm kiếm danh sách sinh viên trong Modal "CHỌN SINH VIÊN ĐỂ IN BẢNG ĐIỂM"
    /// với 5 bộ lọc riêng biệt: Khóa học, Khoa, Lớp hành chính, Tìm theo Mã SV, Tìm theo Họ tên.
    /// Hỗ trợ includeAllFilteredIds=true khi người dùng nhấn "Chọn tất cả trong kết quả lọc".
    /// </summary>
    [Authorize(Roles = "Admin,KhaoThi,GiangVien,GiaoVuKhoa")]
    [HttpGet("batch-candidates")]
    public async Task<IActionResult> GetBatchCandidates(
        [FromQuery] string? cohort = null,
        [FromQuery] string? maKhoa = null,
        [FromQuery] string? maLop = null,
        [FromQuery] string? searchMaSV = null,
        [FromQuery] string? searchHoTen = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool includeAllFilteredIds = false,
        CancellationToken cancellationToken = default)
    {
        var query = await StudentLookupScope.StudentsAsync(_db, User);

        if (!string.IsNullOrWhiteSpace(maLop))
            query = query.Where(s => s.MaLop == maLop);
        if (!string.IsNullOrWhiteSpace(maKhoa))
            query = query.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.MaKhoa == maKhoa);
        if (!string.IsNullOrWhiteSpace(cohort))
            query = query.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.NienKhoa == cohort);

        if (!string.IsNullOrWhiteSpace(searchMaSV))
        {
            var codeKw = searchMaSV.Trim();
            query = query.Where(s => s.MaSV.Contains(codeKw));
        }

        if (!string.IsNullOrWhiteSpace(searchHoTen))
        {
            var nameKw = searchHoTen.Trim();
            query = query.Where(s => s.HoTen.Contains(nameKw));
        }

        var ordered = query.OrderBy(s => s.MaSV);

        pageSize = Math.Clamp(pageSize, 5, 100);
        int total = await ordered.CountAsync(cancellationToken);
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var data = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.MaSV,
                s.HoTen,
                s.GioiTinh,
                s.NgaySinh,
                MaLop = s.MaLop ?? "—",
                NienKhoa = s.LopHanhChinh != null ? s.LopHanhChinh.NienKhoa : null,
                TenNganh = s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null ? s.LopHanhChinh.Nganh.TenNganh : "—",
                TenKhoa = s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.Khoa != null ? s.LopHanhChinh.Nganh.Khoa.TenKhoa : "—",
                TrangThai = s.TrangThai ?? "Đang học"
            })
            .ToListAsync(cancellationToken);

        int maxBatch = _pdfService.MaxStudentsPerBatch;

        if (includeAllFilteredIds)
        {
            var allMatching = await ordered
                .Take(maxBatch + 1)
                .Select(s => new
                {
                    s.MaSV,
                    s.HoTen,
                    MaLop = s.MaLop ?? "—"
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                status = "success",
                data,
                total,
                page,
                pageSize,
                totalPages,
                maxBatchSize = maxBatch,
                allFilteredStudents = allMatching.Take(maxBatch),
                exceededMaxBatch = allMatching.Count > maxBatch
            });
        }

        return Ok(new
        {
            status = "success",
            data,
            total,
            page,
            pageSize,
            totalPages,
            maxBatchSize = maxBatch
        });
    }
}
