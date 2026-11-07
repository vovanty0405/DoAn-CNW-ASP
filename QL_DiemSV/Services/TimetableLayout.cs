using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public static class TimetableLayout
{
    // Partition one day's lessons into parallel columns. Touching intervals can reuse
    // a column; intersecting intervals keep their own start row and duration.
    public static List<List<ClassSchedule>> Lanes(IEnumerable<ClassSchedule> lessons)
    {
        var lanes = new List<List<ClassSchedule>>();
        foreach (var lesson in lessons.OrderBy(x => x.TietBatDau).ThenBy(x => x.SoTiet)
            .ThenBy(x => x.MaGV).ThenBy(x => x.MaLHP).ThenBy(x => x.Id))
        {
            var lane = lanes.FirstOrDefault(items => items[^1].TietBatDau + items[^1].SoTiet <= lesson.TietBatDau);
            if (lane == null) { lane = new(); lanes.Add(lane); }
            lane.Add(lesson);
        }
        if (lanes.Count == 0) lanes.Add(new());
        return lanes;
    }

    public static string ConflictLabel(ClassSchedule lesson, IEnumerable<ClassSchedule> lessons)
    {
        var overlapping = lessons.Where(other => !ReferenceEquals(other, lesson)
            && (lesson.Id == 0 || other.Id != lesson.Id)
            && other.Thu == lesson.Thu
            && other.TietBatDau < lesson.TietBatDau + lesson.SoTiet
            && lesson.TietBatDau < other.TietBatDau + other.SoTiet).ToList();
        bool teacher = overlapping.Any(other => !string.IsNullOrWhiteSpace(lesson.MaGV)
            && other.MaGV == lesson.MaGV && ScheduleRules.Overlaps(lesson, other));
        bool room = overlapping.Any(other => !string.IsNullOrWhiteSpace(lesson.PhongHoc)
            && other.PhongHoc == lesson.PhongHoc && ScheduleRules.Overlaps(lesson, other));
        return teacher && room ? "Trùng giảng viên và phòng" : teacher ? "Trùng giảng viên" : room ? "Trùng phòng" : "";
    }
}
