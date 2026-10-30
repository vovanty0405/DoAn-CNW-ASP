using QL_DiemSV.Models;
namespace QL_DiemSV.Services;
public static class ScheduleRules
{
    public static HashSet<int> Weeks(string input, int max = 52)
    {
        var weeks = new HashSet<int>();
        foreach (var token in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = token.Split('-');
            if (range.Length > 2 || !int.TryParse(range[0], out int a)) throw new ArgumentException("Tuần học phải là danh sách số hoặc khoảng, ví dụ 1-5,8,10-12.");
            int b = a;
            if (range.Length == 2 && !int.TryParse(range[1], out b)) throw new ArgumentException("Khoảng tuần không hợp lệ.");
            if (a < 1 || b > max || a > b) throw new ArgumentException($"Tuần học phải nằm trong 1–{max}.");
            for (int i = a; i <= b; i++) weeks.Add(i);
        }
        if (weeks.Count == 0) throw new ArgumentException("Phải chọn ít nhất một tuần học.");
        return weeks;
    }
    public static bool Overlaps(ClassSchedule a, ClassSchedule b) => a.Thu == b.Thu && a.TietBatDau < b.TietBatDau + b.SoTiet && b.TietBatDau < a.TietBatDau + a.SoTiet && Weeks(a.ChuoiTuanHoc).Overlaps(Weeks(b.ChuoiTuanHoc));
    public static bool IsOpen(HocKy hk)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
        return hk.BatDauDangKy.HasValue && hk.KetThucDangKy.HasValue && now >= hk.BatDauDangKy && now <= hk.KetThucDangKy;
    }
    public static string DayName(int day) => day == 8 ? "Chủ nhật" : new[] { "", "", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" }[day];
    public static bool HasGrades(KetQuaHocTap k) => k.DiemCC != null || k.DiemGK != null || k.DiemCK != null || k.DiemTongKet != null || k.DiemThiLan1 != null || k.DiemThiLan2 != null || !string.IsNullOrWhiteSpace(k.ChiTietDiemQT) || !string.IsNullOrWhiteSpace(k.DiemChu);
}
