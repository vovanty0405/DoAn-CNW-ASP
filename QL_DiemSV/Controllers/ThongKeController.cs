using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,GiangVien,KhaoThi")]
public class ThongKeController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ThongKeController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("tong-quan")]
    public async Task<IActionResult> GetTongQuan(
        [FromQuery] string? maKhoa,
        [FromQuery] string? maNganh,
        [FromQuery] string? maLop,
        [FromQuery] string? maHK,
        [FromQuery] string? maMon,
        [FromQuery] string? maLHP,
        CancellationToken cancellationToken = default)
    {
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
                student.MaSV, section.MaHK, section.MaMon,
                Score = grade.DiemTongKet!.Value, Credits = section.MonHoc!.SoTinChi,
                section.HocKy!.TenHK, section.HocKy.NamHocBatDau, section.HocKy.NamHocKetThuc
            };

        var bestByCourse = grades.GroupBy(g => new { g.MaSV, g.MaMon, g.Credits })
            .Select(g => new { g.Key.MaSV, g.Key.Credits, Score = g.Max(x => x.Score) });
        var studentTotals = await bestByCourse.GroupBy(g => g.MaSV)
            .Select(g => new
            {
                MaSV = g.Key, Credits = g.Sum(x => x.Credits),
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

                return new
                {
                    sv.MaSV,
                    sv.HoTen,
                    sv.MaLop,
                    sv.TenNganh,
                    sv.TenKhoa,
                    TinChiTichLuy = tinChiDat,
                    GPA_He10 = gpa10,
                    GPA_He4 = gpa4,
                    XepLoai = xepLoai
                };
            })
            .ToList();
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

        var phanBoHocLuc = new[]
        {
            new { name = "Xuất sắc", value = studentSummaries.Count(s => s.XepLoai == "Xuất sắc"), color = "#10B981" },
            new { name = "Giỏi", value = studentSummaries.Count(s => s.XepLoai == "Giỏi"), color = "#4d8eff" },
            new { name = "Khá", value = studentSummaries.Count(s => s.XepLoai == "Khá"), color = "#F59E0B" },
            new { name = "Trung bình", value = studentSummaries.Count(s => s.XepLoai == "Trung bình"), color = "#8B5CF6" },
            new { name = "Yếu", value = studentSummaries.Count(s => s.XepLoai == "Yếu"), color = "#EF4444" }
        };

        var phoDiem = new[]
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
            .ToList();

        var topSinhVien = studentSummaries
            .OrderByDescending(s => s.GPA_He4)
            .ThenByDescending(s => s.GPA_He10)
            .Take(10)
            .ToList();

        var sinhVienCanhBao = studentSummaries
            .Where(s => s.GPA_He4 < 2.0)
            .OrderBy(s => s.GPA_He4)
            .ToList();

        int soSVCanhBaoCount = sinhVienCanhBao.Count;

        return Ok(new
        {
            status = "success",
            data = new
            {
                summary = new
                {
                    tongSinhVien,
                    diemTBToanKhoa,
                    gpaHe4TB,
                    tyLeQuaMon,
                    tyLeRotMon,
                    soSVCanhBao = soSVCanhBaoCount
                },
                phanBoHocLuc,
                phoDiem,
                xuHuongGPA,
                topSinhVien,
                sinhVienCanhBao
            }
        });
    }
}
