using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public static class GraduationRules
{
    public static (int Total, int Required, int Elective, double MinGpa) Requirements(
        IReadOnlyCollection<KhungChuongTrinh> curriculum, QuyDinhTotNghiep? rule)
    {
        int required = curriculum.Where(c => c.LoaiMon != "Tự chọn").Sum(c => c.MonHoc?.SoTinChi ?? 0);
        int elective = curriculum.Where(c => c.LoaiMon == "Tự chọn").Sum(c => c.MonHoc?.SoTinChi ?? 0);
        return (rule?.TongTTC is > 0 ? rule.TongTTC : (required + elective > 0 ? required + elective : 120),
            rule?.TCBatBuoc ?? required, rule?.TCTuChon ?? elective, rule?.MinGPA ?? 2);
    }

    public static Dictionary<string, bool> CourseTypes(IEnumerable<KhungChuongTrinh> curriculum) =>
        curriculum.Where(c => !string.IsNullOrWhiteSpace(c.MaMon))
            .GroupBy(c => c.MaMon!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Any(c => c.LoaiMon != "Tự chọn"), StringComparer.OrdinalIgnoreCase);

    public sealed record MissingCourse(string MaMon, string TenMon, int SoTinChi, double? DiemDaCongBo);
    public sealed record Gaps(int ThieuTongTinChi, int ThieuTinChiBatBuoc, int ThieuTinChiTuChon,
        double? GpaHienTai, double GpaYeuCau, double? GpaConThieu, List<MissingCourse> MonBatBuocChuaDat)
    {
        public bool ThieuGpa => !GpaHienTai.HasValue || GpaHienTai < GpaYeuCau;
        public bool ThieuTinChi => ThieuTongTinChi > 0 || ThieuTinChiBatBuoc > 0 || ThieuTinChiTuChon > 0;
        public bool NoMonBatBuoc => MonBatBuocChuaDat.Count > 0;
    }
    public static Gaps Describe(IEnumerable<KetQuaHocTap> grades, IReadOnlyCollection<KhungChuongTrinh> curriculum,
        double? gpa, int total, int required, int elective, double minGpa)
    {
        var list = grades.ToList();
        var types = CourseTypes(curriculum);
        var result = Evaluate(list, types, gpa, total, required, elective, minGpa);
        var best = list.Where(k => k.LopHocPhan?.MaMon != null && k.DiemTongKet.HasValue)
            .GroupBy(k => k.LopHocPhan!.MaMon!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(k => k.DiemTongKet), StringComparer.OrdinalIgnoreCase);
        var missing = curriculum.Where(c => c.MaMon != null && types.GetValueOrDefault(c.MaMon))
            .GroupBy(c => c.MaMon!, StringComparer.OrdinalIgnoreCase)
            .Where(g => !best.TryGetValue(g.Key, out var score) || score < 4)
            .Select(g => new MissingCourse(g.Key, g.First().MonHoc?.TenMon ?? g.Key,
                g.First().MonHoc?.SoTinChi ?? 0, best.GetValueOrDefault(g.Key)))
            .OrderBy(c => c.MaMon).ToList();
        return new(Math.Max(0, total-result.PassedCredits), Math.Max(0, required-result.RequiredCredits),
            Math.Max(0, elective-result.ElectiveCredits), gpa, minGpa,
            gpa.HasValue ? Math.Round(Math.Max(0, minGpa-gpa.Value), 2, MidpointRounding.AwayFromZero) : null, missing);
    }

    public record Result(int RequiredCredits, int ElectiveCredits, int FailedCourses, int FailedCredits,
        int MissingRequiredCourses, bool Eligible)
    {
        public int PassedCredits => RequiredCredits + ElectiveCredits;
    }

    public static Result Evaluate(IEnumerable<KetQuaHocTap> grades, IReadOnlyDictionary<string, bool> courseTypes,
        double? gpa, int total, int required, int elective, double minGpa)
    {
        var best = grades.Where(k => k.DiemTongKet.HasValue && k.LopHocPhan?.MaMon != null && k.LopHocPhan.MonHoc != null)
            .GroupBy(k => k.LopHocPhan!.MaMon!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(k => k.DiemTongKet)!).ToList();
        var passed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int passedRequired = 0, passedElective = 0, failedCourses = 0, failedCredits = 0;
        foreach (var grade in best)
        {
            var code = grade.LopHocPhan!.MaMon!;
            int credits = Math.Max(0, grade.LopHocPhan.MonHoc!.SoTinChi);
            if (grade.DiemTongKet >= 4)
            {
                passed.Add(code);
                if (courseTypes.GetValueOrDefault(code)) passedRequired += credits;
                else passedElective += credits;
            }
            else
            {
                failedCourses++;
                failedCredits += credits;
            }
        }

        int missingRequired = courseTypes.Count(c => c.Value && !passed.Contains(c.Key));
        bool eligible = passedRequired + passedElective >= total && passedRequired >= required && passedElective >= elective
            && gpa.HasValue && gpa.Value >= minGpa && missingRequired == 0 && failedCourses == 0;
        return new(passedRequired, passedElective, failedCourses, failedCredits, missingRequired, eligible);
    }
}
