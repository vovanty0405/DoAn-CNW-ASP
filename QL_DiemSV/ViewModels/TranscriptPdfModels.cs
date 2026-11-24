using QL_DiemSV.Services;

namespace QL_DiemSV.ViewModels;

public sealed class TranscriptDocumentDto
{
    public TranscriptStudentInfoDto ThongTinSinhVien { get; init; } = new();
    public TranscriptOverallSummaryDto TongKetToanKhoa { get; init; } = new();
    public List<TranscriptSemesterDto> KetQuaTheoHocKy { get; init; } = new();
}

public sealed class TranscriptStudentInfoDto
{
    public string MaSV { get; init; } = string.Empty;
    public string HoTen { get; init; } = string.Empty;
    public DateTime? NgaySinh { get; init; }
    public string? GioiTinh { get; init; }
    public string? TrangThai { get; init; }
    public string? Email { get; init; }
    public string? SDT { get; init; }
    public string? MaLop { get; init; }
    public string? TenLop { get; init; }
    public string? NienKhoa { get; init; }
    public string? TenNganh { get; init; }
    public string? TenKhoa { get; init; }
    public string? HeDaoTao { get; init; }
}

public sealed class TranscriptOverallSummaryDto
{
    public int TongTinChiTichLuy { get; init; }
    public int TCBatBuocDat { get; init; }
    public int TCTuChonDat { get; init; }
    public double? GPA_He10 { get; init; }
    public double? GPA_He4 { get; init; }
    public TranscriptTotals ThongKe { get; init; } = new();
    public string XepLoai { get; init; } = "Chưa xếp loại";
    public int SoMonNo { get; init; }
    public int SoTinChiNo { get; init; }
    public TranscriptGraduationEligibilityDto XetTotNghiep { get; init; } = new();
}

public sealed class TranscriptGraduationEligibilityDto
{
    public GraduationRules.Gaps? ChiTiet { get; init; }
    public bool DuDieuKien { get; init; }
    public List<string> LyDo { get; init; } = new();
    public TranscriptGraduationRuleDto QuyDinh { get; init; } = new();
}

public sealed class TranscriptGraduationRuleDto
{
    public int TongTTC { get; init; }
    public int TCBatBuoc { get; init; }
    public int TCTuChon { get; init; }
    public double MinGPA { get; init; }
}

public sealed class TranscriptSemesterDto
{
    public string MaHK { get; init; } = string.Empty;
    public string TenHK { get; init; } = string.Empty;
    public string NamHoc { get; init; } = string.Empty;
    public int NamHocBatDau { get; init; }
    public int TongTinChiHK { get; init; }
    public double? GPA_HK_He10 { get; init; }
    public double? GPA_HK_He4 { get; init; }
    public TranscriptTotals ThongKeHocKy { get; init; } = new();
    public TranscriptTotals TichLuyDenHocKy { get; init; } = new();
    public List<TranscriptCourseRowDto> DanhSachMon { get; init; } = new();
}

public sealed class TranscriptCourseRowDto
{
    public int MaKQ { get; init; }
    public string? MaLHP { get; init; }
    public string? TenLopHP { get; init; }
    public string MaMon { get; init; } = string.Empty;
    public string TenMon { get; init; } = string.Empty;
    public int SoTinChi { get; init; }
    public string LoaiMon { get; init; } = "Bắt buộc";
    public double? DiemQT { get; init; }
    public double? DiemCC { get; init; }
    public double? DiemGK { get; init; }
    public double? DiemCK { get; init; }
    public double? DiemThiLan1 { get; init; }
    public double? DiemThiLan2 { get; init; }
    public double? DiemTongKet { get; init; }
    public double? DiemHe4 { get; init; }
    public string? DiemChu { get; init; }
    public string? GhiChu { get; init; }
    public bool QuaMon { get; init; }
}
