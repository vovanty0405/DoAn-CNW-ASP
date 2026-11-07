using System.Text.RegularExpressions;
using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public sealed record TranscriptTotals
{
    public double? GPAHe10 { get; init; }
    public double? GPAHe4 { get; init; }
    public int TinChiDangKy { get; init; }
    public int TinChiDaCoDiem { get; init; }
    public int TinChiDat { get; init; }
    public int TinChiKhongDat { get; init; }
    public int TinChiChuaCoDiem { get; init; }
    public int SoMonDaCoDiem { get; init; }
    public int SoMonChuaCoDiem { get; init; }
    public string XepLoai { get; init; } = "Chưa xếp loại";
}

public static class TranscriptStatistics
{
    public static int TermOrder(HocKy term)
    {
        var match = Regex.Match(term.TenHK, @"\d+");
        return match.Success && int.TryParse(match.Value, out var number) ? number
            : term.TenHK.Contains("hè", StringComparison.OrdinalIgnoreCase) ? 3 : int.MaxValue;
    }

    // Semester: all attempts in that semester. Cumulative: highest published final
    // grade per subject, using only the attempts supplied by the caller (no future terms).
    public static TranscriptTotals Calculate(IEnumerable<KetQuaHocTap> results, bool cumulative = false)
    {
        var courses = results.Where(x => x.LopHocPhan?.MonHoc != null).ToList();
        if (cumulative)
            courses = courses.GroupBy(x => x.LopHocPhan!.MonHoc!.MaMon)
                .Select(g => g.OrderByDescending(x => x.DiemTongKet.HasValue)
                    .ThenByDescending(x => x.DiemTongKet).ThenByDescending(x => x.MaKQ).First()).ToList();
        int Credits(KetQuaHocTap x) => Math.Max(0, x.LopHocPhan!.MonHoc!.SoTinChi);
        var graded = courses.Where(x => x.DiemTongKet.HasValue).ToList();
        var pending = courses.Where(x => !x.DiemTongKet.HasValue).ToList();
        int credits = graded.Sum(Credits);
        double? average10 = credits > 0 ? graded.Sum(x => x.DiemTongKet!.Value * Credits(x)) / credits : null;
        double? average4 = credits > 0 ? graded.Sum(x => GradeHelper.QuyDoiHe4(x.DiemTongKet!.Value) * Credits(x)) / credits : null;
        return new TranscriptTotals
        {
            GPAHe10 = average10.HasValue ? Math.Round(average10.Value, 2, MidpointRounding.AwayFromZero) : null,
            GPAHe4 = average4.HasValue ? Math.Round(average4.Value, 2, MidpointRounding.AwayFromZero) : null,
            TinChiDangKy = courses.Sum(Credits), TinChiDaCoDiem = credits,
            TinChiDat = graded.Where(x => x.DiemTongKet >= 4).Sum(Credits),
            TinChiKhongDat = graded.Where(x => x.DiemTongKet < 4).Sum(Credits),
            TinChiChuaCoDiem = pending.Sum(Credits), SoMonDaCoDiem = graded.Count, SoMonChuaCoDiem = pending.Count,
            XepLoai = average4 switch
            {
                null => "Chưa xếp loại", >= 3.6 => "Xuất sắc", >= 3.2 => "Giỏi",
                >= 2.5 => "Khá", >= 2 => "Trung bình", _ => "Yếu"
            }
        };
    }
}
