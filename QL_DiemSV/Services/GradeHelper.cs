using QL_DiemSV.Data;
using QL_DiemSV.Models;

namespace QL_DiemSV.Services;

public static class GradeHelper
{
    public static string QuyDoiDiemChu(double diemTK)
    {
        if (diemTK >= 9.0) return "A+";
        if (diemTK >= 8.5) return "A";
        if (diemTK >= 8.0) return "B+";
        if (diemTK >= 7.0) return "B";
        if (diemTK >= 6.5) return "C+";
        if (diemTK >= 5.5) return "C";
        if (diemTK >= 5.0) return "D+";
        if (diemTK >= 4.0) return "D";
        return "F";
    }

    public static double QuyDoiHe4(double diemHe10)
    {
        if (diemHe10 >= 8.5) return 4.0;
        if (diemHe10 >= 8.0) return 3.5;
        if (diemHe10 >= 7.0) return 3.0;
        if (diemHe10 >= 6.5) return 2.5;
        if (diemHe10 >= 5.5) return 2.0;
        if (diemHe10 >= 5.0) return 1.5;
        if (diemHe10 >= 4.0) return 1.0;
        return 0.0;
    }

    /// <summary>
    /// Quy tắc tính điểm tổng kết cuối cùng theo tỷ lệ động (TyLeQuaTrinh% - TyLeThiKetThuc%),
    /// bao gồm quy chế QC 6.0 cho thi lại / cải thiện Lần 2, và hỗ trợ môn 100% Quá trình (TyLeThiKetThuc == 0).
    /// DiemCK trong DB đóng vai trò là Điểm Quá Trình Tổng.
    /// </summary>
    public static (double? diemTK, string? diemChu, string? ghiChu) TinhDiemTongKetCuoiCung(
        double? diemQT, double? diemThi1, double? diemThi2, double tyLeQT = 40, double tyLeThi = 60)
    {
        if (diemQT == null)
        {
            return (null, null, null);
        }

        double wQt = tyLeQT / 100.0;
        double wThi = tyLeThi / 100.0;
        double qt = diemQT.Value;

        // Trường hợp môn học 100% Quá trình - 0% Thi kết thúc (Đồ án, Thực hành...)
        if (tyLeThi <= 0)
        {
            double tkQtOnly = Math.Round(qt * wQt, 1, MidpointRounding.AwayFromZero);
            string dcQtOnly = QuyDoiDiemChu(tkQtOnly);
            string gcQtOnly = tkQtOnly >= 4.0 ? "Đạt" : "Học lại";
            return (tkQtOnly, dcQtOnly, gcQtOnly);
        }

        if (diemThi1 == null)
        {
            return (null, null, null);
        }

        double t1 = diemThi1.Value;
        double tk1 = Math.Round((qt * wQt) + (t1 * wThi), 1, MidpointRounding.AwayFromZero);

        if (diemThi2 == null)
        {
            string dc1 = QuyDoiDiemChu(tk1);
            string gc1 = tk1 >= 4.0 ? "Đạt" : "Học lại";
            return (tk1, dc1, gc1);
        }

        double t2 = diemThi2.Value;
        double tk2Raw = Math.Round((qt * wQt) + (t2 * wThi), 1, MidpointRounding.AwayFromZero);

        double finalTK;
        string ghiChu;

        if (tk1 < 4.0)
        {
            // Trường hợp rớt ở Lần 1 -> Thi lại: Điểm tối đa không vượt quá 6.0 (QC 6.0)
            finalTK = Math.Min(tk2Raw, 6.0);
            ghiChu = finalTK >= 4.0 ? "Đạt (Thi lại)" : "Học lại";
        }
        else
        {
            // Trường hợp đã đậu Lần 1 nhưng thi Lần 2 cải thiện: lấy max, nhưng nếu dùng tk2Raw thì giới hạn theo quy chế 6.0 nếu tk1 < 6.0
            double cappedTk2 = tk1 < 6.0 ? Math.Min(tk2Raw, 6.0) : tk2Raw;
            finalTK = Math.Max(tk1, cappedTk2);
            ghiChu = finalTK >= 4.0 ? "Đạt" : "Học lại";
        }

        string diemChu = QuyDoiDiemChu(finalTK);
        return (finalTK, diemChu, ghiChu);
    }

    public static void GhiNhatKy(ApplicationDbContext db, string? nguoiDung, string hanhDong, string chiTiet, string loai = "Thông tin")
    {
        try
        {
            db.NhatKyHoatDongs.Add(new NhatKyHoatDong
            {
                NguoiDung = string.IsNullOrWhiteSpace(nguoiDung) ? "Hệ thống" : nguoiDung,
                HanhDong = hanhDong,
                ChiTiet = chiTiet,
                Loai = loai,
                ThoiGian = DateTime.Now
            });
            db.SaveChanges();
        }
        catch
        {
            // Không làm gián đoạn luồng chính nếu lỗi ghi log
        }
    }
}
