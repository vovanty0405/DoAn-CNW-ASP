using System.ComponentModel.DataAnnotations;
using QL_DiemSV.Models;
namespace QL_DiemSV.ViewModels;
public class PortalViewModel
{
    public List<Article> Articles { get; set; } = new();
    public Dictionary<string,int> Counts { get; set; } = new();
    public List<Article> News { get; set; } = new();
    public List<Nganh> Majors { get; set; } = new();
    public List<PortalDocument> Documents { get; set; } = new();
    public List<RoomTodayRow> Rooms { get; set; } = new();
    public string Category { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public string? Search { get; set; }
}
public record RoomTodayRow(string Room,string Periods,string Status);
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
    public string StatusClass(ClassSchedule lesson) => QL_DiemSV.Services.TimetablePresentation.Status(lesson,TermSchedules,IsHistoricalTerm,Mode=="all"?null:Week);

}
public class ArticleListViewModel
{
    public List<Article> Articles { get; set; } = new();
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
}
public class ArticleEditViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage="Nhập tiêu đề."),MaxLength(250)] public string TieuDe { get; set; } = "";
    [MaxLength(1000)] public string? TomTat { get; set; }
    [Required(ErrorMessage="Nhập nội dung."),MaxLength(100000)] public string NoiDung { get; set; } = "";
    [Required,MaxLength(40)] public string DanhMuc { get; set; } = "ThongBaoChung";
    [Required(ErrorMessage="Nhập đơn vị đăng."),MaxLength(200)] public string DonViDang { get; set; } = "";
    public bool GhimDauTrang { get; set; }
    [MaxLength(500)] public string? ThumbnailUrl { get; set; }
    [Required] public string TrangThai { get; set; } = "BanNhap";
    public IFormFile? File { get; set; }
    public bool RemoveAttachment { get; set; }
    public string? CurrentAttachment { get; set; }
}
