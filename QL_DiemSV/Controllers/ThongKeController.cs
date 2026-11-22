using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QL_DiemSV.Data;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien,KhaoThi")]
public class ThongKeController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ThongKeController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public sealed class StudentStatRow
    {
        public string MaSV { get; init; } = string.Empty;
        public string HoTen { get; init; } = string.Empty;
        public string MaLop { get; init; } = string.Empty;
        public string TenNganh { get; init; } = string.Empty;
        public string TenKhoa { get; init; } = string.Empty;
        public int TinChiTichLuy { get; init; }
        public double GPA_He10 { get; init; }
        public double GPA_He4 { get; init; }
        public string XepLoai { get; init; } = string.Empty;
    }

    private sealed class CachedReportSnapshot
    {
        public int TongSinhVien { get; init; }
        public double DiemTBToanKhoa { get; init; }
        public double GpaHe4TB { get; init; }
        public double TyLeQuaMon { get; init; }
        public double TyLeRotMon { get; init; }
        public object[] PhanBoHocLuc { get; init; } = Array.Empty<object>();
        public object[] PhoDiem { get; init; } = Array.Empty<object>();
        public object[] XuHuongGPA { get; init; } = Array.Empty<object>();
        public List<StudentStatRow> TopOrdered { get; init; } = new();
        public List<StudentStatRow> WarningOrdered { get; init; } = new();
    }

    private async Task<CachedReportSnapshot> BuildOrGetSnapshotAsync(
        string? maKhoa,
        string? maNganh,
        string? maLop,
        string? maHK,
        string? maMon,
        string? maLHP,
        CancellationToken cancellationToken)
    {
        var userKey = $"{User.Identity?.Name}:{User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value}";
        var cacheKey = $"THONGKE_SNAP:{userKey}:{maKhoa}:{maNganh}:{maLop}:{maHK}:{maMon}:{maLHP}";

        if (_cache.TryGetValue(cacheKey, out CachedReportSnapshot? cached) && cached != null)
        {
            return cached;
        }

        var svQuery = await StudentLookupScope.StudentsAsync(_db, User);

        if (!string.IsNullOrWhiteSpace(maLop))
            svQuery = svQuery.Where(s => s.MaLop == maLop);
        else if (!string.IsNullOrWhiteSpace(maNganh))
            svQuery = svQuery.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.MaNganh == maNganh);
        else if (!string.IsNullOrWhiteSpace(maKhoa))
            svQuery = svQuery.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.MaKhoa == maKhoa);

        var svList = await svQuery
            .Select(s => new
            {
                s.MaSV,
                s.HoTen,
                MaLop = s.MaLop ?? "N/A",
                TenNganh = s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null ? s.LopHanhChinh.Nganh.TenNganh : "N/A",
                TenKhoa = s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.Khoa != null ? s.LopHanhChinh.Nganh.Khoa.TenKhoa : "N/A"
            })
            .ToListAsync(cancellationToken);

        int tongSinhVien = svList.Count;
        var svDict = svList.ToDictionary(s => s.MaSV, StringComparer.OrdinalIgnoreCase);

        var lhpQuery = _db.LopHocPhans.AsNoTracking()
            .Where(l => l.MonHoc != null && l.HocKy != null);

        if (!string.IsNullOrWhiteSpace(maHK))
            lhpQuery = lhpQuery.Where(l => l.MaHK == maHK);
        if (!string.IsNullOrWhiteSpace(maMon))
            lhpQuery = lhpQuery.Where(l => l.MaMon == maMon);
        if (!string.IsNullOrWhiteSpace(maLHP))
            lhpQuery = lhpQuery.Where(l => l.MaLHP == maLHP);

        var grades =
            from grade in _db.KetQuaHocTaps.AsNoTracking()
            join section in lhpQuery on grade.MaLHP equals section.MaLHP
            join student in svQuery on grade.MaSV equals student.MaSV
            where grade.DiemTongKet != null
            select new
            {
                student.MaSV,
                section.MaHK,
                section.MaMon,
                Score = grade.DiemTongKet!.Value,
                Credits = section.MonHoc!.SoTinChi,
                section.HocKy!.TenHK,
                section.HocKy.NamHocBatDau,
                section.HocKy.NamHocKetThuc
            };

        var bestByCourse = grades.GroupBy(g => new { g.MaSV, g.MaMon, g.Credits })
            .Select(g => new { g.Key.MaSV, g.Key.Credits, Score = g.Max(x => x.Score) });

        var studentTotals = await bestByCourse.GroupBy(g => g.MaSV)
            .Select(g => new
            {
                MaSV = g.Key,
                Credits = g.Sum(x => x.Credits),
                PassedCredits = g.Sum(x => x.Score >= 4 ? x.Credits : 0),
                Weighted10 = g.Sum(x => x.Score * x.Credits),
                Weighted4 = g.Sum(x => (x.Score >= 8.5 ? 4.0 : x.Score >= 8.0 ? 3.5 : x.Score >= 7.0 ? 3.0
                    : x.Score >= 6.5 ? 2.5 : x.Score >= 5.5 ? 2.0 : x.Score >= 5.0 ? 1.5 : x.Score >= 4.0 ? 1.0 : 0) * x.Credits)
            }).ToListAsync(cancellationToken);

        var gradeFrequency = await grades.GroupBy(x => new { x.MaHK, x.TenHK, x.NamHocBatDau, x.NamHocKetThuc, x.Score })
            .Select(g => new { g.Key.MaHK, g.Key.TenHK, g.Key.NamHocBatDau, g.Key.NamHocKetThuc, g.Key.Score, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var studentSummaries = studentTotals.Where(g => svDict.ContainsKey(g.MaSV)).Select(g =>
        {
            var sv = svDict[g.MaSV];
            int tinChiDat = g.PassedCredits;
            double gpa10 = g.Credits > 0 ? Math.Round(g.Weighted10 / g.Credits, 2, MidpointRounding.AwayFromZero) : 0;
            double gpa4 = g.Credits > 0 ? Math.Round(g.Weighted4 / g.Credits, 2, MidpointRounding.AwayFromZero) : 0;
            string xepLoai = gpa4 switch
            {
                >= 3.6 => "Xuất sắc",
                >= 3.2 => "Giỏi",
                >= 2.5 => "Khá",
                >= 2.0 => "Trung bình",
                _ => "Yếu"
            };

            return new StudentStatRow
            {
                MaSV = sv.MaSV,
                HoTen = sv.HoTen,
                MaLop = sv.MaLop,
                TenNganh = sv.TenNganh,
                TenKhoa = sv.TenKhoa,
                TinChiTichLuy = tinChiDat,
                GPA_He10 = gpa10,
                GPA_He4 = gpa4,
                XepLoai = xepLoai
            };
        }).ToList();

        double diemTBToanKhoa = studentSummaries.Any()
            ? Math.Round(studentSummaries.Average(s => s.GPA_He10), 2)
            : 0;
        double gpaHe4TB = studentSummaries.Any()
            ? Math.Round(studentSummaries.Average(s => s.GPA_He4), 2)
            : 0;

        int totalCourseGrades = gradeFrequency.Sum(k => k.Count);
        int passedCourseGrades = gradeFrequency.Where(k => k.Score >= 4.0).Sum(k => k.Count);
        int failedCourseGrades = totalCourseGrades - passedCourseGrades;
        double tyLeQuaMon = totalCourseGrades > 0 ? Math.Round(passedCourseGrades * 100.0 / totalCourseGrades, 1) : 100;
        double tyLeRotMon = totalCourseGrades > 0 ? Math.Round(failedCourseGrades * 100.0 / totalCourseGrades, 1) : 0;

        var phanBoHocLuc = new object[]
        {
            new { name = "Xuất sắc", value = studentSummaries.Count(s => s.XepLoai == "Xuất sắc"), color = "#10B981" },
            new { name = "Giỏi", value = studentSummaries.Count(s => s.XepLoai == "Giỏi"), color = "#4d8eff" },
            new { name = "Khá", value = studentSummaries.Count(s => s.XepLoai == "Khá"), color = "#F59E0B" },
            new { name = "Trung bình", value = studentSummaries.Count(s => s.XepLoai == "Trung bình"), color = "#8B5CF6" },
            new { name = "Yếu", value = studentSummaries.Count(s => s.XepLoai == "Yếu"), color = "#EF4444" }
        };

        var phoDiem = new object[]
        {
            new { range = "0 - 3.9 (F)", count = gradeFrequency.Where(k => k.Score < 4.0).Sum(k => k.Count) },
            new { range = "4.0 - 5.4 (D/D+)", count = gradeFrequency.Where(k => k.Score >= 4.0 && k.Score < 5.5).Sum(k => k.Count) },
            new { range = "5.5 - 6.9 (C/C+)", count = gradeFrequency.Where(k => k.Score >= 5.5 && k.Score < 7.0).Sum(k => k.Count) },
            new { range = "7.0 - 8.4 (B/B+)", count = gradeFrequency.Where(k => k.Score >= 7.0 && k.Score < 8.5).Sum(k => k.Count) },
            new { range = "8.5 - 10 (A/A+)", count = gradeFrequency.Where(k => k.Score >= 8.5).Sum(k => k.Count) }
        };

        var xuHuongGPA = gradeFrequency
            .Where(k => !string.IsNullOrEmpty(k.MaHK))
            .GroupBy(k => k.MaHK!)
            .Select(g =>
            {
                var lhp = g.First();
                int attempts = g.Sum(x => x.Count);
                double avg10 = Math.Round(g.Sum(x => x.Score * x.Count) / attempts, 2);
                double avg4 = Math.Round(g.Sum(x => GradeHelper.QuyDoiHe4(x.Score) * x.Count) / attempts, 2);
                return new
                {
                    MaHK = lhp.MaHK,
                    TenHK = $"{lhp.TenHK} ({lhp.NamHocBatDau}-{lhp.NamHocKetThuc})",
                    NamHocBatDau = lhp.NamHocBatDau,
                    GPA_He10 = avg10,
                    GPA_He4 = avg4
                };
            })
            .OrderBy(x => x.NamHocBatDau)
            .ThenBy(x => x.MaHK)
            .Cast<object>()
            .ToArray();

        // Top sinh viên tiêu biểu: sắp xếp điểm cao -> thấp
        var topOrdered = studentSummaries
            .OrderByDescending(s => s.GPA_He4)
            .ThenByDescending(s => s.GPA_He10)
            .ThenByDescending(s => s.TinChiTichLuy)
            .ThenBy(s => s.MaSV)
            .ToList();

        // Danh sách cảnh báo học vụ: GPA Hệ 4 < 2.0
        var warningOrdered = studentSummaries
            .Where(s => s.GPA_He4 < 2.0)
            .OrderBy(s => s.GPA_He4)
            .ThenBy(s => s.GPA_He10)
            .ThenBy(s => s.MaSV)
            .ToList();

        var snapshot = new CachedReportSnapshot
        {
            TongSinhVien = tongSinhVien,
            DiemTBToanKhoa = diemTBToanKhoa,
            GpaHe4TB = gpaHe4TB,
            TyLeQuaMon = tyLeQuaMon,
            TyLeRotMon = tyLeRotMon,
            PhanBoHocLuc = phanBoHocLuc,
            PhoDiem = phoDiem,
            XuHuongGPA = xuHuongGPA,
            TopOrdered = topOrdered,
            WarningOrdered = warningOrdered
        };

        _cache.Set(cacheKey, snapshot, TimeSpan.FromSeconds(45));
        return snapshot;
    }

    [HttpGet("tong-quan")]
    public async Task<IActionResult> GetTongQuan(
        [FromQuery] string? maKhoa,
        [FromQuery] string? maNganh,
        [FromQuery] string? maLop,
        [FromQuery] string? maHK,
        [FromQuery] string? maMon,
        [FromQuery] string? maLHP,
        [FromQuery] int topPage = 1,
        [FromQuery] int topPageSize = 10,
        [FromQuery] int warnPage = 1,
        [FromQuery] int warnPageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var snap = await BuildOrGetSnapshotAsync(maKhoa, maNganh, maLop, maHK, maMon, maLHP, cancellationToken);

        topPageSize = Math.Clamp(topPageSize, 5, 100);
        int topTotal = snap.TopOrdered.Count;
        int topTotalPages = Math.Max(1, (topTotal + topPageSize - 1) / topPageSize);
        topPage = Math.Clamp(topPage, 1, topTotalPages);
        var topSinhVien = snap.TopOrdered.Skip((topPage - 1) * topPageSize).Take(topPageSize).ToList();

        warnPageSize = Math.Clamp(warnPageSize, 5, 100);
        int warnTotal = snap.WarningOrdered.Count;
        int warnTotalPages = Math.Max(1, (warnTotal + warnPageSize - 1) / warnPageSize);
        warnPage = Math.Clamp(warnPage, 1, warnTotalPages);
        var sinhVienCanhBao = snap.WarningOrdered.Skip((warnPage - 1) * warnPageSize).Take(warnPageSize).ToList();

        return Ok(new
        {
            status = "success",
            data = new
            {
                summary = new
                {
                    tongSinhVien = snap.TongSinhVien,
                    diemTBToanKhoa = snap.DiemTBToanKhoa,
                    gpaHe4TB = snap.GpaHe4TB,
                    tyLeQuaMon = snap.TyLeQuaMon,
                    tyLeRotMon = snap.TyLeRotMon,
                    soSVCanhBao = warnTotal
                },
                phanBoHocLuc = snap.PhanBoHocLuc,
                phoDiem = snap.PhoDiem,
                xuHuongGPA = snap.XuHuongGPA,
                topSinhVien,
                topPagination = new
                {
                    page = topPage,
                    pageSize = topPageSize,
                    total = topTotal,
                    totalPages = topTotalPages
                },
                sinhVienCanhBao,
                warnPagination = new
                {
                    page = warnPage,
                    pageSize = warnPageSize,
                    total = warnTotal,
                    totalPages = warnTotalPages
                }
            }
        });
    }

    [HttpGet("top-sinh-vien")]
    public async Task<IActionResult> GetTopSinhVienPage(
        [FromQuery] string? maKhoa,
        [FromQuery] string? maNganh,
        [FromQuery] string? maLop,
        [FromQuery] string? maHK,
        [FromQuery] string? maMon,
        [FromQuery] string? maLHP,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var snap = await BuildOrGetSnapshotAsync(maKhoa, maNganh, maLop, maHK, maMon, maLHP, cancellationToken);
        pageSize = Math.Clamp(pageSize, 5, 100);
        int total = snap.TopOrdered.Count;
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var data = snap.TopOrdered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new { status = "success", data, page, pageSize, total, totalPages });
    }

    [HttpGet("canh-bao-hoc-vu")]
    public async Task<IActionResult> GetCanhBaoHocVuPage(
        [FromQuery] string? maKhoa,
        [FromQuery] string? maNganh,
        [FromQuery] string? maLop,
        [FromQuery] string? maHK,
        [FromQuery] string? maMon,
        [FromQuery] string? maLHP,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var snap = await BuildOrGetSnapshotAsync(maKhoa, maNganh, maLop, maHK, maMon, maLHP, cancellationToken);
        pageSize = Math.Clamp(pageSize, 5, 100);
        int total = snap.WarningOrdered.Count;
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var data = snap.WarningOrdered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new { status = "success", data, page, pageSize, total, totalPages });
    }
}

