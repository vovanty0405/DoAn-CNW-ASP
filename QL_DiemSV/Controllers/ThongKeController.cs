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
        [FromQuery] string? maLHP)
    {
        var svQuery = _db.SinhViens.AsNoTracking().AsQueryable();

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
            .ToListAsync();

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

        var lhpDict = await lhpQuery
            .Select(l => new
            {
                l.MaLHP,
                l.MaMon,
                l.MaHK,
                SoTinChi = l.MonHoc!.SoTinChi,
                TenHK = l.HocKy!.TenHK,
                NamHocBatDau = l.HocKy.NamHocBatDau,
                NamHocKetThuc = l.HocKy.NamHocKetThuc
            })
            .ToDictionaryAsync(l => l.MaLHP, StringComparer.OrdinalIgnoreCase);

        var kqQuery = _db.KetQuaHocTaps.AsNoTracking()
            .Where(k => k.MaSV != null && k.MaLHP != null && k.DiemTongKet != null);

        if (!string.IsNullOrWhiteSpace(maLHP))
            kqQuery = kqQuery.Where(k => k.MaLHP == maLHP);

        var rawKqList = await kqQuery
            .Select(k => new { k.MaSV, k.MaLHP, DiemTongKet = k.DiemTongKet!.Value })
            .ToListAsync();

        var kqList = rawKqList
            .Where(k => svDict.ContainsKey(k.MaSV!) && lhpDict.ContainsKey(k.MaLHP!))
            .Select(k => new
            {
                MaSV = k.MaSV!,
                MaLHP = k.MaLHP!,
                k.DiemTongKet,
                SV = svDict[k.MaSV!],
                LHP = lhpDict[k.MaLHP!]
            })
            .ToList();

        // Tính GPA cho từng sinh viên dựa trên tập kết quả đã lọc (khử trùng lặp môn học lại bằng cách lấy điểm cao nhất của mỗi môn)
        var studentSummaries = kqList
            .GroupBy(k => k.MaSV, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var sv = g.First().SV;
                var bestByCourse = g
                    .GroupBy(x => x.LHP.MaMon ?? x.MaLHP, StringComparer.OrdinalIgnoreCase)
                    .Select(cg => cg.OrderByDescending(x => x.DiemTongKet).First())
                    .ToList();

                int tinChiDat = bestByCourse.Where(x => x.DiemTongKet >= 4.0).Sum(x => x.LHP.SoTinChi);
                int tongTinChi = bestByCourse.Sum(x => x.LHP.SoTinChi);
                double gpa10 = tongTinChi > 0
                    ? Math.Round(bestByCourse.Sum(x => x.DiemTongKet * x.LHP.SoTinChi) / tongTinChi, 2, MidpointRounding.AwayFromZero)
                    : 0;
                double gpa4 = tongTinChi > 0
                    ? Math.Round(bestByCourse.Sum(x => GradeHelper.QuyDoiHe4(x.DiemTongKet) * x.LHP.SoTinChi) / tongTinChi, 2, MidpointRounding.AwayFromZero)
                    : 0;
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

        int totalCourseGrades = kqList.Count;
        int passedCourseGrades = kqList.Count(k => k.DiemTongKet >= 4.0);
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
            new { range = "0 - 3.9 (F)", count = kqList.Count(k => k.DiemTongKet < 4.0) },
            new { range = "4.0 - 5.4 (D/D+)", count = kqList.Count(k => k.DiemTongKet >= 4.0 && k.DiemTongKet < 5.5) },
            new { range = "5.5 - 6.9 (C/C+)", count = kqList.Count(k => k.DiemTongKet >= 5.5 && k.DiemTongKet < 7.0) },
            new { range = "7.0 - 8.4 (B/B+)", count = kqList.Count(k => k.DiemTongKet >= 7.0 && k.DiemTongKet < 8.5) },
            new { range = "8.5 - 10 (A/A+)", count = kqList.Count(k => k.DiemTongKet >= 8.5) }
        };

        var xuHuongGPA = kqList
            .Where(k => !string.IsNullOrEmpty(k.LHP.MaHK))
            .GroupBy(k => k.LHP.MaHK!)
            .Select(g =>
            {
                var lhp = g.First().LHP;
                double avg10 = Math.Round(g.Average(x => x.DiemTongKet), 2);
                double avg4 = Math.Round(g.Average(x => GradeHelper.QuyDoiHe4(x.DiemTongKet)), 2);
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
