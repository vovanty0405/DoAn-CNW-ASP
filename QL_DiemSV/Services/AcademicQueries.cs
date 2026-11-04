using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using System.Linq.Expressions;

namespace QL_DiemSV.Services;

public static class AcademicQueries
{
    public static async Task<string?> ResolveTermAsync(ApplicationDbContext db, string? termId)
    {
        if (!string.IsNullOrWhiteSpace(termId)) return termId.Trim();
        var terms = await db.HocKys.AsNoTracking().ToListAsync();
        return RoomMatrixRules.ResolveCalendar(terms, null, null, null, null, RoomMatrixRules.Today).Term?.MaHK;
    }

    // List cards never need the potentially very large HTML body or author entity.
    public static readonly Expression<Func<Article, Article>> ArticleList = a => new Article
    {
        Id = a.Id, TieuDe = a.TieuDe, TomTat = a.TomTat, DanhMuc = a.DanhMuc,
        HeDaoTao = a.HeDaoTao, DonViDang = a.DonViDang, NguoiDangId = a.NguoiDangId,
        NgayDang = a.NgayDang, GhimDauTrang = a.GhimDauTrang, FileDinhKemUrl = a.FileDinhKemUrl,
        TenFileDinhKem = a.TenFileDinhKem, DungLuongFile = a.DungLuongFile,
        ThumbnailUrl = a.ThumbnailUrl, TrangThai = a.TrangThai, XuatBanLuc = a.XuatBanLuc
    };

    public static readonly Expression<Func<ClassSchedule, ClassSchedule>> ScheduleList = x => new ClassSchedule
    {
        Id = x.Id, MaLHP = x.MaLHP, MaGV = x.MaGV, PhongHoc = x.PhongHoc,
        Thu = x.Thu, TietBatDau = x.TietBatDau, SoTiet = x.SoTiet,
        LoaiLich = x.LoaiLich, LoaiBuoiHoc = x.LoaiBuoiHoc,
        ChuoiTuanHoc = x.ChuoiTuanHoc, TuanBaoNghi = x.TuanBaoNghi,
        GiangVien = x.GiangVien == null ? null : new GiangVien { MaGV = x.GiangVien.MaGV, HoTen = x.GiangVien.HoTen },
        LopHocPhan = x.LopHocPhan == null ? null : new LopHocPhan
        {
            MaLHP = x.LopHocPhan.MaLHP, MaHK = x.LopHocPhan.MaHK, MaMon = x.LopHocPhan.MaMon,
            TenLopHP = x.LopHocPhan.TenLopHP, NMH = x.LopHocPhan.NMH, TTH = x.LopHocPhan.TTH,
            SiSoToiDa = x.LopHocPhan.SiSoToiDa,
            MonHoc = x.LopHocPhan.MonHoc == null ? null : new MonHoc
            { MaMon = x.LopHocPhan.MonHoc.MaMon, TenMon = x.LopHocPhan.MonHoc.TenMon, SoTinChi = x.LopHocPhan.MonHoc.SoTinChi }
        }
    };
}
