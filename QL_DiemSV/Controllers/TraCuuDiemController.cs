using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
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
        var sv = await _db.SinhViens
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
        var quyDinh = sv.LopHanhChinh?.Nganh?.QuyDinhTotNghiep ?? await _db.QuyDinhTotNghieps.FindAsync(maNganh);
        int reqTongTC = quyDinh?.TongTTC ?? 130;
        int reqTCBatBuoc = quyDinh?.TCBatBuoc ?? 100;
        int reqTCTuChon = quyDinh?.TCTuChon ?? 30;
        double reqMinGPA = quyDinh?.MinGPA ?? 2.0;

        var khungCT = await _db.KhungChuongTrinhs
            .Where(k => k.MaNganh == maNganh)
            .ToDictionaryAsync(k => k.MaMon, k => k.LoaiMon);

        var ketQuas = await _db.KetQuaHocTaps
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.MonHoc)
            .Include(k => k.LopHocPhan)
                .ThenInclude(l => l!.HocKy)
            .Where(k => k.MaSV == maSV && k.LopHocPhan != null && k.LopHocPhan.HocKy != null && k.LopHocPhan.MonHoc != null)
            .ToListAsync();

        var groupedByHK = ketQuas
            .GroupBy(k => k.LopHocPhan!.MaHK!)
            .Select(g =>
            {
                var hk = g.First().LopHocPhan!.HocKy!;
                var danhSachMon = g.Select(k =>
                {
                    var mh = k.LopHocPhan!.MonHoc!;
                    string loaiMon = khungCT.ContainsKey(mh.MaMon) ? khungCT[mh.MaMon] : "Bắt buộc";
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

                var gradedCourses = danhSachMon.Where(m => m.DiemTongKet != null).ToList();
                int tongTinChiHK = gradedCourses.Where(m => m.QuaMon).Sum(m => m.SoTinChi);
                int totalCreditsAttemptedHK = gradedCourses.Sum(m => m.SoTinChi);

                double gpaHKHe10 = totalCreditsAttemptedHK > 0
                    ? Math.Round(gradedCourses.Sum(m => m.DiemTongKet!.Value * m.SoTinChi) / totalCreditsAttemptedHK, 2)
                    : 0;
                double gpaHKHe4 = totalCreditsAttemptedHK > 0
                    ? Math.Round(gradedCourses.Sum(m => m.DiemHe4!.Value * m.SoTinChi) / totalCreditsAttemptedHK, 2)
                    : 0;

                return new
                {
                    hk.MaHK,
                    hk.TenHK,
                    NamHoc = $"{hk.NamHocBatDau} - {hk.NamHocKetThuc}",
                    hk.NamHocBatDau,
                    TongTinChiHK = tongTinChiHK,
                    GPA_HK_He10 = gpaHKHe10,
                    GPA_HK_He4 = gpaHKHe4,
                    DanhSachMon = danhSachMon
                };
            })
            .OrderBy(x => x.NamHocBatDau)
            .ThenBy(x => x.MaHK)
            .ToList();

        // Tính tích lũy toàn khóa (lấy điểm cao nhất cho mỗi môn học)
        var bestByCourse = ketQuas
            .Where(k => k.DiemTongKet != null && k.LopHocPhan?.MonHoc != null)
            .GroupBy(k => k.LopHocPhan!.MaMon!)
            .Select(g => g.OrderByDescending(x => x.DiemTongKet).First())
            .ToList();

        int tcTichLuy = 0;
        int tcBatBuocDat = 0;
        int tcTuChonDat = 0;
        int soMonNo = 0;
        int tcNo = 0;

        double weightedSum10 = 0;
        double weightedSum4 = 0;
        int totalAttemptedCredits = 0;

        foreach (var item in bestByCourse)
        {
            int tc = item.LopHocPhan!.MonHoc!.SoTinChi;
            double tk = item.DiemTongKet!.Value;
            double he4 = GradeHelper.QuyDoiHe4(tk);
            string maMon = item.LopHocPhan.MaMon!;
            string loaiMon = khungCT.ContainsKey(maMon) ? khungCT[maMon] : "Bắt buộc";

            totalAttemptedCredits += tc;
            weightedSum10 += tk * tc;
            weightedSum4 += he4 * tc;

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

        double gpaToanKhoa10 = totalAttemptedCredits > 0 ? Math.Round(weightedSum10 / totalAttemptedCredits, 2) : 0;
        double gpaToanKhoa4 = totalAttemptedCredits > 0 ? Math.Round(weightedSum4 / totalAttemptedCredits, 2) : 0;

        string xepLoai = "Chưa xếp loại";
        if (totalAttemptedCredits > 0)
        {
            if (gpaToanKhoa4 >= 3.6) xepLoai = "Xuất sắc";
            else if (gpaToanKhoa4 >= 3.2) xepLoai = "Giỏi";
            else if (gpaToanKhoa4 >= 2.5) xepLoai = "Khá";
            else if (gpaToanKhoa4 >= 2.0) xepLoai = "Trung bình";
            else xepLoai = "Yếu";
        }

        var lyDoKhongDat = new List<string>();
        if (tcTichLuy < reqTongTC) lyDoKhongDat.Add($"Thiếu {reqTongTC - tcTichLuy} tín chỉ tích lũy ({tcTichLuy}/{reqTongTC} TC)");
        if (tcBatBuocDat < reqTCBatBuoc) lyDoKhongDat.Add($"Thiếu {reqTCBatBuoc - tcBatBuocDat} tín chỉ bắt buộc ({tcBatBuocDat}/{reqTCBatBuoc} TC)");
        if (tcTuChonDat < reqTCTuChon) lyDoKhongDat.Add($"Thiếu {reqTCTuChon - tcTuChonDat} tín chỉ tự chọn ({tcTuChonDat}/{reqTCTuChon} TC)");
        if (gpaToanKhoa4 < reqMinGPA) lyDoKhongDat.Add($"GPA tích lũy hệ 4 ({gpaToanKhoa4}) thấp hơn mức tối thiểu ({reqMinGPA})");
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
                    GPA_He10 = gpaToanKhoa10,
                    GPA_He4 = gpaToanKhoa4,
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
