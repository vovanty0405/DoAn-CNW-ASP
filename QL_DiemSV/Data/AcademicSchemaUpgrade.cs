using Microsoft.EntityFrameworkCore;
namespace QL_DiemSV.Data;
// Additive, repeatable upgrade for existing databases created with EnsureCreated.
public static class AcademicSchemaUpgrade
{
    public static void Apply(ApplicationDbContext db)
    {
        using var tx = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw("""
            EXEC sp_getapplock @Resource=N'AcademicLedger.Schema', @LockMode='Exclusive', @LockOwner='Transaction';
            IF COL_LENGTH('LopHocPhan','NMH') IS NULL ALTER TABLE LopHocPhan ADD NMH nvarchar(10) NOT NULL CONSTRAINT DF_LHP_NMH DEFAULT '01';
            IF COL_LENGTH('LopHocPhan','TTH') IS NULL ALTER TABLE LopHocPhan ADD TTH nvarchar(10) NULL;
            IF COL_LENGTH('LopHocPhan','STC') IS NULL BEGIN
                ALTER TABLE LopHocPhan ADD STC int NOT NULL CONSTRAINT DF_LHP_STC DEFAULT 0;
                EXEC(N'UPDATE l SET STC = m.SoTinChi FROM LopHocPhan l JOIN MonHoc m ON l.MaMon=m.MaMon');
            END;
            IF COL_LENGTH('LopHocPhan','STCHP') IS NULL BEGIN
                ALTER TABLE LopHocPhan ADD STCHP decimal(8,2) NOT NULL CONSTRAINT DF_LHP_STCHP DEFAULT 0;
                EXEC(N'UPDATE LopHocPhan SET STCHP=STC');
            END;
            IF COL_LENGTH('LopHocPhan','MaLop') IS NULL ALTER TABLE LopHocPhan ADD MaLop nvarchar(20) NULL;
            IF COL_LENGTH('HocKy','NgayBatDauTuan1') IS NULL ALTER TABLE HocKy ADD NgayBatDauTuan1 datetime2 NULL;
            IF COL_LENGTH('HocKy','BatDauDangKy') IS NULL ALTER TABLE HocKy ADD BatDauDangKy datetime2 NULL;
            IF COL_LENGTH('HocKy','KetThucDangKy') IS NULL ALTER TABLE HocKy ADD KetThucDangKy datetime2 NULL;
            IF COL_LENGTH('HocKy','SoTuan') IS NULL ALTER TABLE HocKy ADD SoTuan int NOT NULL CONSTRAINT DF_HK_SoTuan DEFAULT 20;
            IF COL_LENGTH('HocKy','DonGiaTinChi') IS NULL ALTER TABLE HocKy ADD DonGiaTinChi decimal(18,2) NOT NULL CONSTRAINT DF_HK_DonGia DEFAULT 635000;
            IF COL_LENGTH('KetQuaHocTap','HocPhiDangKy') IS NULL ALTER TABLE KetQuaHocTap ADD HocPhiDangKy decimal(18,2) NULL;
            IF COL_LENGTH('KetQuaHocTap','MienGiam') IS NULL ALTER TABLE KetQuaHocTap ADD MienGiam decimal(18,2) NOT NULL CONSTRAINT DF_KQ_MienGiam DEFAULT 0;
            IF COL_LENGTH('KetQuaHocTap','NgayDangKy') IS NULL ALTER TABLE KetQuaHocTap ADD NgayDangKy datetime2 NULL;
            IF OBJECT_ID('PhongHoc','U') IS NULL CREATE TABLE PhongHoc (MaPhong nvarchar(50) NOT NULL PRIMARY KEY, SucChua int NOT NULL CHECK (SucChua > 0));
            IF OBJECT_ID('ClassSchedule','U') IS NULL BEGIN
                CREATE TABLE ClassSchedule (
                    Id int IDENTITY PRIMARY KEY, MaLHP nvarchar(20) NOT NULL,
                    LoaiBuoiHoc bit NOT NULL, Thu int NOT NULL CHECK (Thu BETWEEN 2 AND 8),
                    TietBatDau int NOT NULL CHECK (TietBatDau BETWEEN 1 AND 12),
                    SoTiet int NOT NULL CHECK (SoTiet > 0), PhongHoc nvarchar(50) NOT NULL,
                    MaGV nvarchar(20) NOT NULL, ChuoiTuanHoc nvarchar(200) NOT NULL,
                    CONSTRAINT CK_Schedule_Periods CHECK (TietBatDau + SoTiet <= 13),
                    FOREIGN KEY (MaLHP) REFERENCES LopHocPhan(MaLHP) ON DELETE CASCADE,
                    FOREIGN KEY (MaGV) REFERENCES GiangVien(MaGV),
                    FOREIGN KEY (PhongHoc) REFERENCES PhongHoc(MaPhong));
                CREATE INDEX IX_ClassSchedule_MaLHP ON ClassSchedule(MaLHP);
                CREATE INDEX IX_ClassSchedule_MaGV ON ClassSchedule(MaGV);
                CREATE INDEX IX_ClassSchedule_PhongHoc ON ClassSchedule(PhongHoc);
            END;
            IF COL_LENGTH('PhongHoc','ToaNha') IS NULL ALTER TABLE PhongHoc ADD ToaNha nvarchar(30) NOT NULL CONSTRAINT DF_Phong_ToaNha DEFAULT 'Khac';
            IF COL_LENGTH('PhongHoc','TrangThai') IS NULL ALTER TABLE PhongHoc ADD TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_Phong_TrangThai DEFAULT 'HoatDong';
            IF COL_LENGTH('ClassSchedule','LoaiLich') IS NULL ALTER TABLE ClassSchedule ADD LoaiLich nvarchar(20) NOT NULL CONSTRAINT DF_Schedule_Loai DEFAULT 'LichHoc';
            IF COL_LENGTH('Nganh','HeDaoTao') IS NULL ALTER TABLE Nganh ADD HeDaoTao nvarchar(50) NOT NULL CONSTRAINT DF_Nganh_He DEFAULT N'Chính quy';
            IF OBJECT_ID('Article','U') IS NULL BEGIN
                CREATE TABLE Article (Id int IDENTITY PRIMARY KEY, TieuDe nvarchar(250) NOT NULL, TomTat nvarchar(1000) NOT NULL,
                    NoiDung nvarchar(max) NOT NULL, DanhMuc nvarchar(40) NOT NULL, DonViDang nvarchar(200) NOT NULL,
                    NguoiDangId int NOT NULL REFERENCES UserAccounts(UserID), NgayDang datetime2 NOT NULL,
                    GhimDauTrang bit NOT NULL, FileDinhKemUrl nvarchar(500) NULL, TenFileDinhKem nvarchar(255) NULL,
                    DungLuongFile bigint NULL, ThumbnailUrl nvarchar(500) NULL, TrangThai nvarchar(20) NOT NULL, XuatBanLuc datetime2 NULL);
                CREATE INDEX IX_Article_TrangThai_DanhMuc_NgayDang ON Article(TrangThai,DanhMuc,NgayDang);
            END;
            IF OBJECT_ID('PortalDocument','U') IS NULL CREATE TABLE PortalDocument (
                Id int IDENTITY PRIMARY KEY,TieuDe nvarchar(250) NOT NULL,Loai nvarchar(30) NOT NULL,
                FileUrl nvarchar(500) NOT NULL,DungLuongBytes bigint NOT NULL,HienThi bit NOT NULL);
            IF COL_LENGTH('ClassSchedule','TuanBaoNghi') IS NULL ALTER TABLE ClassSchedule ADD TuanBaoNghi nvarchar(200) NOT NULL CONSTRAINT DF_Schedule_TuanBaoNghi DEFAULT '';
            """);
        tx.Commit();
    }
}
