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
public class TraCuuDiemController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TraCuuDiemController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{maSV}")]
    public async Task<IActionResult> TraCuuTheoMaSV(string maSV)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (role == "SinhVien")
        {
            var myMaSV = User.FindFirstValue("MaSV") ?? User.Identity?.Name ?? "";
            if (!string.Equals(myMaSV, maSV, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { status = "error", message = "Sinh viên chỉ được phép tra cứu bảng điểm của chính mình!" });
            }
        }

        var students = await StudentLookupScope.StudentsAsync(_db, User);
        var sv = await students
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.Khoa)
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.QuyDinhTotNghiep)
            .FirstOrDefaultAsync(s => s.MaSV == maSV);

        if (sv == null)
        {
            return NotFound(new { status = "error", message = $"Không tìm thấy sinh viên có mã {maSV}!" });
        }

        var maNganh = sv.LopHanhChinh?.MaNganh ?? "CNPM";
        var khungRows = await _db.KhungChuongTrinhs.AsNoTracking()
            .Include(k => k.MonHoc)
            .Where(k => k.MaNganh == maNganh)
            .ToListAsync();

        var khungCT = khungRows
            .GroupBy(k => k.MaMon, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().LoaiMon, StringComparer.OrdinalIgnoreCase);

        var quyDinh = sv.LopHanhChinh?.Nganh?.QuyDinhTotNghiep
            ?? await _db.QuyDinhTotNghieps.AsNoTracking().FirstOrDefaultAsync(q => q.MaNganh == maNganh);

        // Lấy chuẩn tín chỉ bắt buộc & tự chọn đã cấu hình ở trang Quản lý Khung đào tạo;
        // nếu ngành chưa lưu bản ghi QuyDinhTotNghiep thì cộng động từ danh sách môn trong Khung đào tạo của ngành đó
        int fallbackBatBuoc = khungRows.Where(k => k.LoaiMon != "Tự chọn").Sum(k => k.MonHoc?.SoTinChi ?? 0);
        int fallbackTuChon = khungRows.Where(k => k.LoaiMon == "Tự chọn").Sum(k => k.MonHoc?.SoTinChi ?? 0);
        int fallbackTong = fallbackBatBuoc + fallbackTuChon;

        int reqTCBatBuoc = quyDinh != null ? quyDinh.TCBatBuoc : (fallbackBatBuoc > 0 ? fallbackBatBuoc : 100);
        int reqTCTuChon = quyDinh != null ? quyDinh.TCTuChon : (fallbackTuChon > 0 ? fallbackTuChon : 30);
        int reqTongTC = quyDinh != null ? quyDinh.TongTTC : (fallbackTong > 0 ? fallbackTong : (reqTCBatBuoc + reqTCTuChon));
        double reqMinGPA = quyDinh?.MinGPA ?? 2.0;

        var ketQuas = await _db.KetQuaHocTaps.AsNoTracking()
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.MonHoc)
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.HocKy)
            .Where(k => k.MaSV == maSV && k.LopHocPhan != null && k.LopHocPhan.HocKy != null && k.LopHocPhan.MonHoc != null)
            .ToListAsync();

        var completedSoFar = new List<QL_DiemSV.Models.KetQuaHocTap>();
        var groupedByHK = ketQuas
            .GroupBy(k => k.LopHocPhan!.MaHK!)
            .OrderBy(g => g.First().LopHocPhan!.HocKy!.NamHocBatDau)
            .ThenBy(g => TranscriptStatistics.TermOrder(g.First().LopHocPhan!.HocKy!))
            .ThenBy(g => g.First().LopHocPhan!.HocKy!.NgayBatDauTuan1)
            .ThenBy(g => g.Key)
            .Select(g =>
            {
                var hk = g.First().LopHocPhan!.HocKy!;
                var danhSachMon = g.Select(k =>
                {
                    var mh = k.LopHocPhan!.MonHoc!;
                    string loaiMon = khungCT.TryGetValue(mh.MaMon, out var loai) ? loai : "Tự chọn";
                    double? diemHe4 = k.DiemTongKet != null ? GradeHelper.QuyDoiHe4(k.DiemTongKet.Value) : null;
                    bool quaMon = k.DiemTongKet != null && k.DiemTongKet.Value >= 4.0;

                    return new
                    {
                        k.MaKQ,
                        k.MaLHP,
                        TenLopHP = k.LopHocPhan!.TenLopHP,
                        mh.MaMon,
                        mh.TenMon,
                        mh.SoTinChi,
                        LoaiMon = loaiMon,
                        DiemQT = k.DiemCK,
                        k.DiemCC,
                        k.DiemGK,
                        k.DiemCK,
                        k.DiemThiLan1,
                        k.DiemThiLan2,
                        k.DiemTongKet,
                        DiemHe4 = diemHe4,
                        k.DiemChu,
                        k.GhiChu,
                        QuaMon = quaMon
                    };
                }).ToList();

                var semesterTotals = TranscriptStatistics.Calculate(g);
                completedSoFar.AddRange(g);
                var accumulatedTotals = TranscriptStatistics.Calculate(completedSoFar, cumulative: true);

                return new
                {
                    hk.MaHK,
                    hk.TenHK,
                    NamHoc = $"{hk.NamHocBatDau} - {hk.NamHocKetThuc}",
                    hk.NamHocBatDau,
                    TongTinChiHK = semesterTotals.TinChiDat,
                    GPA_HK_He10 = semesterTotals.GPAHe10,
                    GPA_HK_He4 = semesterTotals.GPAHe4,
                    ThongKeHocKy = semesterTotals,
                    TichLuyDenHocKy = accumulatedTotals,
                    DanhSachMon = danhSachMon
                };
            })
            .ToList();

        // Tính tích lũy toàn khóa (lấy điểm cao nhất cho mỗi môn học)
        var bestByCourse = ketQuas
            .Where(k => k.DiemTongKet != null && k.LopHocPhan?.MonHoc != null)
            .GroupBy(k => k.LopHocPhan!.MaMon!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.DiemTongKet).First())
            .ToList();

        int tcTichLuy = 0;
        int tcBatBuocDat = 0;
        int tcTuChonDat = 0;
        int soMonNo = 0;
        int tcNo = 0;
        int totalAttemptedCredits = 0;

        foreach (var item in bestByCourse)
        {
            int tc = item.LopHocPhan!.MonHoc!.SoTinChi;
            double tk = item.DiemTongKet!.Value;
            string maMon = item.LopHocPhan.MaMon!;
            string loaiMon = khungCT.TryGetValue(maMon, out var loai) ? loai : "Tự chọn";

            totalAttemptedCredits += tc;

            if (tk >= 4.0)
            {
                tcTichLuy += tc;
                if (loaiMon == "Tự chọn") tcTuChonDat += tc;
                else tcBatBuocDat += tc;
            }
            else
            {
                soMonNo++;
                tcNo += tc;
            }
        }

        var overallTotals = TranscriptStatistics.Calculate(ketQuas, cumulative: true);
        double gpaToanKhoa4 = overallTotals.GPAHe4 ?? 0;
        string xepLoai = overallTotals.XepLoai;

        var lyDoKhongDat = new List<string>();
        if (tcTichLuy < reqTongTC) lyDoKhongDat.Add($"Thiếu {reqTongTC - tcTichLuy} tín chỉ tích lũy ({tcTichLuy}/{reqTongTC} TC)");
        if (tcBatBuocDat < reqTCBatBuoc) lyDoKhongDat.Add($"Thiếu {reqTCBatBuoc - tcBatBuocDat} tín chỉ bắt buộc ({tcBatBuocDat}/{reqTCBatBuoc} TC)");
        if (tcTuChonDat < reqTCTuChon) lyDoKhongDat.Add($"Thiếu {reqTCTuChon - tcTuChonDat} tín chỉ tự chọn ({tcTuChonDat}/{reqTCTuChon} TC)");
        if (gpaToanKhoa4 < reqMinGPA) lyDoKhongDat.Add($"GPA tích lũy hệ 4 ({gpaToanKhoa4:0.00}) thấp hơn mức tối thiểu ({reqMinGPA:0.00})");
        if (soMonNo > 0) lyDoKhongDat.Add($"Còn nợ {soMonNo} môn học ({tcNo} tín chỉ)");

        bool duDieuKienTotNghiep = lyDoKhongDat.Count == 0 && totalAttemptedCredits > 0;

        return Ok(new
        {
            status = "success",
            data = new
            {
                thongTinSinhVien = new
                {
                    sv.MaSV,
                    sv.HoTen,
                    sv.NgaySinh,
                    sv.GioiTinh,
                    sv.TrangThai,
                    sv.Email,
                    sv.SDT,
                    MaLop = sv.MaLop,
                    TenLop = sv.LopHanhChinh?.TenLop,
                    NienKhoa = sv.LopHanhChinh?.NienKhoa,
                    TenNganh = sv.LopHanhChinh?.Nganh?.TenNganh,
                    TenKhoa = sv.LopHanhChinh?.Nganh?.Khoa?.TenKhoa
                },
                tongKetToanKhoa = new
                {
                    TongTinChiTichLuy = tcTichLuy,
                    TCBatBuocDat = tcBatBuocDat,
                    TCTuChonDat = tcTuChonDat,
                    GPA_He10 = overallTotals.GPAHe10,
                    GPA_He4 = overallTotals.GPAHe4,
                    ThongKe = overallTotals,
                    XepLoai = xepLoai,
                    SoMonNo = soMonNo,
                    SoTinChiNo = tcNo,
                    XetTotNghiep = new
                    {
                        DuDieuKien = duDieuKienTotNghiep,
                        LyDo = lyDoKhongDat,
                        QuyDinh = new
                        {
                            TongTTC = reqTongTC,
                            TCBatBuoc = reqTCBatBuoc,
                            TCTuChon = reqTCTuChon,
                            MinGPA = reqMinGPA
                        }
                    }
                },
                ketQuaTheoHocKy = groupedByHK
            }
        });
    }
}
