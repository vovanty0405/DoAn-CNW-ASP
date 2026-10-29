using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
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
        var svQuery = _db.SinhViens
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maLop))
            svQuery = svQuery.Where(s => s.MaLop == maLop);
        else if (!string.IsNullOrWhiteSpace(maNganh))
            svQuery = svQuery.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.MaNganh == maNganh);
        else if (!string.IsNullOrWhiteSpace(maKhoa))
            svQuery = svQuery.Where(s => s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.MaKhoa == maKhoa);

        var filteredSvIds = await svQuery.Select(s => s.MaSV).ToListAsync();
        var filteredSvSet = filteredSvIds.ToHashSet();

        var kqQuery = _db.KetQuaHocTaps
            .Include(k => k.SinhVien)
                .ThenInclude(s => s!.LopHanhChinh)
                    .ThenInclude(l => l!.Nganh)
                        .ThenInclude(n => n!.Khoa)
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.MonHoc)
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.HocKy)
            .Where(k => k.MaSV != null && filteredSvSet.Contains(k.MaSV) && k.DiemTongKet != null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(maHK))
            kqQuery = kqQuery.Where(k => k.LopHocPhan != null && k.LopHocPhan.MaHK == maHK);
        if (!string.IsNullOrWhiteSpace(maMon))
            kqQuery = kqQuery.Where(k => k.LopHocPhan != null && k.LopHocPhan.MaMon == maMon);
        if (!string.IsNullOrWhiteSpace(maLHP))
            kqQuery = kqQuery.Where(k => k.MaLHP == maLHP);

        var kqList = await kqQuery.ToListAsync();

        // Tính GPA cho từng sinh viên dựa trên tập kết quả đã lọc
        var studentSummaries = kqList
            .Where(k => k.SinhVien != null && k.LopHocPhan?.MonHoc != null)
            .GroupBy(k => k.MaSV!)
            .Select(g =>
            {
                var sv = g.First().SinhVien!;
                int totalTC = g.Sum(x => x.LopHocPhan!.MonHoc!.SoTinChi);
                int tcDat = g.Where(x => x.DiemTongKet >= 4.0).Sum(x => x.LopHocPhan!.MonHoc!.SoTinChi);
                double gpa10 = totalTC > 0 ? Math.Round(g.Sum(x => x.DiemTongKet!.Value * x.LopHocPhan!.MonHoc!.SoTinChi) / totalTC, 2) : 0;
                double gpa4 = totalTC > 0 ? Math.Round(g.Sum(x => GradeHelper.QuyDoiHe4(x.DiemTongKet!.Value) * x.LopHocPhan!.MonHoc!.SoTinChi) / totalTC, 2) : 0;

                string xepLoai = "Yếu";
                if (gpa4 >= 3.6) xepLoai = "Xuất sắc";
                else if (gpa4 >= 3.2) xepLoai = "Giỏi";
                else if (gpa4 >= 2.5) xepLoai = "Khá";
                else if (gpa4 >= 2.0) xepLoai = "Trung bình";

                return new
                {
                    sv.MaSV,
                    sv.HoTen,
                    MaLop = sv.MaLop ?? "N/A",
                    TenNganh = sv.LopHanhChinh?.Nganh?.TenNganh ?? "N/A",
                    TenKhoa = sv.LopHanhChinh?.Nganh?.Khoa?.TenKhoa ?? "N/A",
                    TinChiTichLuy = tcDat,
                    GPA_He10 = gpa10,
                    GPA_He4 = gpa4,
                    XepLoai = xepLoai
                };
            })
            .ToList();

        int tongSinhVien = filteredSvIds.Count;
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
            .Where(k => k.LopHocPhan?.HocKy != null)
            .GroupBy(k => k.LopHocPhan!.MaHK!)
            .Select(g =>
            {
                var hk = g.First().LopHocPhan!.HocKy!;
                double avg10 = Math.Round(g.Average(x => x.DiemTongKet!.Value), 2);
                double avg4 = Math.Round(g.Average(x => GradeHelper.QuyDoiHe4(x.DiemTongKet!.Value)), 2);
                return new
                {
                    MaHK = hk.MaHK,
                    TenHK = $"{hk.TenHK} ({hk.NamHocBatDau}-{hk.NamHocKetThuc})",
                    NamHocBatDau = hk.NamHocBatDau,
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
                    soSVCanhBao = sinhVienCanhBao.Count
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
