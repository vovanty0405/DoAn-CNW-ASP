using QL_DiemSV.Models;
namespace QL_DiemSV.Services;
public static class TimetablePresentation
{
    public static bool IsHistorical(HocKy term,DateTime today) => term.NgayBatDauTuan1 is DateTime start && start.Date.AddDays(term.SoTuan*7)<=today.Date;
    public static HashSet<int> AbsenceWeeks(ClassSchedule lesson) => string.IsNullOrWhiteSpace(lesson.TuanBaoNghi)?new():ScheduleRules.Weeks(lesson.TuanBaoNghi);
    public static string Status(ClassSchedule lesson,IEnumerable<ClassSchedule> all,bool historical,int? week)
    {
        var weeks=ScheduleRules.Weeks(lesson.ChuoiTuanHoc);
        var absence=AbsenceWeeks(lesson);
        if(week.HasValue && absence.Contains(week.Value) || !week.HasValue && weeks.IsSubsetOf(absence)) return "cancelled";
        var active=weeks.Except(absence).Where(w=>!week.HasValue||w==week.Value).ToList();
        var others=all.Where(x=>!ReferenceEquals(x,lesson) && (x.Id==0 || lesson.Id==0 || x.Id!=lesson.Id) && x.Thu==lesson.Thu)
            .Select(x=>new { Lesson=x, Weeks=ScheduleRules.Weeks(x.ChuoiTuanHoc).Except(AbsenceWeeks(x)).ToHashSet() }).ToList();
        int overlap=0;
        foreach(var w in active)
            for(int period=lesson.TietBatDau;period<lesson.TietBatDau+lesson.SoTiet;period++)
                if(others.Any(x=>x.Weeks.Contains(w)&&x.Lesson.TietBatDau<=period&&period<x.Lesson.TietBatDau+x.Lesson.SoTiet))overlap++;
        if(overlap>0)return overlap==active.Count*lesson.SoTiet?"full-overlap":"partial-overlap";
        return historical?"historical":"normal";
    }
    public static string StatusLabel(string status)=>status switch {
        "cancelled"=>"Giảng viên báo nghỉ", "full-overlap"=>"Trùng tất cả các tiết", "partial-overlap"=>"Trùng một vài tiết", "historical"=>"Thời khóa biểu học kỳ cũ", _=>"Lịch học bình thường"
    };
    public static string WeekLabel(HocKy term,int week) => term.NgayBatDauTuan1 is DateTime first
        ? $"Tuần {week} [Từ {first.AddDays((week-1)*7):dd/MM/yyyy} -- Đến {first.AddDays((week-1)*7+6):dd/MM/yyyy}]"
        : $"Tuần {week} [Chưa cấu hình ngày tuần 1]";
}
