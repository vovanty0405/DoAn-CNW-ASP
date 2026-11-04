using QL_DiemSV.Models;
namespace QL_DiemSV.ViewModels;
public class RegistrationViewModel
{
    public List<HocKy> HocKys { get; set; } = new();
    public HocKy? HocKy { get; set; }
    public List<LopHocPhan> Sections { get; set; } = new();
    public List<KetQuaHocTap> Selected { get; set; } = new();
    public List<FilterOption> Options { get; set; } = new();
    public string? Search { get; set; }
    public string? Condition { get; set; }
    public string? Value { get; set; }
    public bool IsOpen { get; set; }
}
public record FilterOption(string Id, string Name);
public class TimetableViewModel
{
    public List<HocKy> HocKys { get; set; } = new();
    public HocKy? HocKy { get; set; }
    public int Week { get; set; } = 1;
    public string Mode { get; set; } = "week";
    public List<ClassSchedule> Schedules { get; set; } = new();
    public List<PhongHoc> Rooms { get; set; } = new();
    public List<LopHocPhan> Sections { get; set; } = new();
    public List<GiangVien> Teachers { get; set; } = new();
    public string? SelectedMaLHP { get; set; }
    public string? SelectedMaGV { get; set; }
    public string? SelectedPhong { get; set; }
    public int Day { get; set; } = 2;
}
public class ScheduleEditorViewModel
{
    public List<LopHanhChinh> AdministrativeClasses { get; set; } = new();
    public List<Khoa> Faculties { get; set; } = new();
    public List<string> Cohorts { get; set; } = new();
    public List<LopHocPhan> Sections { get; set; } = new();
    public List<HocKy> Terms { get; set; } = new();
    public HocKy? ConfigurationTerm { get; set; }
    public LopHocPhan Section { get; set; } = new();
    public List<GiangVien> Teachers { get; set; } = new();
    public List<PhongHoc> Rooms { get; set; } = new();
}
public class RegistrationRequest
{
    public string MaHK { get; set; } = "";
    public List<string> SectionIds { get; set; } = new();
}

public class RoomMatrixViewModel : TimetableViewModel
{
    public string ViewMode { get; set; } = "week";
    public string ScheduleType { get; set; } = "all";
    public string Building { get; set; } = "all";
    public DateTime? Date { get; set; }
    public bool OutsideTerm { get; set; }
    public DateTime Today { get; set; }
    public bool HasCurrentTerm { get; set; }
    public DateTime? WeekStart => Date?.AddDays(2-Day);
    public DateTime? WeekEnd => WeekStart?.AddDays(6);
    public List<int> VisiblePeriods => ViewMode == "compact" && Schedules.Count > 0
        ? Enumerable.Range(Schedules.Min(x=>x.TietBatDau),Schedules.Max(x=>x.TietBatDau+x.SoTiet)-Schedules.Min(x=>x.TietBatDau)).ToList()
        : Enumerable.Range(1,12).ToList();
    public List<int> Days => ViewMode == "week" ? Enumerable.Range(2,7).ToList() : new() { Day };
}

public record MatrixBookingViewModel(PhongHoc Room, List<ClassSchedule> Lessons);
