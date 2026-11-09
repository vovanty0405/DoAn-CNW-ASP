using System.ComponentModel.DataAnnotations;
using QL_DiemSV.Models;

namespace QL_DiemSV.ViewModels;

public class PortalViewModel
{
    public List<Curriculum> Curriculums { get; set; } = new();
    public List<Article> Articles { get; set; } = new();
    public List<Article> AllPublishedArticles { get; set; } = new();
    public Dictionary<string, int> Counts { get; set; } = new();
    public List<Article> News { get; set; } = new();
    public List<Nganh> Majors { get; set; } = new();
    public List<PortalDocument> Documents { get; set; } = new();
    public List<RoomTodayRow> Rooms { get; set; } = new();
    public string Category { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public string? Search { get; set; }
}

public class PortalArticlesViewModel
{
    public List<Article> Articles { get; set; } = new();
    public Dictionary<string, int> Counts { get; set; } = new();
    public string Category { get; set; } = "all";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
    public int Total { get; set; }
    public int TotalAll { get; set; }
    public int TotalPages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}

public class PortalArticleDetailViewModel
{
    public Article Article { get; set; } = new();
    public List<Article> RelatedArticles { get; set; } = new();
}

public record RoomTodayRow(string Room, string Periods, string Status);

public class PortalTimetableViewModel : TimetableViewModel
{
    public string? Code { get; set; }
    public string Kind { get; set; } = "student";
    public string? FullName { get; set; }
    public string? ClassName { get; set; }
    public string? MajorName { get; set; }
    public string? Error { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? FacultyName { get; set; }
    public string? Degree { get; set; }
    public string? Email { get; set; }
    public bool HasResult => FullName != null;
    public bool NoCurrentTerm { get; set; }
    public bool IsHistoricalTerm { get; set; }
    public DateTime LoadedAt { get; set; }
    public List<ClassSchedule> TermSchedules { get; set; } = new();
    public string StatusClass(ClassSchedule lesson) =>
        QL_DiemSV.Services.TimetablePresentation.Status(lesson, TermSchedules, IsHistoricalTerm, Mode == "all" ? null : Week);
}

public class ArticleListViewModel
{
    public List<Article> Articles { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Department { get; set; }
    public string? Program { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int Total { get; set; }
    public int TotalPublished { get; set; }
    public int TotalDraft { get; set; }
    public int TotalPinned { get; set; }
    public int TotalPages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}

public class ArticleEditViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Nhập tiêu đề bài viết."), MaxLength(250)]
    public string TieuDe { get; set; } = "";
    [MaxLength(1000)]
    public string? TomTat { get; set; }
    [Required(ErrorMessage = "Nhập nội dung bài viết.")]
    public string NoiDung { get; set; } = "";
    [Required, MaxLength(40)]
    public string DanhMuc { get; set; } = "ThongBaoChung";
    [MaxLength(100)]
    public string HeDaoTao { get; set; } = "Đại học chính quy";
    [Required(ErrorMessage = "Nhập đơn vị / khoa đăng bài."), MaxLength(200)]
    public string DonViDang { get; set; } = "";
    public bool GhimDauTrang { get; set; }
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }
    [Required]
    public string TrangThai { get; set; } = "DaXuatBan";
    public DateTime? NgayDang { get; set; }
    public IFormFile? File { get; set; }
    public bool RemoveAttachment { get; set; }
    public string? CurrentAttachment { get; set; }
    public string? CurrentAttachmentUrl { get; set; }
    public long? CurrentAttachmentSize { get; set; }
    public List<string> Departments { get; set; } = new();
}

public class PortalKetQuaHocTapViewModel
{
    public bool RequiresVerification { get; set; }
    public bool IsOwnRecord { get; set; }
    public string MaSV { get; set; } = "";
    public string HoTen { get; set; } = "";
    public DateTime? NgaySinh { get; set; }
    public string GioiTinh { get; set; } = "";
    public string TenLop { get; set; } = "";
    public string MaNganh { get; set; } = "";
    public string TenNganh { get; set; } = "";
    public string TenKhoa { get; set; } = "";
    public string KhoaHoc { get; set; } = "";
    public string TrangThaiHocTap { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string CCCD { get; set; } = "";
    public string Email { get; set; } = "";
    public string SDT { get; set; } = "";
    public string Username { get; set; } = "";
    public string ActiveTab { get; set; } = "ket-qua";

    // Dynamic Graduation Requirements from [QuyDinhTotNghiep] (with fallback from KhungChuongTrinh)
    public int TongTTC { get; set; }
    public int TCBatBuoc { get; set; }
    public int TCTuChon { get; set; }
    public double MinGPA { get; set; }

    // Student's Accumulated Progress
    public int TongTinChiTichLuy { get; set; }
    public int TCBatBuocDat { get; set; }
    public int TCTuChonDat { get; set; }
    public int TongTinChiDangKy { get; set; }
    public int SoMonNo { get; set; }
    public int SoMonBatBuocNo { get; set; }
    public double? GPAHe10 { get; set; }
    public double? GPAHe4 { get; set; }
    public string XepLoai { get; set; } = "Chưa xếp loại";

    // Graduation Eligibility Status
    public bool DuDieuKienTotNghiep { get; set; }
    public int ThieuTinChi => Math.Max(0, TongTTC - TongTinChiTichLuy);
    public int ThieuTCBatBuoc => Math.Max(0, TCBatBuoc - TCBatBuocDat);
    public int ThieuTCTuChon => Math.Max(0, TCTuChon - TCTuChonDat);
    public double PhanTramTichLuy => TongTTC > 0 ? Math.Min(100.0, Math.Round((double)TongTinChiTichLuy / TongTTC * 100.0, 1)) : 0;
    public double PhanTramBatBuoc => TCBatBuoc > 0 ? Math.Min(100.0, Math.Round((double)TCBatBuocDat / TCBatBuoc * 100.0, 1)) : 100.0;
    public double PhanTramTuChon => TCTuChon > 0 ? Math.Min(100.0, Math.Round((double)TCTuChonDat / TCTuChon * 100.0, 1)) : 100.0;
    public string TrangThaiTotNghiepText { get; set; } = "";
    public List<string> ChiTietDieuKien { get; set; } = new();

    // Filtering & Semesters
    public string SelectedHocKy { get; set; } = "all";
    public List<(string MaHK, string TenHK, string NamHoc)> AvailableSemesters { get; set; } = new();
    public List<PortalSemesterGradesViewModel> Semesters { get; set; } = new();

    // Existing Complaints for this student
    public List<PortalComplaintItemViewModel> Complaints { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class PortalSemesterGradesViewModel
{
    public string MaHK { get; set; } = "";
    public string TenHK { get; set; } = "";
    public string NamHoc { get; set; } = "";
    public double? GPAHe10HK { get; set; }
    public double? GPAHe4HK { get; set; }
    public int TinChiDangKyHK { get; set; }
    public int TinChiDatHK { get; set; }
    public int TinChiDaCoDiemHK { get; set; }
    public int TinChiKhongDatHK { get; set; }
    public int TinChiChuaCoDiemHK { get; set; }
    public string XepLoaiHK { get; set; } = "Chưa xếp loại";
    public double? GPAHe10TichLuy { get; set; }
    public double? GPAHe4TichLuy { get; set; }
    public int TinChiTichLuy { get; set; }
    public string XepLoaiTichLuy { get; set; } = "Chưa xếp loại";
    public List<PortalCourseGradeRowViewModel> Courses { get; set; } = new();
}

public class PortalCourseGradeRowViewModel
{
    public int STT { get; set; }
    public int MaKQ { get; set; }
    public string MaLHP { get; set; } = "";
    public string MaMon { get; set; } = "";
    public string TenMon { get; set; } = "";
    public int SoTC { get; set; }
    public bool BatBuoc { get; set; } = true;
    public string MaGV { get; set; } = "";
    public string TenGV { get; set; } = "Chưa phân công";
    public string EmailGV { get; set; } = "";
    public double? DiemQT { get; set; }
    public double? DiemThiL1 { get; set; }
    public double? DiemThiL2 { get; set; }
    public double? DiemTongKet { get; set; }
    public double? DiemHe4 { get; set; }
    public string DiemChu { get; set; } = "-";
    public string KetQua { get; set; } = "Chưa có điểm";
    public bool IsPassed => DiemTongKet.HasValue && DiemTongKet.Value >= 4.0;
    public bool HasGrade => DiemTongKet.HasValue || DiemQT.HasValue || DiemThiL1.HasValue || DiemThiL2.HasValue;
    public PortalComplaintItemViewModel? LatestComplaint { get; set; }
}

public class PortalComplaintItemViewModel
{
    public int Id { get; set; }
    public string MaSV { get; set; } = "";
    public string MaLHP { get; set; } = "";
    public string MaMon { get; set; } = "";
    public string TenMon { get; set; } = "";
    public string MaGV { get; set; } = "";
    public string TenGV { get; set; } = "Chưa phân công";
    public string TenHK { get; set; } = "";
    public string NamHoc { get; set; } = "";
    public string LoaiDiem { get; set; } = "";
    public string LyDo { get; set; } = "";
    public string TrangThai { get; set; } = "pending";
    public string? PhanHoi { get; set; }
    public DateTime NgayGui { get; set; }
    public DateTime? NgayXuLy { get; set; }

    public string TrangThaiLabel => TrangThai?.ToLowerInvariant() switch
    {
        "approved" => "Đã duyệt",
        "rejected" => "Từ chối",
        _ => "Chờ xử lý"
    };

    public string LoaiDiemLabel => LoaiDiem switch
    {
        "quatrinh" or "DiemQT" => "Điểm quá trình",
        "DiemGK" => "Điểm giữa kỳ",
        "thiLan1" or "DiemThi" => "Điểm thi L1",
        "thiLan2" => "Điểm thi L2",
        "cuoiky" or "DiemTK" => "Điểm tổng kết",
        _ => string.IsNullOrWhiteSpace(LoaiDiem) ? "Điểm tổng kết" : LoaiDiem
    };
}
