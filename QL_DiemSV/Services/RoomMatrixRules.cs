namespace QL_DiemSV.Services;
public static class RoomMatrixRules
{
    public static readonly IReadOnlyDictionary<string,string> Buildings = new Dictionary<string,string> {
        ["A"]="Tòa nhà A",["B"]="Tòa nhà B",["C"]="Tòa nhà C",["D"]="Tòa nhà D",["E"]="Tòa nhà E",["SanTap"]="Sân tập",["PhongHop"]="Phòng họp",["Khac"]="Khác"
    };
    public static readonly IReadOnlyDictionary<string,string> Statuses = new Dictionary<string,string> {
        ["HoatDong"]="Hoạt động",["BaoTri"]="Bảo trì",["DangKhoa"]="Đang khóa"
    };
    public static readonly IReadOnlyDictionary<string,string> ScheduleTypes = new Dictionary<string,string> {
        ["LichHoc"]="Lịch học",["LichThi"]="Lịch thi",["DayBu"]="Dạy bù",["Hop"]="Họp",["Khac"]="Khác"
    };
    private static readonly string[] Starts = { "07:00","07:50","08:50","09:40","10:30","13:00","13:50","14:50","15:40","16:30","17:30","18:20" };
    public static string PeriodTime(int period)
    {
        if (period < 1 || period > 12) throw new ArgumentOutOfRangeException(nameof(period));
        var start=TimeOnly.ParseExact(Starts[period-1],"HH:mm");
        return $"{start:HH:mm} - {start.AddMinutes(50):HH:mm}";
    }
    // Merge only overlapping intervals, so corrupt/legacy collisions remain visible without breaking columns.
    public static List<MatrixBlock> Blocks(IEnumerable<QL_DiemSV.Models.ClassSchedule> lessons)
    {
        var result = new List<MatrixBlock>();
        foreach (var lesson in lessons.OrderBy(x => x.TietBatDau).ThenBy(x => x.Id)) {
            var last = result.LastOrDefault();
            if (last != null && lesson.TietBatDau < last.Start + last.Span) {
                last.Span = Math.Max(last.Start + last.Span,lesson.TietBatDau + lesson.SoTiet)-last.Start;
                last.Lessons.Add(lesson);
            } else result.Add(new MatrixBlock { Start = lesson.TietBatDau, Span = lesson.SoTiet, Lessons = new() { lesson } });
        }
        return result;
    }
    public static bool ContainsDate(QL_DiemSV.Models.HocKy term, DateTime date) => term.NgayBatDauTuan1 is DateTime first
        && date.Date >= first.Date && (date.Date-first.Date).TotalDays < term.SoTuan*7;

    public static MatrixCalendar ResolveCalendar(IEnumerable<QL_DiemSV.Models.HocKy> terms, string? termId, int? week, int? day, DateTime? date, DateTime today)
    {
        today=today.Date;
        var target=(date??today).Date;
        var list=terms.OrderByDescending(x=>x.NgayBatDauTuan1).ThenByDescending(x=>x.NamHocBatDau).ThenByDescending(x=>x.MaHK).ToList();
        var requested=list.FirstOrDefault(x=>x.MaHK==termId);
        var term=requested ?? list.FirstOrDefault(x=>ContainsDate(x,target))
            ?? list.Where(x=>x.NgayBatDauTuan1.HasValue).OrderBy(x=>Distance(x,target)).FirstOrDefault() ?? list.FirstOrDefault();
        var selectedDay=Math.Clamp(day??AcademicDay(today),2,8);
        DateTime selectedDate;
        bool byAcademicWeek=!date.HasValue && (week.HasValue || day.HasValue);
        if (byAcademicWeek && term?.NgayBatDauTuan1 is DateTime first) {
            var current=(int)Math.Floor((today-first.Date).TotalDays/7)+1;
            var selectedWeek=Math.Clamp(week??current,1,Math.Max(1,term.SoTuan));
            selectedDate=first.Date.AddDays((selectedWeek-1)*7+selectedDay-2);
        } else selectedDate=target;
        var result=new MatrixCalendar { Term=term, Date=selectedDate, Day=AcademicDay(selectedDate), Week=week.HasValue ? Math.Clamp(week.Value,1,Math.Max(1,term?.SoTuan??20)) : 1 };
        if(term?.NgayBatDauTuan1 is DateTime start) {
            result.Week=(int)Math.Floor((selectedDate-start.Date).TotalDays/7)+1;
            result.OutsideTerm=!ContainsDate(term,selectedDate);
        } else if(byAcademicWeek) result.Day=selectedDay;
        return result;
    }
    private static double Distance(QL_DiemSV.Models.HocKy term,DateTime date)
    {
        var start=term.NgayBatDauTuan1!.Value.Date;
        var last=start.AddDays(term.SoTuan*7-1);
        return date<start?(start-date).TotalDays:date>last?(date-last).TotalDays:0;
    }
    public static DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
    public static int AcademicDay(DateTime date) => date.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek+1;
}

public class MatrixBlock
{
    public int Start { get; set; }
    public int Span { get; set; }
    public List<QL_DiemSV.Models.ClassSchedule> Lessons { get; set; } = new();
}

public class MatrixCalendar
{
    public QL_DiemSV.Models.HocKy? Term { get; set; }
    public int Week { get; set; }
    public int Day { get; set; }
    public DateTime Date { get; set; }
    public bool OutsideTerm { get; set; }
}
